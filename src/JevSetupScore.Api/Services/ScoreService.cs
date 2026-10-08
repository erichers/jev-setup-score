using System.Globalization;
using JevSetupScore.Api.Contracts;
using JevSetupScore.Api.Data;
using JevSetupScore.Core;
using Microsoft.EntityFrameworkCore;

namespace JevSetupScore.Api.Services;

public sealed class StudyCache
{
    private readonly object _gate = new();
    private readonly Dictionary<int, (string Stamp, Study Study)> _byHorizon = new();

    public Study GetOrBuild(int horizon, string stamp, Func<Study> build)
    {
        lock (_gate)
        {
            if (_byHorizon.TryGetValue(horizon, out var cached) && cached.Stamp == stamp)
                return cached.Study;
            var study = build();
            _byHorizon[horizon] = (stamp, study);
            return study;
        }
    }
}

public sealed class Study
{
    public required LogisticModel Model { get; init; }
    public required IReadOnlyList<Prediction> Predictions { get; init; }
    public required int FoldCount { get; init; }
    public required int TrainingRows { get; init; }
    public required Dictionary<string, FeatureRow> Latest { get; init; }
}

public sealed class ScoreService(MarketDataService market, StudyCache cache, AppDbContext db, ILogger<ScoreService> logger)
{
    public async Task<ScoreResponse> ScoreAsync(string ticker, int horizon, int threshold, CancellationToken ct)
    {
        if (horizon is not (5 or 10 or 20))
            throw new SetupException(400, "Horizon must be 5, 10, or 20 sessions.");
        if (threshold is < 50 or > 90)
            throw new SetupException(400, "Threshold must be from 50 to 90.");

        var loaded = await market.LoadForScoreAsync(ticker, ct);
        var universe = await market.LoadAllAsync(ct);
        if (!universe.TryGetValue(loaded.Symbol, out var series) || series.Count < loaded.Bars.Count)
            universe[loaded.Symbol] = loaded.Bars.ToList();

        var usable = universe
            .Where(pair => pair.Value.Count >= 260)
            .ToDictionary(pair => pair.Key, pair => (IReadOnlyList<Bar>)pair.Value);
        if (!usable.ContainsKey(loaded.Symbol))
            throw new SetupException(422, $"{loaded.Symbol} does not have enough daily history to score.");

        var stamp = string.Join("|", usable
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}:{pair.Value.Count}:{pair.Value[^1].Date:yyyyMMdd}"));

        var study = cache.GetOrBuild(horizon, stamp, () => Build(usable, horizon));
        if (!study.Latest.TryGetValue(loaded.Symbol, out var latest))
            throw new SetupException(422, $"{loaded.Symbol} does not have enough history for the indicators.");

        var probability = study.Model.PredictProbability(latest.Values);
        var contributions = study.Model.Contributions(latest.Values);
        var factors = new List<FactorDto>(FeatureSchema.Count);
        for (var i = 0; i < FeatureSchema.Count; i++)
        {
            var display = latest.Factors[i];
            factors.Add(new FactorDto(
                display.Key,
                display.Label,
                display.Display,
                display.Reading,
                study.Model.Weights[i],
                contributions[i]));
        }

        factors = factors.OrderByDescending(factor => Math.Abs(factor.Contribution)).ToList();
        var score = Math.Clamp((int)Math.Round(probability * 100, MidpointRounding.AwayFromZero), 0, 100);
        var report = BacktestMath.Summarize(study.Predictions, loaded.Symbol, threshold, study.FoldCount);
        var name = await market.NameAsync(loaded.Symbol, ct);

        var response = new ScoreResponse(
            loaded.Symbol,
            name,
            horizon,
            threshold,
            latest.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            loaded.Source,
            loaded.Note,
            score,
            probability,
            BacktestMath.SummarizeScore(horizon, probability, factors.Select(factor => (factor.Label, factor.Contribution)).ToList()),
            study.TrainingRows,
            study.Model.Converged,
            factors,
            Coefficients(study.Model),
            ChartBuilder.Build(loaded.Bars).Select(ToChart).ToList(),
            ToBacktest(report));

        db.ScoreQueries.Add(new ScoreQuery
        {
            Symbol = response.Ticker,
            Horizon = response.Horizon,
            Threshold = response.Threshold,
            Score = response.Score,
            Probability = response.Probability,
            AsOf = latest.Date,
            DataSource = response.DataSource,
            Summary = response.Summary,
            CreatedUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
        return response;
    }

    public async Task<IReadOnlyList<ExampleOutcome>> ExamplesAsync(CancellationToken ct)
    {
        var study = await StudyForHorizonAsync(10, ct);
        var names = await db.Tickers.AsNoTracking().ToDictionaryAsync(ticker => ticker.Symbol, ticker => ticker.Name, ct);
        var latest = study.Predictions
            .Where(prediction => MarketDataService.Universe.Any(ticker => ticker.Symbol == prediction.Ticker))
            .GroupBy(prediction => prediction.Ticker, StringComparer.Ordinal)
            .Select(group => group.OrderByDescending(prediction => prediction.Date).First())
            .OrderByDescending(prediction => prediction.Date)
            .ThenBy(prediction => prediction.Ticker, StringComparer.Ordinal)
            .ToList();

        return latest.Select(prediction =>
        {
            var score = Math.Clamp((int)Math.Round(prediction.Probability * 100, MidpointRounding.AwayFromZero), 0, 100);
            var up = prediction.Label == 1;
            var move = Math.Abs(prediction.ForwardReturn * 100).ToString("0.0", CultureInfo.InvariantCulture);
            var direction = up ? "up" : "down";
            var feature = prediction.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var label = prediction.LabelDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var known = MarketDataService.Universe.First(ticker => ticker.Symbol == prediction.Ticker);
            return new ExampleOutcome(
                prediction.Ticker,
                names.GetValueOrDefault(prediction.Ticker) ?? known.Name,
                feature,
                label,
                score,
                prediction.Probability,
                prediction.ForwardReturn,
                up,
                $"On {feature} the model scored {score}. By {label} the close was {direction} {move}%.");
        }).ToList();
    }

    public async Task<IReadOnlyList<HistoryItem>> HistoryAsync(int limit, CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, 50);
        var rows = await db.ScoreQueries.AsNoTracking()
            .OrderByDescending(query => query.CreatedUtc)
            .ThenByDescending(query => query.Id)
            .Take(limit)
            .ToListAsync(ct);
        return rows.Select(query => new HistoryItem(
            query.Id,
            query.Symbol,
            query.Horizon,
            query.Threshold,
            query.Score,
            query.Probability,
            query.AsOf.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            query.DataSource,
            query.Summary,
            query.CreatedUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture))).ToList();
    }

