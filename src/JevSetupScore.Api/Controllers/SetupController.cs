using JevSetupScore.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace JevSetupScore.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class SetupController(MarketDataService market, ScoreService scores, PdfReportService pdf) : ControllerBase
{
    [HttpGet("health")]
    public IActionResult Health() => Ok(new { status = "ok" });

    [HttpGet("tickers")]
    public async Task<IActionResult> Tickers(CancellationToken ct)
    {
        try
        {
            return Ok(await market.ListAsync(ct));
        }
        catch (SetupException ex)
        {
            return StatusCode(ex.Status, new { message = ex.Message });
        }
    }

    [HttpGet("examples")]
    public async Task<IActionResult> Examples(CancellationToken ct)
    {
        try
        {
            return Ok(await scores.ExamplesAsync(ct));
        }
        catch (SetupException ex)
        {
            return StatusCode(ex.Status, new { message = ex.Message });
        }
    }

    [HttpGet("history")]
    public async Task<IActionResult> History([FromQuery] int limit = 12, CancellationToken ct = default)
    {
        try
        {
            return Ok(await scores.HistoryAsync(limit, ct));
        }
        catch (SetupException ex)
        {
            return StatusCode(ex.Status, new { message = ex.Message });
        }
    }

    [HttpGet("score/{ticker}")]
    public async Task<IActionResult> Score(string ticker, [FromQuery] int horizon = 10, [FromQuery] int threshold = 60, CancellationToken ct = default)
    {
        try
        {
            return Ok(await scores.ScoreAsync(ticker, horizon, threshold, ct));
        }
        catch (SetupException ex)
        {
            return StatusCode(ex.Status, new { message = ex.Message });
        }
    }

    [HttpGet("score/{ticker}/report.pdf")]
    public async Task<IActionResult> Report(string ticker, [FromQuery] int horizon = 10, [FromQuery] int threshold = 60, CancellationToken ct = default)
    {
        try
        {
            var score = await scores.ScoreAsync(ticker, horizon, threshold, ct);
            var bytes = pdf.Render(score);
            return File(bytes, "application/pdf", $"jev-setup-score-{score.Ticker}-{score.Horizon}.pdf");
        }
        catch (SetupException ex)
        {
            return StatusCode(ex.Status, new { message = ex.Message });
        }
    }
}
