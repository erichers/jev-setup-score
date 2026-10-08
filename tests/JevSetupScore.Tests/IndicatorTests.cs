using JevSetupScore.Core;

namespace JevSetupScore.Tests;

public class IndicatorTests
{
    [Fact]
    public void Rsi14_MatchesWilderValues()
    {
        double[] closes =
        [
            44.34, 44.09, 44.15, 43.61, 44.33, 44.83, 45.10, 45.42, 45.84, 46.08,
            45.89, 46.03, 45.61, 46.28, 46.28, 46.00, 46.03, 46.41, 46.22, 45.64,
            46.21, 46.25, 45.71, 46.45, 45.78, 45.35, 44.03, 44.18, 44.22, 44.57,
            43.42, 42.66, 43.13,
        ];

        var rsi = Indicators.Rsi(closes, 14);

        Assert.Equal(70.4641350211, rsi[14], 6);
        Assert.Equal(37.7887719821, rsi[^1], 6);
        Assert.True(double.IsNaN(rsi[13]));
    }

    [Fact]
    public void Macd_MatchesSmaSeededEma()
    {
        double[] closes =
        [
            20.0, 20.15, 20.4, 20.4, 20.5, 20.7, 20.65, 20.7, 20.85, 21.1,
            21.1, 21.2, 21.4, 21.35, 21.4, 21.55, 21.8, 21.8, 21.9, 22.1,
            22.05, 22.1, 22.25, 22.5, 22.5, 22.6, 22.8, 22.75, 22.8, 22.95,
            23.2, 23.2, 23.3, 23.5, 23.45, 23.5, 23.65, 23.9, 23.9, 24.0,
        ];

        var macd = Indicators.Macd(closes);

        Assert.Equal(0.7025411886, macd.Line[25], 6);
        Assert.Equal(0.7024181270, macd.Signal[33], 6);
        Assert.Equal(0.0073142897, macd.Histogram[33], 6);
        Assert.Equal(0.7015552034, macd.Line[39], 6);
        Assert.Equal(0.0010241500, macd.Histogram[39], 6);
        Assert.True(double.IsNaN(macd.Histogram[32]));
    }

    [Fact]
    public void Atr14_MatchesWilderValues()
    {
        double[] high = [48.70, 48.72, 48.90, 48.87, 48.82, 49.05, 49.20, 49.35, 49.92, 50.19, 50.12, 49.66, 49.88, 50.19, 50.36, 50.57];
        double[] low = [47.79, 48.14, 48.39, 48.37, 48.24, 48.64, 48.94, 48.86, 49.50, 49.87, 49.20, 48.90, 49.43, 49.73, 49.26, 50.09];
        double[] close = [48.16, 48.61, 48.75, 48.63, 48.74, 49.03, 49.07, 49.32, 49.91, 50.13, 49.53, 49.50, 49.75, 50.14, 49.53, 50.31];
        var bars = new List<Bar>(close.Length);
        for (var i = 0; i < close.Length; i++)
            bars.Add(new Bar(new DateOnly(2024, 1, 1).AddDays(i), close[i], high[i], low[i], close[i], 1));

        var atr = Indicators.Atr(bars, 14);

        Assert.Equal(0.5542857143, atr[13], 6);
        Assert.Equal(0.5932653061, atr[14], 6);
        Assert.Equal(0.6251749271, atr[15], 6);
        Assert.True(double.IsNaN(atr[12]));
    }
}
