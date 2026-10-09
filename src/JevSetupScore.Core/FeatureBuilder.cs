using System.Globalization;

namespace JevSetupScore.Core;

public static class FeatureBuilder
{
    public static FeatureRow?[] BuildAll(IReadOnlyList<Bar> bars)
    {
        var n = bars.Count;
        var rows = new FeatureRow?[n];
        if (n == 0)
            return rows;

        var closes = new double[n];
        var volume = new double[n];
        for (var i = 0; i < n; i++)
        {
            closes[i] = bars[i].Close;
            volume[i] = bars[i].Volume;
        }

        var sma20 = Indicators.Sma(closes, 20);
        var sma50 = Indicators.Sma(closes, 50);
        var sma200 = Indicators.Sma(closes, 200);
        var rsi = Indicators.Rsi(closes, 14);
        var macd = Indicators.Macd(closes);
        var atr = Indicators.Atr(bars, 14);
        var volSma = Indicators.Sma(volume, 20);

        var closedWeeks = new List<double>(n / 4);
        double? weekClose = null;
        var weekKey = int.MinValue;

        for (var i = 0; i < n; i++)
        {
            var bar = bars[i];
            var key = WeekKey(bar.Date);
            if (weekClose is null || key != weekKey)
            {
                if (weekClose is not null)
                    closedWeeks.Add(weekClose.Value);
                weekKey = key;
            }

            weekClose = bar.Close;

            if (i < 200 || i < 10)
                continue;
            if (double.IsNaN(sma20[i]) || double.IsNaN(sma50[i]) || double.IsNaN(sma200[i]))
                continue;
            if (double.IsNaN(sma20[i - 5]) || double.IsNaN(sma50[i - 5]))
                continue;
            if (double.IsNaN(rsi[i]) || double.IsNaN(macd.Histogram[i]) || double.IsNaN(macd.Histogram[i - 3]))
                continue;
            if (double.IsNaN(atr[i]) || double.IsNaN(volSma[i]) || volSma[i] == 0)
                continue;

            var weeklyCloses = new List<double>(closedWeeks.Count + 1);
            weeklyCloses.AddRange(closedWeeks);
            weeklyCloses.Add(weekClose.Value);
            if (weeklyCloses.Count < 14)
                continue;

            var weeklySma = Indicators.Sma(weeklyCloses, 10);
            var last = weeklyCloses.Count - 1;
            if (last < 13 || double.IsNaN(weeklySma[last]) || double.IsNaN(weeklySma[last - 4]))
                continue;

            var price = bar.Close;
            if (price <= 0 || sma20[i] == 0 || sma50[i] == 0 || sma200[i] == 0 || closes[i - 10] == 0)
                continue;

            var daily20 = (sma20[i] - sma20[i - 5]) / 5.0 / price;
            var daily50 = (sma50[i] - sma50[i - 5]) / 5.0 / price;
            var weekly = (weeklySma[last] - weeklySma[last - 4]) / 4.0 / price;
            var above20 = price > sma20[i] ? 1d : -1d;
            var stack20 = sma20[i] > sma50[i] ? 1d : -1d;
            var stack50 = sma50[i] > sma200[i] ? 1d : -1d;
            var alignment = (above20 + stack20 + stack50) / 3.0;
            var rsiValue = rsi[i];
            var hist = macd.Histogram[i];
            var histSlope = macd.Histogram[i] - macd.Histogram[i - 3];
            var atrPct = atr[i] / price;
            var volRatio = volume[i] / volSma[i] - 1.0;
            var dist20 = price / sma20[i] - 1.0;
            var dist50 = price / sma50[i] - 1.0;
            var dist200 = price / sma200[i] - 1.0;
            var ret10 = price / closes[i - 10] - 1.0;
            var upChecks = (above20 > 0 ? 1 : 0) + (stack20 > 0 ? 1 : 0) + (stack50 > 0 ? 1 : 0);

            var values = new[]
            {
                daily20, daily50, weekly, alignment, (rsiValue - 50.0) / 50.0,
                hist / price, histSlope / price, atrPct, volRatio, dist20, dist50, dist200, ret10,
            };

            rows[i] = new FeatureRow
            {
                Date = bar.Date,
                BarIndex = i,
                Values = values,
                Factors = Describe(values, rsiValue, hist, histSlope, upChecks, volume[i] / volSma[i]),
            };
        }

        return rows;
    }

