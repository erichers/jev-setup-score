using System.Globalization;

namespace JevSetupScore.Core;

public sealed record Prediction(
    string Ticker,
    DateOnly Date,
    DateOnly LabelDate,
    double Probability,
    int Label,
    double ForwardReturn);

public sealed record CalibrationBucket(string Label, double MeanPredicted, double ActualRate, int Count);

public sealed record EquityPoint(DateOnly Date, double Strategy, double BuyHold);

public sealed record BacktestReport(
    int FoldCount,
    int PredictionCount,
    int TradeCount,
    double HitRate,
    double Brier,
    double BaseRate,
    double BaseRateBrier,
    string Honesty,
    IReadOnlyList<CalibrationBucket> Calibration,
    IReadOnlyList<EquityPoint> Equity,
    double StrategyMultiple,
    double BuyHoldMultiple);

public static class BacktestMath
{
    public const int TrainWindow = 504;
    public const int TestWindow = 63;

    public static BacktestReport Summarize(IReadOnlyList<Prediction> predictions, string ticker, int threshold, int foldCount)
    {
        var n = predictions.Count;
        if (n == 0)
        {
            return new BacktestReport(
                foldCount,
                0,
                0,
                0,
                0,
                0,
                0,
                Honesty(0, 0, 0, 0, 0),
                EmptyBuckets(),
                [],
                1,
                1);
        }

        double hits = 0;
        double brier = 0;
        double ups = 0;
        var buckets = new BucketAcc[5];
        foreach (var prediction in predictions)
        {
            var predictedUp = prediction.Probability >= 0.5 ? 1 : 0;
            if (predictedUp == prediction.Label)
                hits++;
            var error = prediction.Probability - prediction.Label;
            brier += error * error;
            ups += prediction.Label;
            var index = prediction.Probability >= 1 ? 4 : Math.Clamp((int)(prediction.Probability * 5), 0, 4);
            buckets[index].Count++;
            buckets[index].Predicted += prediction.Probability;
            buckets[index].Actual += prediction.Label;
        }

        var hitRate = hits / n;
        var brierScore = brier / n;
        var baseRate = ups / n;
        var baseBrier = baseRate * (1 - baseRate);
        var calibration = new CalibrationBucket[5];
        for (var i = 0; i < 5; i++)
        {
            var count = buckets[i].Count;
            calibration[i] = new CalibrationBucket(
                $"{i * 20}-{(i + 1) * 20}%",
                count == 0 ? (i + 0.5) / 5.0 : buckets[i].Predicted / count,
                count == 0 ? 0 : buckets[i].Actual / count,
                count);
        }

        var equity = BuildEquity(predictions, ticker, threshold, out var trades, out var strategy, out var buyHold);
        return new BacktestReport(
            foldCount,
            n,
            trades,
            hitRate,
            brierScore,
            baseRate,
            baseBrier,
            Honesty(n, hitRate, baseRate, brierScore, baseBrier),
            calibration,
            equity,
            strategy,
            buyHold);
    }

    public static string Honesty(int count, double hitRate, double baseRate, double brier, double baseBrier)
    {
        if (count == 0)
            return "There are not enough completed windows for a walk-forward test yet.";

        var hitPct = (hitRate * 100).ToString("0.0", CultureInfo.InvariantCulture);
        var basePct = (baseRate * 100).ToString("0.0", CultureInfo.InvariantCulture);
        var majority = Math.Max(baseRate, 1 - baseRate);

        if (hitRate + 0.01 < majority && brier > baseBrier - 0.002)
            return $"Hit rate is {hitPct} percent against an up-move base rate of {basePct} percent. The model does not beat a simple base-rate guess on this test. Treat the score as a description of the setup, not as an edge.";

        if (hitRate < baseRate + 0.02 && brier > baseBrier - 0.005)
            return $"Hit rate is {hitPct} percent against an up-move base rate of {basePct} percent. The model barely beats the base rate here. Any edge is small and can disappear on new data.";

        if (brier + 0.005 < baseBrier && hitRate >= baseRate)
            return $"Hit rate is {hitPct} percent against an up-move base rate of {basePct} percent. The model beats the base rate on this walk-forward test. The result is historical and can fade.";

        return $"Hit rate is {hitPct} percent against an up-move base rate of {basePct} percent. Compare the Brier score with the base-rate Brier before reading an edge into the score.";
    }

    public static string SummarizeScore(int horizon, double probability, IReadOnlyList<(string Label, double Contribution)> factors)
    {
        var pct = (probability * 100).ToString("0.0", CultureInfo.InvariantCulture);
        var lean = probability switch
        {
            >= 0.6 => "leans up",
            >= 0.5 => "leans slightly up",
            >= 0.4 => "leans slightly down",
            _ => "leans down",
        };
        var top = factors
            .OrderByDescending(factor => Math.Abs(factor.Contribution))
            .Take(2)
            .Select(factor => factor.Label)
            .ToArray();
        var lead = top.Length >= 2
            ? $" The largest contributions are {top[0]} and {top[1]}."
            : "";
        return $"The model {lean}. It puts the chance of a higher close after {horizon} sessions at {pct} percent.{lead}";
    }

