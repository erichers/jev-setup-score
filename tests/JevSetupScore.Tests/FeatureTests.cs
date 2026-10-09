using JevSetupScore.Core;

namespace JevSetupScore.Tests;

public class FeatureTests
{
    [Fact]
    public void Features_DoNotUseFutureBars()
    {
        var bars = WeekdayBars(260);
        var full = FeatureBuilder.BuildAll(bars);
        Assert.NotNull(full[230]);

        var prefix = FeatureBuilder.BuildAll(bars.Take(231).ToList());
        Assert.Equal(full[230]!.Values, prefix[230]!.Values);

        var mutated = bars.ToList();
        var future = mutated[250];
        mutated[250] = future with { Close = future.Close * 3, High = future.High * 3, Low = future.Low * 3 };
        var after = FeatureBuilder.BuildAll(mutated);
        Assert.Equal(full[230]!.Values, after[230]!.Values);

        var changed = bars.ToList();
        var current = changed[230];
        changed[230] = current with { Close = current.Close * 1.2, High = current.High * 1.2 };
        var moved = FeatureBuilder.BuildAll(changed);
        Assert.NotEqual(full[230]!.Values[FeatureSchema.Return10], moved[230]!.Values[FeatureSchema.Return10]);
    }

    [Fact]
    public void Uptrend_HasPositiveTrendFeatures()
    {
        var rows = FeatureBuilder.BuildAll(WeekdayBars(260));
        var last = rows.Last(row => row is not null);
        Assert.NotNull(last);
        Assert.True(last!.Values[FeatureSchema.TrendAlignment] > 0);
        Assert.True(last.Values[FeatureSchema.Rsi14] > 0);
        Assert.True(last.Values[FeatureSchema.Return10] > 0);
        Assert.True(last.Values[FeatureSchema.DistMa20] > 0);
        Assert.True(last.Values[FeatureSchema.DailyMa20Slope] > 0);
    }

    private static List<Bar> WeekdayBars(int count)
    {
        var bars = new List<Bar>(count);
        var date = new DateOnly(2020, 1, 2);
        while (bars.Count < count)
        {
            if (date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
            {
                var close = 100 + bars.Count * 0.2;
                bars.Add(new Bar(date, close - 0.1, close + 0.3, close - 0.3, close, 1_000_000 + bars.Count * 100));
            }

            date = date.AddDays(1);
        }

        return bars;
    }
}
