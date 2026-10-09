namespace JevSetupScore.Core;

public readonly record struct ChartPoint(
    DateOnly Date,
    double Open,
    double High,
    double Low,
    double Close,
    long Volume,
    double Ma20,
    double Ma50,
    double Ma200,
    double Rsi,
    double Macd,
    double MacdSignal,
    double MacdHistogram);

public static class ChartBuilder
{
    public static IReadOnlyList<ChartPoint> Build(IReadOnlyList<Bar> bars, int take = 252)
    {
        var n = bars.Count;
        if (n == 0)
            return [];

        var closes = new double[n];
        for (var i = 0; i < n; i++)
            closes[i] = bars[i].Close;

        var sma20 = Indicators.Sma(closes, 20);
        var sma50 = Indicators.Sma(closes, 50);
        var sma200 = Indicators.Sma(closes, 200);
        var rsi = Indicators.Rsi(closes, 14);
        var macd = Indicators.Macd(closes);
        var start = Math.Max(0, n - take);
        var points = new List<ChartPoint>(n - start);
        for (var i = start; i < n; i++)
        {
            var bar = bars[i];
            points.Add(new ChartPoint(
                bar.Date,
                bar.Open,
                bar.High,
                bar.Low,
                bar.Close,
                bar.Volume,
                sma20[i],
                sma50[i],
                sma200[i],
                rsi[i],
                macd.Line[i],
                macd.Signal[i],
                macd.Histogram[i]));
        }

        return points;
    }
}
