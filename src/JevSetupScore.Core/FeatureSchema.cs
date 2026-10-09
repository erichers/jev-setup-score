namespace JevSetupScore.Core;

public static class FeatureSchema
{
    public const int DailyMa20Slope = 0;
    public const int DailyMa50Slope = 1;
    public const int WeeklyMa10Slope = 2;
    public const int TrendAlignment = 3;
    public const int Rsi14 = 4;
    public const int MacdHistogram = 5;
    public const int MacdHistogramSlope = 6;
    public const int AtrPercent = 7;
    public const int VolumeRatio = 8;
    public const int DistMa20 = 9;
    public const int DistMa50 = 10;
    public const int DistMa200 = 11;
    public const int Return10 = 12;
    public const int Count = 13;

    public static readonly string[] Keys =
    [
        "dailyMa20Slope",
        "dailyMa50Slope",
        "weeklyMa10Slope",
        "trendAlignment",
        "rsi14",
        "macdHistogram",
        "macdHistogramSlope",
        "atrPercent",
        "volumeRatio",
        "distMa20",
        "distMa50",
        "distMa200",
        "return10",
    ];

    public static readonly string[] Labels =
    [
        "20-day slope",
        "50-day slope",
        "10-week slope",
        "Trend alignment",
        "RSI (14)",
        "MACD histogram",
        "MACD histogram slope",
        "ATR % of price",
        "Volume vs average",
        "Distance from 20-day",
        "Distance from 50-day",
        "Distance from 200-day",
        "10-session return",
    ];
}

public sealed record FactorDisplay(string Key, string Label, double ModelValue, string Display, string Reading);

public sealed class FeatureRow
{
    public required DateOnly Date { get; init; }
    public required int BarIndex { get; init; }
    public required double[] Values { get; init; }
    public required IReadOnlyList<FactorDisplay> Factors { get; init; }
}

public sealed record Sample(
    string Ticker,
    DateOnly FeatureDate,
    DateOnly LabelDate,
    int FeatureBarIndex,
    double[] Features,
    int Label,
    double ForwardReturn);