    private static List<EquityPoint> BuildEquity(
        IReadOnlyList<Prediction> predictions,
        string ticker,
        int threshold,
        out int trades,
        out double strategy,
        out double buyHold)
    {
        var rows = predictions
            .Where(prediction => string.Equals(prediction.Ticker, ticker, StringComparison.OrdinalIgnoreCase))
            .OrderBy(prediction => prediction.Date)
            .ToList();

        strategy = 1;
        buyHold = 1;
        trades = 0;
        var curve = new List<EquityPoint>(rows.Count);
        var i = 0;
        while (i < rows.Count)
        {
            var row = rows[i];
            var score = (int)Math.Round(row.Probability * 100, MidpointRounding.AwayFromZero);
            var take = score > threshold;
            if (take)
            {
                strategy *= 1 + row.ForwardReturn;
                trades++;
            }

            buyHold *= 1 + row.ForwardReturn;
            curve.Add(new EquityPoint(row.Date, strategy, buyHold));
            var exit = row.LabelDate;
            i++;
            while (i < rows.Count && rows[i].Date < exit)
                i++;
        }

        return curve;
    }

    private static IReadOnlyList<CalibrationBucket> EmptyBuckets()
    {
        var buckets = new CalibrationBucket[5];
        for (var i = 0; i < 5; i++)
            buckets[i] = new CalibrationBucket($"{i * 20}-{(i + 1) * 20}%", (i + 0.5) / 5.0, 0, 0);
        return buckets;
    }

    private struct BucketAcc
    {
        public int Count;
        public double Predicted;
        public double Actual;
    }
}

public static class WalkForwardRunner
{
    public static (IReadOnlyList<Prediction> Predictions, int FoldCount) Run(
        IReadOnlyList<DateOnly> calendar,
        IReadOnlyDictionary<string, IReadOnlyList<Bar>> barsByTicker,
        IReadOnlyDictionary<string, IReadOnlyList<Sample>> samplesByTicker,
        int horizon)
    {
        var folds = WalkForward.Create(calendar.Count, horizon, BacktestMath.TrainWindow, BacktestMath.TestWindow);
        var predictions = new List<Prediction>();
        var usedFolds = 0;

        foreach (var fold in folds)
        {
            var testStartDate = calendar[fold.TestStart];
            var testEndDate = calendar[fold.TestEnd - 1];
            var trainRows = new List<Sample>();

            foreach (var (ticker, samples) in samplesByTicker)
            {
                if (!barsByTicker.TryGetValue(ticker, out var bars))
                    continue;
                var testStartBar = FirstIndexOnOrAfter(bars, testStartDate);
                if (testStartBar < 0)
                    continue;

                foreach (var sample in samples)
                {
                    if (sample.FeatureDate < calendar[fold.TrainFeatureStart] || sample.FeatureDate > calendar[fold.TrainFeatureEnd])
                        continue;
                    if (!WalkForward.IsEligibleTrain(sample.FeatureBarIndex, horizon, testStartBar))
                        continue;
                    trainRows.Add(sample);
                }
            }

            if (trainRows.Count < 80)
                continue;
            var ups = trainRows.Count(sample => sample.Label == 1);
            if (ups == 0 || ups == trainRows.Count)
                continue;

            var x = new double[trainRows.Count][];
            var y = new int[trainRows.Count];
            for (var i = 0; i < trainRows.Count; i++)
            {
                x[i] = trainRows[i].Features;
                y[i] = trainRows[i].Label;
            }

            var model = LogisticRegression.Train(x, y);
            usedFolds++;

            foreach (var (ticker, samples) in samplesByTicker)
            {
                foreach (var sample in samples)
                {
                    if (sample.FeatureDate < testStartDate || sample.FeatureDate > testEndDate)
                        continue;
                    predictions.Add(new Prediction(
                        ticker,
                        sample.FeatureDate,
                        sample.LabelDate,
                        model.PredictProbability(sample.Features),
                        sample.Label,
                        sample.ForwardReturn));
                }
            }
        }

        return (predictions, usedFolds);
    }

    private static int FirstIndexOnOrAfter(IReadOnlyList<Bar> bars, DateOnly date)
    {
        var lo = 0;
        var hi = bars.Count - 1;
        var found = -1;
        while (lo <= hi)
        {
            var mid = (lo + hi) / 2;
            if (bars[mid].Date >= date)
            {
                found = mid;
                hi = mid - 1;
            }
            else
            {
                lo = mid + 1;
            }
        }

        return found;
    }
}
