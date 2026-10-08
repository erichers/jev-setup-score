namespace JevSetupScore.Api.Contracts;

public sealed record TickerSummary(
    string Symbol,
    string Name,
    string Kind,
    string AsOf,
    double Last,
    double ChangePercent);

public sealed record FactorDto(
    string Key,
    string Label,
    string Display,
    string Reading,
    double Coefficient,
    double Contribution);

public sealed record CoefficientDto(string Key, string Label, double Weight, string Meaning);

public sealed record ChartPointDto(
    string Date,
    double Open,
    double High,
    double Low,
    double Close,
    long Volume,
    double? Ma20,
    double? Ma50,
    double? Ma200,
    double? Rsi,
    double? Macd,
    double? MacdSignal,
    double? MacdHistogram);

public sealed record CalibrationDto(string Label, double MeanPredicted, double ActualRate, int Count);

public sealed record EquityDto(string Date, double Strategy, double BuyHold);

public sealed record BacktestDto(
    int FoldCount,
    int PredictionCount,
    int TradeCount,
    double HitRate,
    double Brier,
    double BaseRate,
    double BaseRateBrier,
    string Honesty,
    IReadOnlyList<CalibrationDto> Calibration,
    IReadOnlyList<EquityDto> Equity,
    double StrategyMultiple,
    double BuyHoldMultiple);

public sealed record ScoreResponse(
    string Ticker,
    string Name,
    int Horizon,
    int Threshold,
    string AsOf,
    string DataSource,
    string DataNote,
    int Score,
    double Probability,
    string Summary,
    int TrainingRows,
    bool ModelConverged,
    IReadOnlyList<FactorDto> Factors,
    IReadOnlyList<CoefficientDto> Coefficients,
    IReadOnlyList<ChartPointDto> Chart,
    BacktestDto Backtest);
