# Jev Setup Score

![SPY setup score in light mode](docs/screenshots/score-light-desktop.png)

Type a ticker. The app returns a 0 to 100 setup score and the chance the close is higher after 5, 10, or 20 sessions.

Educational tool. Not financial advice.

## Features

- Daily features, plus a weekly resample: 20-day and 50-day moving-average slopes, 10-week slope, alignment of the 20, 50, and 200 day averages, RSI(14), MACD(12, 26, 9) histogram and its slope, ATR(14) as a percent of price, volume versus its 20-day average, distance from the 20, 50, and 200 day averages, and the 10-session return.
- A logistic regression in C#. No paid data and no paid model. Training uses the cached ticker set. The label is a forward return above zero. The page shows coefficients and the current contribution of each input.
- The score is that probability times 100. Each factor has a short plain-English read.
- A walk-forward backtest on the page: rolling train and test windows, hit rate, Brier score, a calibration chart, the base rate, and an equity curve for a long-when-score-is-above-threshold rule against buy and hold. The copy says so when the model does not clearly beat the base rate.
- A price chart with moving averages, plus RSI and MACD panels.
- Live daily bars from Yahoo Finance, then Stooq, then the cached files. The page says whether the result used live or cached data.
- Cached bars for SPY, QQQ, AAPL, NVDA, TSLA, and MSFT, so `docker compose up` still scores those tickers offline.
- Light and dark mode. The toggle is remembered. With no saved choice, the app follows the system theme.
- A PDF report of the score.
- A small "by Ulric studio" credit in the footer.

## Stack

- ASP.NET Core 8 Web API
- Angular (standalone components and signals)
- SQLite with EF Core for cached bars
- QuestPDF Community license for the PDF report
- ECharts for the price, oscillator, calibration, and equity charts
- xUnit for indicator math, logistic convergence, and the walk-forward split

## Run with Docker

From the repository root:

```bash
docker compose up --build
```

Open http://localhost:8080.

The image includes the cached bars. If Yahoo or Stooq cannot be reached, scores for the cached tickers still load. Set `Data__AllowLiveFetch=false` in `docker-compose.yml` to skip live calls.

## Run the API and the UI separately

API:

```bash
dotnet run --project src/JevSetupScore.Api
```

The API listens on http://localhost:5080. Swagger is at http://localhost:5080/swagger in Development.

UI:

```bash
cd web
npm install
npm start
```

Open http://localhost:4200. The dev server proxies `/api` to port 5080.

Tests:

```bash
dotnet test
```

## Environment variables

| Variable | Purpose | Default |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Development` or `Production` | `Development` for `dotnet run` |
| `ASPNETCORE_URLS` | Bind address | `http://localhost:5080` in the launch profile, `http://+:8080` in Docker |
| `ConnectionStrings__Default` | SQLite file | `Data Source=jev.db` |
| `Data__CacheDirectory` | Folder of cached JSON bars. Empty uses `data/cache` in the repo, or `/app/data/cache` in Docker | empty |
| `Data__AllowLiveFetch` | Try Yahoo, then Stooq, before cached bars | `true` |
| `Data__YahooBaseUrl` | Yahoo chart endpoint prefix | `https://query1.finance.yahoo.com/v8/finance/chart/` |
| `Data__StooqBaseUrl` | Stooq daily CSV prefix | `https://stooq.com/q/d/l/` |
| `Data__HttpTimeoutSeconds` | Live request timeout | `8` |
| `Cors__Origins__0` | Browser origin allowed to call the API | `http://localhost:4200` |

See `.env.example` and `src/JevSetupScore.Api/appsettings.Development.example.json`. No API keys are required. Do not commit a filled `.env`.

## Architecture

`JevSetupScore.Core` holds the indicators, the feature rows, logistic regression, and the walk-forward split. RSI and ATR use Wilder smoothing. MACD uses EMA seeded with a simple average. A feature at day t uses only bars through t. A training row is eligible only when its forward label bar is strictly before the test window.

The API imports `data/cache` into SQLite on first start, optionally refreshes a ticker from Yahoo or Stooq, trains one logistic model per horizon on every cached name, and runs the walk-forward test. The Angular app renders the score, the factor bars, the charts, and the test. QuestPDF writes the same numbers to a PDF.

## License

MIT. See `LICENSE`. PDF export uses QuestPDF under the QuestPDF Community license.
