namespace JevSetupScore.Api.Services;

public sealed class DataOptions
{
    public string CacheDirectory { get; set; } = "";
    public bool AllowLiveFetch { get; set; } = true;
    public string YahooBaseUrl { get; set; } = "https://query1.finance.yahoo.com/v8/finance/chart/";
    public string StooqBaseUrl { get; set; } = "https://stooq.com/q/d/l/";
    public int HttpTimeoutSeconds { get; set; } = 8;
}
