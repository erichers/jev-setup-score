using JevSetupScore.Api.Services;

namespace JevSetupScore.Tests;

public class DeployTests
{
    [Fact]
    public void EmptyPublicBase_KeepsTheReportLinkRelative()
    {
        var links = new PublicLinks("");
        Assert.Equal(
            "api/score/SPY/report.pdf?horizon=10&threshold=60",
            links.Report("SPY", 10, 60));
    }

    [Fact]
    public void PublicBaseUrl_PrefixesServerLinks()
    {
        var links = new PublicLinks("http://localhost:8888/apps/jev-setup-score/");
        Assert.Equal(
            "http://localhost:8888/apps/jev-setup-score/api/score/NVDA/report.pdf?horizon=5&threshold=70",
            links.Report("NVDA", 5, 70));
    }

    [Fact]
    public void Frontend_DoesNotCallAbsoluteApiPaths()
    {
        var root = RepoRoot();
        var src = Path.Combine(root, "web", "src");
        string[] forbidden = ["'/api/", "\"/api/", "`/api/"];
        foreach (var file in Directory.EnumerateFiles(src, "*.*", SearchOption.AllDirectories))
        {
            if (!file.EndsWith(".ts") && !file.EndsWith(".html"))
                continue;
            var text = File.ReadAllText(file);
            foreach (var pattern in forbidden)
                Assert.DoesNotContain(pattern, text);
        }
    }

    [Fact]
    public void MySqlMigrations_UseUtf8Mb4AndAvoidMysql8OnlySql()
    {
        var dir = Path.Combine(RepoRoot(), "src", "JevSetupScore.Api", "Data", "Migrations", "MySql");
        var files = Directory.GetFiles(dir, "*.cs");
        Assert.NotEmpty(files);
        var text = string.Join("\n", files.Select(File.ReadAllText));
        Assert.Contains("utf8mb4", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("utf8mb4_unicode_ci", text, StringComparison.Ordinal);
        Assert.DoesNotContain("utf8mb4_0900", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"json\"", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IsDescending", text, StringComparison.Ordinal);
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
