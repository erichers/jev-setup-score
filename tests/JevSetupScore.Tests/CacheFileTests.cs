using System.Text.Json;

namespace JevSetupScore.Tests;

public class CacheFileTests
{
    [Fact]
    public void ShippedCache_CoversTheDemoUniverse()
    {
        var root = RepoRoot();
        string[] symbols = ["SPY", "QQQ", "AAPL", "NVDA", "TSLA", "MSFT"];
        foreach (var symbol in symbols)
        {
            var path = Path.Combine(root, "data", "cache", symbol + ".json");
            Assert.True(File.Exists(path), path);
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal(symbol, doc.RootElement.GetProperty("symbol").GetString());
            var bars = doc.RootElement.GetProperty("bars");
            Assert.True(bars.GetArrayLength() >= 1000);
            string? previous = null;
            foreach (var bar in bars.EnumerateArray())
            {
                var date = bar.GetProperty("d").GetString();
                Assert.False(string.IsNullOrWhiteSpace(date));
                if (previous is not null)
                    Assert.True(string.CompareOrdinal(previous, date) < 0);
                previous = date;
                Assert.True(bar.GetProperty("h").GetDouble() + 1e-6 >= bar.GetProperty("l").GetDouble());
                Assert.True(bar.GetProperty("c").GetDouble() > 0);
            }
        }
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "JevSetupScore.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not find the repository root.");
    }
}