    public static List<Sample> BuildSamples(string ticker, IReadOnlyList<Bar> bars, FeatureRow?[] rows, int horizon)
    {
        var samples = new List<Sample>();
        if (horizon < 1)
            return samples;

        for (var i = 0; i < bars.Count; i++)
        {
            var row = rows[i];
            if (row is null)
                continue;
            var labelIndex = i + horizon;
            if (labelIndex >= bars.Count)
                continue;
            var start = bars[i].Close;
            if (start == 0)
                continue;
            var forward = bars[labelIndex].Close / start - 1.0;
            samples.Add(new Sample(
                ticker,
                bars[i].Date,
                bars[labelIndex].Date,
                i,
                row.Values,
                forward > 0 ? 1 : 0,
                forward));
        }

        return samples;
    }

    public static FeatureRow? Latest(FeatureRow?[] rows)
    {
        for (var i = rows.Length - 1; i >= 0; i--)
        {
            if (rows[i] is not null)
                return rows[i];
        }

        return null;
    }

    private static int WeekKey(DateOnly date)
    {
        var dt = date.ToDateTime(TimeOnly.MinValue);
        return System.Globalization.ISOWeek.GetYear(dt) * 100 + System.Globalization.ISOWeek.GetWeekOfYear(dt);
    }

    private static IReadOnlyList<FactorDisplay> Describe(
        double[] values,
        double rsi,
        double hist,
        double histSlope,
        int upChecks,
        double volumeTimes)
    {
        var factors = new FactorDisplay[FeatureSchema.Count];
        factors[FeatureSchema.DailyMa20Slope] = Factor(
            FeatureSchema.DailyMa20Slope,
            values,
            SignedPct(values[FeatureSchema.DailyMa20Slope], 2) + " per session",
            SlopeSentence("20-day average", values[FeatureSchema.DailyMa20Slope], "session"));
        factors[FeatureSchema.DailyMa50Slope] = Factor(
            FeatureSchema.DailyMa50Slope,
            values,
            SignedPct(values[FeatureSchema.DailyMa50Slope], 2) + " per session",
            SlopeSentence("50-day average", values[FeatureSchema.DailyMa50Slope], "session"));
        factors[FeatureSchema.WeeklyMa10Slope] = Factor(
            FeatureSchema.WeeklyMa10Slope,
            values,
            SignedPct(values[FeatureSchema.WeeklyMa10Slope], 2) + " per week",
            SlopeSentence("10-week average", values[FeatureSchema.WeeklyMa10Slope], "week"));
        factors[FeatureSchema.TrendAlignment] = Factor(
            FeatureSchema.TrendAlignment,
            values,
            $"{upChecks} of 3 up",
            upChecks == 3
                ? "Price and the 20, 50, and 200 day averages are stacked up."
                : upChecks == 0
                    ? "Price and the 20, 50, and 200 day averages are stacked down."
                    : $"{upChecks} of 3 trend checks are up. The stack is mixed.");
        factors[FeatureSchema.Rsi14] = Factor(
            FeatureSchema.Rsi14,
            values,
            rsi.ToString("0.0", CultureInfo.InvariantCulture),
            RsiSentence(rsi));
        factors[FeatureSchema.MacdHistogram] = Factor(
            FeatureSchema.MacdHistogram,
            values,
            hist.ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture),
            hist >= 0 ? "MACD is above its signal line." : "MACD is below its signal line.");
        factors[FeatureSchema.MacdHistogramSlope] = Factor(
            FeatureSchema.MacdHistogramSlope,
            values,
            histSlope.ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture),
            histSlope > 0 ? "The MACD histogram is rising." : histSlope < 0 ? "The MACD histogram is falling." : "The MACD histogram is flat.");
        factors[FeatureSchema.AtrPercent] = Factor(
            FeatureSchema.AtrPercent,
            values,
            Pct(values[FeatureSchema.AtrPercent], 1) + " of price",
            AtrSentence(values[FeatureSchema.AtrPercent]));
        factors[FeatureSchema.VolumeRatio] = Factor(
            FeatureSchema.VolumeRatio,
            values,
            volumeTimes.ToString("0.00", CultureInfo.InvariantCulture) + "x average",
            VolumeSentence(volumeTimes));
        factors[FeatureSchema.DistMa20] = Factor(
            FeatureSchema.DistMa20,
            values,
            SignedPct(values[FeatureSchema.DistMa20], 1),
            DistanceSentence(values[FeatureSchema.DistMa20], "20-day"));
        factors[FeatureSchema.DistMa50] = Factor(
            FeatureSchema.DistMa50,
            values,
            SignedPct(values[FeatureSchema.DistMa50], 1),
            DistanceSentence(values[FeatureSchema.DistMa50], "50-day"));
        factors[FeatureSchema.DistMa200] = Factor(
            FeatureSchema.DistMa200,
            values,
            SignedPct(values[FeatureSchema.DistMa200], 1),
            DistanceSentence(values[FeatureSchema.DistMa200], "200-day"));
        factors[FeatureSchema.Return10] = Factor(
            FeatureSchema.Return10,
            values,
            SignedPct(values[FeatureSchema.Return10], 1),
            $"The last 10 sessions returned {SignedPct(values[FeatureSchema.Return10], 1)}.");
        return factors;
    }

    private static FactorDisplay Factor(int index, double[] values, string display, string reading) =>
        new(FeatureSchema.Keys[index], FeatureSchema.Labels[index], values[index], display, reading);

    private static string SlopeSentence(string name, double slope, string unit)
    {
        var speed = Pct(Math.Abs(slope), 2);
        if (slope > 0.00005)
            return $"The {name} is rising by {speed} per {unit}.";
        if (slope < -0.00005)
            return $"The {name} is falling by {speed} per {unit}.";
        return $"The {name} is flat.";
    }

    private static string RsiSentence(double rsi)
    {
        var text = rsi.ToString("0.0", CultureInfo.InvariantCulture);
        if (rsi >= 70)
            return $"RSI is {text}, stretched to the upside.";
        if (rsi <= 30)
            return $"RSI is {text}, stretched to the downside.";
        if (rsi >= 55)
            return $"RSI is {text}, leaning up.";
        if (rsi <= 45)
            return $"RSI is {text}, leaning down.";
        return $"RSI is {text}, near the middle.";
    }

    private static string AtrSentence(double atrPct)
    {
        var size = atrPct >= 0.03 ? "wide" : atrPct <= 0.012 ? "tight" : "ordinary";
        return $"The daily range is {size}, at {Pct(atrPct, 1)} of price.";
    }

    private static string VolumeSentence(double times)
    {
        if (times >= 1.25)
            return $"Volume is heavy, at {times.ToString("0.00", CultureInfo.InvariantCulture)} times the 20-day average.";
        if (times <= 0.75)
            return $"Volume is light, at {times.ToString("0.00", CultureInfo.InvariantCulture)} times the 20-day average.";
        return $"Volume is near its 20-day average, at {times.ToString("0.00", CultureInfo.InvariantCulture)} times.";
    }

    private static string DistanceSentence(double distance, string average) =>
        $"Price is {SignedPct(distance, 1)} versus the {average} average.";

    private static string Pct(double fraction, int digits) =>
        (fraction * 100).ToString("0." + new string('0', digits), CultureInfo.InvariantCulture) + "%";

    private static string SignedPct(double fraction, int digits)
    {
        var value = fraction * 100;
        var body = Math.Abs(value).ToString("0." + new string('0', digits), CultureInfo.InvariantCulture) + "%";
        if (value > 0)
            return "+" + body;
        if (value < 0)
            return "-" + body;
        return body;
    }
}
