using ProductionReporting;
using System.Text;

namespace ProductionReporting.Core.Tests;

public sealed class ExportTests
{
    private static readonly ProductionReading Reading = new()
    {
        Date = new DateTime(2026, 8, 8), TimeSlot = "19:00 - 20:00",
        FeedKg = 15.8, FeedSeconds = 5.27, Boom1Kg = 10.6, Boom1Seconds = 10.1,
        Boom2Kg = 5, Boom2Seconds = 10.8
    };

    [Fact]
    public void DetailedAndMonthlyCsvContainDatesAndSummary()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ProductionReportingTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var detailed = Path.Combine(root, "detailed.csv");
            var monthly = Path.Combine(root, "monthly.csv");
            var readings = new[] { Reading };
            var daily = ReportCalculator.BuildDailySummaries(readings);
            ReportExporter.WriteCsv(detailed, readings);
            ReportExporter.WriteMonthlyCsv(monthly, "Production Site", new DateTime(2026, 8, 1), readings, daily);

            Assert.Contains("\"2026-08-08\"", File.ReadAllText(detailed));
            var monthlyText = File.ReadAllText(monthly);
            Assert.Contains("Monthly Production Report - August 2026", monthlyText);
            Assert.Contains("Daily Summary", monthlyText);
            Assert.Contains("\"2026-08-08\"", monthlyText);
            Assert.Contains("Average yield", monthlyText);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void PdfContainsTableAndNoFormulaFooter()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ProductionReporting-{Guid.NewGuid():N}.pdf");
        try
        {
            var readings = new[] { Reading };
            ReportExporter.WritePdf(path, "Production Site", new DateTime(2026, 8, 1), readings, ReportCalculator.BuildDailySummaries(readings));
            var pdfText = Encoding.ASCII.GetString(File.ReadAllBytes(path));
            Assert.Contains("Daily Summary", pdfText);
            Assert.Contains("Avg input MT/h", pdfText);
            Assert.DoesNotContain("Hourly input = Feed kg", pdfText);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
