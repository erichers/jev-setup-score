namespace JevSetupScore.Core;

public readonly record struct Bar(
    DateOnly Date,
    double Open,
    double High,
    double Low,
    double Close,
    long Volume);
