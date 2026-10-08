using JevSetupScore.Api.Contracts;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace JevSetupScore.Api.Services;

public sealed class PdfReportService
{
    static PdfReportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Render(ScoreResponse score)
    {
        var ink = Color.FromHex("#0B1220");
        var muted = Color.FromHex("#526072");
        var teal = Color.FromHex("#0F8F74");

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(style => style.FontSize(10).FontColor(ink));

                page.Header().Column(column =>
                {
                    column.Item().Text("Jev Setup Score").FontSize(22).SemiBold().FontColor(ink);
                    column.Item().PaddingTop(2).Text("by Ulric studio").FontSize(9).FontColor(muted);
                    column.Item().PaddingTop(8).LineHorizontal(1).LineColor(Color.FromHex("#D5DDE6"));
                });

                page.Content().PaddingVertical(16).Column(column =>
                {
                    column.Spacing(10);
                    column.Item().Text($"{score.Ticker}  {score.Name}").FontSize(16).SemiBold();
                    column.Item().Text($"As of {score.AsOf}. Horizon {score.Horizon} sessions. {DescribeSource(score.DataSource)}.").FontColor(muted);
                    column.Item().PaddingTop(6).Text(score.Score.ToString()).FontSize(42).SemiBold().FontColor(teal);
                    column.Item().Text("Setup score, 0 to 100").FontColor(muted);
                    column.Item().PaddingTop(4).Text($"Up-move likelihood {(score.Probability * 100):0.0} percent.").FontSize(12);
                    column.Item().Text(score.Summary);
                    column.Item().PaddingTop(4).Text("Educational tool. Not financial advice.").Italic().FontColor(muted);

                    column.Item().PaddingTop(8).Text("Factor contributions").FontSize(14).SemiBold();
                    column.Item().Text("Values are log-odds added by each standardized input. Positive values raise the chance of a higher close.").FontColor(muted);
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(5);
                        });
                        table.Header(header =>
                        {
                            header.Cell().PaddingVertical(4).Text("Factor").SemiBold();
                            header.Cell().PaddingVertical(4).Text("Reading").SemiBold();
                            header.Cell().PaddingVertical(4).AlignRight().Text("Contribution").SemiBold();
                            header.Cell().PaddingVertical(4).Text("Plain read").SemiBold();
                        });
                        foreach (var factor in score.Factors)
                        {
                            table.Cell().PaddingVertical(3).Text(factor.Label);
                            table.Cell().PaddingVertical(3).Text(factor.Display);
                            table.Cell().PaddingVertical(3).AlignRight().Text(factor.Contribution.ToString("+0.00;-0.00;0.00"));
                            table.Cell().PaddingVertical(3).Text(factor.Reading);
                        }
                    });

                    column.Item().PaddingTop(8).Text("Walk-forward test").FontSize(14).SemiBold();
                    column.Item().Text(score.Backtest.Honesty);
                    column.Item().Text($"Hit rate {score.Backtest.HitRate * 100:0.0} percent. Base rate {score.Backtest.BaseRate * 100:0.0} percent. Brier {score.Backtest.Brier:0.000}. Base-rate Brier {score.Backtest.BaseRateBrier:0.000}.");
                    column.Item().Text($"Folds {score.Backtest.FoldCount}. Out-of-sample rows {score.Backtest.PredictionCount}. Trades when score is above {score.Threshold}: {score.Backtest.TradeCount}.");
                    column.Item().Text($"Strategy multiple {score.Backtest.StrategyMultiple:0.00}x. Buy and hold multiple {score.Backtest.BuyHoldMultiple:0.00}x. Same non-overlapping windows.");

                    column.Item().PaddingTop(8).Text("Coefficients").FontSize(14).SemiBold();
                    column.Item().Text("Coefficients are on standardized inputs from the model trained through the latest labeled day.").FontColor(muted);
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(5);
                        });
                        foreach (var coefficient in score.Coefficients)
                        {
                            table.Cell().PaddingVertical(2).Text(coefficient.Label);
                            table.Cell().PaddingVertical(2).Text(coefficient.Weight.ToString("+0.000;-0.000;0.000"));
                            table.Cell().PaddingVertical(2).Text(coefficient.Meaning);
                        }
                    });
                });

                page.Footer().Column(column =>
                {
                    column.Item().LineHorizontal(1).LineColor(Color.FromHex("#D5DDE6"));
                    column.Item().PaddingTop(6).Text("Educational tool. Not financial advice. by Ulric studio").FontSize(9).FontColor(muted);
                });
            });
        }).GeneratePdf();
    }

    private static string DescribeSource(string source) =>
        source == "live" ? "Live data" : "Cached data";
}