    private async Task<Study> StudyForHorizonAsync(int horizon, CancellationToken ct)
    {
        var universe = await market.LoadAllAsync(ct);
        var usable = universe
            .Where(pair => pair.Value.Count >= 260)
            .ToDictionary(pair => pair.Key, pair => (IReadOnlyList<Bar>)pair.Value, StringComparer.Ordinal);
        if (usable.Count == 0)
            throw new SetupException(422, "Not enough cached history to build examples.");

        var stamp = string.Join("|", usable
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}:{pair.Value.Count}:{pair.Value[^1].Date:yyyyMMdd}"));
        return cache.GetOrBuild(horizon, stamp, () => Build(usable, horizon));
    }

    private Study Build(IReadOnlyDictionary<string, IReadOnlyList<Bar>> universe, int horizon)
    {
        var started = DateTime.UtcNow;
        var samples = new Dictionary<string, IReadOnlyList<Sample>>(StringComparer.OrdinalIgnoreCase);
        var latest = new Dictionary<string, FeatureRow>(StringComparer.OrdinalIgnoreCase);
        var all = new List<Sample>();
        foreach (var (ticker, bars) in universe)
        {
            var rows = FeatureBuilder.BuildAll(bars);
            var tickerSamples = FeatureBuilder.BuildSamples(ticker, bars, rows, horizon);
            samples[ticker] = tickerSamples;
            all.AddRange(tickerSamples);
            var row = FeatureBuilder.Latest(rows);
            if (row is not null)
                latest[ticker] = row;
        }

        if (all.Count < 80)
            throw new SetupException(422, "Not enough labeled history to train the model.");

        var model = LogisticRegression.Train(
            all.Select(sample => sample.Features).ToArray(),
            all.Select(sample => sample.Label).ToArray());

        var calendar = universe.Values
            .SelectMany(bars => bars.Select(bar => bar.Date))
            .Distinct()
            .OrderBy(date => date)
            .ToList();
        var (predictions, folds) = WalkForwardRunner.Run(calendar, universe, samples, horizon);
        logger.LogInformation(
            "Trained horizon {Horizon} on {Rows} rows in {Ms} ms. Walk-forward folds: {Folds}.",
            horizon,
            all.Count,
            (DateTime.UtcNow - started).TotalMilliseconds,
            folds);

        return new Study
        {
            Model = model,
            Predictions = predictions,
            FoldCount = folds,
            TrainingRows = all.Count,
            Latest = latest,
        };
    }

    private static IReadOnlyList<CoefficientDto> Coefficients(LogisticModel model)
    {
        var rows = new List<CoefficientDto>(FeatureSchema.Count + 1)
        {
            new("intercept", "Intercept", model.Intercept, "Log-odds when every standardized input is at its average."),
        };
        for (var i = 0; i < FeatureSchema.Count; i++)
        {
            var weight = model.Weights[i];
            var meaning = weight >= 0
                ? "A higher value raises the chance of an up move."
                : "A higher value lowers the chance of an up move.";
            rows.Add(new CoefficientDto(FeatureSchema.Keys[i], FeatureSchema.Labels[i], weight, meaning));
        }

        return rows;
    }

    private static ChartPointDto ToChart(ChartPoint point) => new(
        point.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        point.Open,
        point.High,
        point.Low,
        point.Close,
        point.Volume,
        Clean(point.Ma20),
        Clean(point.Ma50),
        Clean(point.Ma200),
        Clean(point.Rsi),
        Clean(point.Macd),
        Clean(point.MacdSignal),
        Clean(point.MacdHistogram));

    private static BacktestDto ToBacktest(BacktestReport report) => new(
        report.FoldCount,
        report.PredictionCount,
        report.TradeCount,
        report.HitRate,
        report.Brier,
        report.BaseRate,
        report.BaseRateBrier,
        report.Honesty,
        report.Calibration.Select(bucket => new CalibrationDto(bucket.Label, bucket.MeanPredicted, bucket.ActualRate, bucket.Count)).ToList(),
        report.Equity.Select(point => new EquityDto(point.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), point.Strategy, point.BuyHold)).ToList(),
        report.StrategyMultiple,
        report.BuyHoldMultiple);

    private static double? Clean(double value) => double.IsFinite(value) ? value : null;
}
