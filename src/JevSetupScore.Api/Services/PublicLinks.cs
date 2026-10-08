namespace JevSetupScore.Api.Services;

public sealed class PublicLinks
{
    private readonly string _base;

    public PublicLinks(IConfiguration configuration)
        : this(configuration["PublicBaseUrl"])
    {
    }

    public PublicLinks(string? publicBaseUrl)
    {
        _base = (publicBaseUrl ?? "").Trim().TrimEnd('/');
    }

    public string Report(string ticker, int horizon, int threshold)
    {
        var relative = $"api/score/{Uri.EscapeDataString(ticker)}/report.pdf?horizon={horizon}&threshold={threshold}";
        return string.IsNullOrEmpty(_base) ? relative : $"{_base}/{relative}";
    }
}
