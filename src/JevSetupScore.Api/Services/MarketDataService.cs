using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using JevSetupScore.Api.Contracts;
using JevSetupScore.Api.Data;
using JevSetupScore.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JevSetupScore.Api.Services;

public sealed class MarketDataService
{
    public static readonly IReadOnlyList<(string Symbol, string Name, string Kind)> Universe =
    [
        ("SPY", "SPDR S&P 500 ETF", "ETF"),
        ("QQQ", "Invesco QQQ Trust", "ETF"),
        ("AAPL", "Apple", "Equity"),
        ("NVDA", "NVIDIA", "Equity"),
        ("TSLA", "Tesla", "Equity"),
        ("MSFT", "Microsoft", "Equity"),
        ("AMZN", "Amazon", "Equity"),
        ("GOOGL", "Alphabet", "Equity"),
        ("META", "Meta", "Equity"),
        ("AMD", "AMD", "Equity"),
        ("JPM", "JPMorgan Chase", "Equity"),
        ("NFLX", "Netflix", "Equity"),
        ("AVGO", "Broadcom", "Equity"),
        ("COST", "Costco", "Equity"),
        ("WMT", "Walmart", "Equity"),
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly Regex SymbolPattern = new("^[A-Z0-9.\\-]{1,12}$", RegexOptions.Compiled);

    private readonly AppDbContext _db;
    private readonly HttpClient _http;
    private readonly DataOptions _options;
    private readonly ILogger<MarketDataService> _logger;
    private readonly TimeZoneInfo _newYork;

    public MarketDataService(AppDbContext db, HttpClient http, IOptions<DataOptions> options, ILogger<MarketDataService> logger)
    {
        _db = db;
        _http = http;
        _options = options.Value;
        _logger = logger;
        try
        {
            _newYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        }
        catch (TimeZoneNotFoundException)
        {
            _newYork = TimeZoneInfo.Utc;
        }
    }

    public async Task InitializeAsync(CancellationToken ct)
    {
        await _db.Database.MigrateAsync(ct);
        foreach (var ticker in Universe)
        {
            var row = await _db.Tickers.FirstOrDefaultAsync(item => item.Symbol == ticker.Symbol, ct);
            if (row is null)
            {
                _db.Tickers.Add(new TickerRow
                {
                    Symbol = ticker.Symbol,
                    Name = ticker.Name,
                    Kind = ticker.Kind,
                });
            }
            else
            {
                row.Name = ticker.Name;
                row.Kind = ticker.Kind;
            }
        }

        await _db.SaveChangesAsync(ct);

        if (!Directory.Exists(_options.CacheDirectory))
            return;

        foreach (var file in Directory.GetFiles(_options.CacheDirectory, "*.json"))
        {
            CacheDocument? doc;
            try
            {
                await using var stream = File.OpenRead(file);
                doc = await JsonSerializer.DeserializeAsync<CacheDocument>(stream, JsonOptions, ct);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Skipping unreadable cache file {File}", file);
                continue;
            }

            if (doc is null || string.IsNullOrWhiteSpace(doc.Symbol) || doc.Bars.Count == 0)
                continue;

            var symbol = doc.Symbol.ToUpperInvariant();
            if (await _db.Bars.AnyAsync(bar => bar.Symbol == symbol, ct))
                continue;

            var bars = doc.Bars.Select(ToCacheBar).Where(bar => bar is not null).Select(bar => bar!.Value).ToList();
            if (bars.Count == 0)
                continue;

            await EnsureTickerAsync(symbol, ct);
            await SaveBarsAsync(symbol, bars, ct);
        }
    }

    public async Task<IReadOnlyList<TickerSummary>> ListAsync(CancellationToken ct)
    {
        var symbols = Universe.Select(ticker => ticker.Symbol).ToArray();
        var rows = await _db.Bars.AsNoTracking()
            .Where(bar => symbols.Contains(bar.Symbol))
            .OrderBy(bar => bar.Date)
            .ToListAsync(ct);
        var names = await _db.Tickers.AsNoTracking().ToDictionaryAsync(ticker => ticker.Symbol, ct);

        return symbols.Select(symbol =>
        {
            var series = rows.Where(bar => bar.Symbol == symbol).ToList();
            var meta = names.GetValueOrDefault(symbol);
            var known = Universe.First(ticker => ticker.Symbol == symbol);
            if (series.Count == 0)
            {
                return new TickerSummary(symbol, meta?.Name ?? known.Name, meta?.Kind ?? known.Kind, "", 0, 0);
            }

            var last = series[^1];
            var prev = series.Count > 1 ? series[^2].Close : last.Close;
            var change = prev == 0 ? 0 : (last.Close / prev - 1) * 100;
            return new TickerSummary(
                symbol,
                meta?.Name ?? known.Name,
                meta?.Kind ?? known.Kind,
                last.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                last.Close,
                change);
        }).ToList();
    }

    public async Task<Dictionary<string, List<Bar>>> LoadAllAsync(CancellationToken ct)
    {
        var rows = await _db.Bars.AsNoTracking()
            .OrderBy(bar => bar.Symbol)
            .ThenBy(bar => bar.Date)
            .ToListAsync(ct);
        return rows
            .GroupBy(bar => bar.Symbol)
            .ToDictionary(
                group => group.Key,
                group => group.Select(bar => new Bar(bar.Date, bar.Open, bar.High, bar.Low, bar.Close, bar.Volume)).ToList());
    }

    public async Task<LoadedBars> LoadForScoreAsync(string rawSymbol, CancellationToken ct)
    {
        var symbol = (rawSymbol ?? "").Trim().ToUpperInvariant();
        if (!SymbolPattern.IsMatch(symbol))
            throw new SetupException(400, "Enter a ticker using letters, numbers, dots, or dashes.");

        if (_options.AllowLiveFetch)
        {
            try
            {
                var live = await FetchYahooAsync(symbol, ct);
                if (live.Count >= 260)
                {
                    await EnsureTickerAsync(symbol, ct);
                    await SaveBarsAsync(symbol, live, ct);
                    return new LoadedBars(symbol, live, "live", "Bars from Yahoo Finance, adjusted for splits, and cached on the server.");
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or JsonException)
            {
                _logger.LogInformation(ex, "Yahoo fetch failed for {Symbol}", symbol);
            }
        }

        var cached = await ReadBarsAsync(symbol, ct);
        if (cached.Count >= 260)
        {
            var note = _options.AllowLiveFetch
                ? "Live data was unavailable. Showing cached daily bars."
                : "Showing cached daily bars shipped with the app.";
            return new LoadedBars(symbol, cached, "cached", note);
        }

        if (_options.AllowLiveFetch)
        {
            try
            {
                var stooq = await FetchStooqAsync(symbol, ct);
                if (stooq.Count >= 260)
                {
                    await EnsureTickerAsync(symbol, ct);
                    await SaveBarsAsync(symbol, stooq, ct);
                    return new LoadedBars(symbol, stooq, "live", "Bars from Stooq. They are not split adjusted.");
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                _logger.LogInformation(ex, "Stooq fetch failed for {Symbol}", symbol);
            }
        }

        var names = string.Join(", ", Universe.Select(ticker => ticker.Symbol));
        throw new SetupException(404, $"No daily bars for {symbol}. Cached tickers: {names}.");
    }

    public async Task<string> NameAsync(string symbol, CancellationToken ct)
    {
        var row = await _db.Tickers.AsNoTracking().FirstOrDefaultAsync(ticker => ticker.Symbol == symbol, ct);
        if (!string.IsNullOrWhiteSpace(row?.Name))
            return row.Name;
        var known = Universe.FirstOrDefault(ticker => ticker.Symbol == symbol);
        return string.IsNullOrEmpty(known.Name) ? symbol : known.Name;
    }

    private async Task<List<Bar>> ReadBarsAsync(string symbol, CancellationToken ct)
    {
        var rows = await _db.Bars.AsNoTracking()
            .Where(bar => bar.Symbol == symbol)
            .OrderBy(bar => bar.Date)
            .ToListAsync(ct);
        return rows.Select(bar => new Bar(bar.Date, bar.Open, bar.High, bar.Low, bar.Close, bar.Volume)).ToList();
    }

    private async Task EnsureTickerAsync(string symbol, CancellationToken ct)
    {
        if (await _db.Tickers.AnyAsync(ticker => ticker.Symbol == symbol, ct))
            return;
        var known = Universe.FirstOrDefault(ticker => ticker.Symbol == symbol);
        _db.Tickers.Add(new TickerRow
        {
            Symbol = symbol,
            Name = string.IsNullOrEmpty(known.Name) ? symbol : known.Name,
            Kind = string.IsNullOrEmpty(known.Kind) ? "Equity" : known.Kind,
        });
    }

    private async Task SaveBarsAsync(string symbol, IReadOnlyList<Bar> bars, CancellationToken ct)
    {
        await _db.Bars.Where(bar => bar.Symbol == symbol).ExecuteDeleteAsync(ct);
        _db.Bars.AddRange(bars.Select(bar => new BarRow
        {
            Symbol = symbol,
            Date = bar.Date,
            Open = bar.Open,
            High = bar.High,
            Low = bar.Low,
            Close = bar.Close,
            Volume = bar.Volume,
        }));
        await _db.SaveChangesAsync(ct);
    }

    private async Task<List<Bar>> FetchYahooAsync(string symbol, CancellationToken ct)
    {
        var baseUrl = _options.YahooBaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/{Uri.EscapeDataString(symbol)}?range=5y&interval=1d&includeAdjustedClose=true";
        using var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var chart = doc.RootElement.GetProperty("chart");
        if (chart.TryGetProperty("error", out var error) && error.ValueKind is not JsonValueKind.Null)
            throw new InvalidOperationException("Yahoo returned an error.");
        var result = chart.GetProperty("result")[0];
        if (!result.TryGetProperty("timestamp", out var timestamps))
            return [];

        var quote = result.GetProperty("indicators").GetProperty("quote")[0];
        var opens = quote.GetProperty("open");
        var highs = quote.GetProperty("high");
        var lows = quote.GetProperty("low");
        var closes = quote.GetProperty("close");
        var volumes = quote.GetProperty("volume");
        JsonElement? adjusted = null;
        if (result.GetProperty("indicators").TryGetProperty("adjclose", out var adjNode)
            && adjNode.GetArrayLength() > 0
            && adjNode[0].TryGetProperty("adjclose", out var adjValues))
        {
            adjusted = adjValues;
        }

        var byDate = new Dictionary<DateOnly, Bar>();
        var count = timestamps.GetArrayLength();
        for (var i = 0; i < count; i++)
        {
            if (timestamps[i].ValueKind != JsonValueKind.Number)
                continue;
            var open = ReadNumber(opens, i);
            var high = ReadNumber(highs, i);
            var low = ReadNumber(lows, i);
            var close = ReadNumber(closes, i);
            if (open is null || high is null || low is null || close is null || close <= 0)
                continue;
            var adj = adjusted is null ? close.Value : ReadNumber(adjusted.Value, i) ?? close.Value;
            var factor = adj / close.Value;
            var volume = ReadNumber(volumes, i) ?? 0;
            var instant = DateTimeOffset.FromUnixTimeSeconds(timestamps[i].GetInt64());
            var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, _newYork).Date);
            byDate[date] = new Bar(
                date,
                Math.Round(open.Value * factor, 4),
                Math.Round(high.Value * factor, 4),
                Math.Round(low.Value * factor, 4),
                Math.Round(adj, 4),
                (long)Math.Round(volume));
        }

        return byDate.OrderBy(pair => pair.Key).Select(pair => pair.Value).ToList();
    }

    private async Task<List<Bar>> FetchStooqAsync(string symbol, CancellationToken ct)
    {
        var stooqSymbol = symbol.Replace('.', '-').ToLowerInvariant() + ".us";
        var baseUrl = _options.StooqBaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/?s={Uri.EscapeDataString(stooqSymbol)}&i=d";
        var csv = await _http.GetStringAsync(url, ct);
        if (csv.TrimStart().StartsWith('<'))
            throw new InvalidOperationException("Stooq did not return CSV.");

        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-5);
        var byDate = new Dictionary<DateOnly, Bar>();
        foreach (var line in csv.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Skip(1))
        {
            var parts = line.Split(',');
            if (parts.Length < 5)
                continue;
            if (!DateOnly.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                continue;
            if (date < cutoff)
                continue;
            if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var open)
                || !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var high)
                || !double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var low)
                || !double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var close)
                || close <= 0 || high < low)
            {
                continue;
            }

            long volume = 0;
            if (parts.Length > 5)
                long.TryParse(parts[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out volume);
            byDate[date] = new Bar(date, open, high, low, close, volume);
        }

        return byDate.OrderBy(pair => pair.Key).Select(pair => pair.Value).ToList();
    }

    private static double? ReadNumber(JsonElement array, int index)
    {
        if (index < 0 || index >= array.GetArrayLength())
            return null;
        var value = array[index];
        return value.ValueKind == JsonValueKind.Number ? value.GetDouble() : null;
    }

    private static Bar? ToCacheBar(CacheBar bar)
    {
        if (!DateOnly.TryParse(bar.D, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return null;
        if (bar.C <= 0 || bar.H < bar.L)
            return null;
        return new Bar(date, bar.O, bar.H, bar.L, bar.C, bar.V);
    }

    private sealed class CacheDocument
    {
        public string Symbol { get; set; } = "";
        public List<CacheBar> Bars { get; set; } = [];
    }

    private sealed class CacheBar
    {
        [JsonPropertyName("d")]
        public string D { get; set; } = "";
        [JsonPropertyName("o")]
        public double O { get; set; }
        [JsonPropertyName("h")]
        public double H { get; set; }
        [JsonPropertyName("l")]
        public double L { get; set; }
        [JsonPropertyName("c")]
        public double C { get; set; }
        [JsonPropertyName("v")]
        public long V { get; set; }
    }
}

public sealed record LoadedBars(string Symbol, IReadOnlyList<Bar> Bars, string Source, string Note);
