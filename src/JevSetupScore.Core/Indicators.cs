namespace JevSetupScore.Core;

public readonly record struct MacdSeries(double[] Line, double[] Signal, double[] Histogram);

/// <summary>
/// Causal indicators. Value at index i uses only observations at indexes through i.
/// RSI and ATR use Wilder smoothing. EMA is seeded with a simple average.
/// </summary>
public static class Indicators
{
    public static double[] Sma(IReadOnlyList<double> values, int period)
    {
        var n = values.Count;
        var result = new double[n];
        Array.Fill(result, double.NaN);
        if (period <= 0 || n < period)
            return result;

        double sum = 0;
        for (var i = 0; i < n; i++)
        {
            sum += values[i];
            if (i >= period)
                sum -= values[i - period];
            if (i >= period - 1)
                result[i] = sum / period;
        }

        return result;
    }

    public static double[] Ema(IReadOnlyList<double> values, int period)
    {
        var n = values.Count;
        var result = new double[n];
        Array.Fill(result, double.NaN);
        if (period <= 0 || n < period)
            return result;

        double sum = 0;
        for (var i = 0; i < period; i++)
            sum += values[i];

        result[period - 1] = sum / period;
        var k = 2.0 / (period + 1);
        for (var i = period; i < n; i++)
            result[i] = values[i] * k + result[i - 1] * (1 - k);

        return result;
    }

    public static double[] Rsi(IReadOnlyList<double> closes, int period = 14)
    {
        var n = closes.Count;
        var result = new double[n];
        Array.Fill(result, double.NaN);
        if (period <= 0 || n <= period)
            return result;

        double gain = 0;
        double loss = 0;
        for (var i = 1; i <= period; i++)
        {
            var change = closes[i] - closes[i - 1];
            if (change >= 0)
                gain += change;
            else
                loss -= change;
        }

        var avgGain = gain / period;
        var avgLoss = loss / period;
        result[period] = FromAverages(avgGain, avgLoss);

        for (var i = period + 1; i < n; i++)
        {
            var change = closes[i] - closes[i - 1];
            var g = change > 0 ? change : 0;
            var l = change < 0 ? -change : 0;
            avgGain = (avgGain * (period - 1) + g) / period;
            avgLoss = (avgLoss * (period - 1) + l) / period;
            result[i] = FromAverages(avgGain, avgLoss);
        }

        return result;
    }

    public static MacdSeries Macd(IReadOnlyList<double> closes, int fast = 12, int slow = 26, int signalPeriod = 9)
    {
        var n = closes.Count;
        var line = new double[n];
        var signal = new double[n];
        var histogram = new double[n];
        Array.Fill(line, double.NaN);
        Array.Fill(signal, double.NaN);
        Array.Fill(histogram, double.NaN);

        if (n < slow || fast <= 0 || slow <= fast || signalPeriod <= 0)
            return new MacdSeries(line, signal, histogram);

        var emaFast = Ema(closes, fast);
        var emaSlow = Ema(closes, slow);
        var macdValues = new List<double>(n - slow + 1);
        for (var i = slow - 1; i < n; i++)
        {
            line[i] = emaFast[i] - emaSlow[i];
            macdValues.Add(line[i]);
        }

        var signalEma = Ema(macdValues, signalPeriod);
        for (var k = 0; k < signalEma.Length; k++)
        {
            if (double.IsNaN(signalEma[k]))
                continue;
            var i = (slow - 1) + k;
            signal[i] = signalEma[k];
            histogram[i] = line[i] - signal[i];
        }

        return new MacdSeries(line, signal, histogram);
    }

    public static double[] Atr(IReadOnlyList<Bar> bars, int period = 14)
    {
        var n = bars.Count;
        var atr = new double[n];
        Array.Fill(atr, double.NaN);
        if (period <= 0 || n == 0)
            return atr;

        var tr = new double[n];
        tr[0] = bars[0].High - bars[0].Low;
        for (var i = 1; i < n; i++)
        {
            var high = bars[i].High;
            var low = bars[i].Low;
            var prev = bars[i - 1].Close;
            var range = high - low;
            var up = Math.Abs(high - prev);
            var down = Math.Abs(low - prev);
            tr[i] = Math.Max(range, Math.Max(up, down));
        }

        if (n < period)
            return atr;

        double sum = 0;
        for (var i = 0; i < period; i++)
            sum += tr[i];

        atr[period - 1] = sum / period;
        for (var i = period; i < n; i++)
            atr[i] = (atr[i - 1] * (period - 1) + tr[i]) / period;

        return atr;
    }

    private static double FromAverages(double avgGain, double avgLoss)
    {
        if (avgGain == 0 && avgLoss == 0)
            return 50;
        if (avgLoss == 0)
            return 100;
        var rs = avgGain / avgLoss;
        return 100 - (100 / (1 + rs));
    }
}
