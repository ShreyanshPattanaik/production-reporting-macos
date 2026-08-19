using ProductionReporting;
using System.Globalization;
using System.Text;

namespace ProductionReporting.Core.Tests;

public sealed class ExportTests
{
    private static readonly ProductionReading Reading = new()
    {
        Date = new DateTime(2026, 8, 8), TimeSlot = "19:00 - 20:00",
        FeedKg = 15.8, FeedSeconds = 5.27, Boom1Kg = 10.6, Boom1Seconds = 10.1,
        Boom2Kg = 5, Boom2Seconds = 10.8, RunningMinutes = 45
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
            Assert.Contains("Production for the day MT", monthlyText);
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
            Assert.Contains("Production for day MT", pdfText);
            Assert.DoesNotContain("Hourly input = Feed kg", pdfText);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void PdfUsesAsciiSafeInvariantFormattingRegardlessOfCurrentCulture()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ProductionReporting-{Guid.NewGuid():N}.pdf");
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
            var hourlyProduction = 3390.693d / (27d * 22d);
            var reading = new ProductionReading
            {
                Date = new DateTime(2026, 8, 8),
                FeedKg = 10.5d * 1000d / 3600d,
                FeedSeconds = 1,
                Boom1Kg = hourlyProduction * 1000d / 3600d,
                Boom1Seconds = 1
            };
            var readings = Enumerable.Repeat(reading, 1234).ToList();
            var daily = new[]
            {
                new DailySummary
                {
                    Date = reading.Date,
                    Readings = 1234,
                    AverageHourlyInputMt = reading.HourlyInputMtPerHour,
                    AverageHourlyProductionMt = reading.HourlyProductionMtPerHour,
                    RunningMinutes = 60
                }
            };

            ReportExporter.WritePdf(path, "Production Site", new DateTime(2026, 8, 1), readings, daily);
            var pdfText = Encoding.ASCII.GetString(File.ReadAllBytes(path));

            Assert.Contains("Estimated monthly production: 3,390.693 MT", pdfText);
            Assert.Contains("Operating readings: 1,234", pdfText);
            Assert.Contains("Monthly Production Report - August 2026", pdfText);
            Assert.Contains("08 Aug 2026", pdfText);
            Assert.Contains("1,234", pdfText);
            Assert.DoesNotContain("3?390.693", pdfText);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(true, "Hourly Summary", "Date,Time Slot,Feed KG,Feed Seconds")]
    [InlineData(false, "Daily Summary", "Date,Readings,Average hourly input MT/h")]
    public void CustomCsvContainsRequestedSummaryAndTotalsWithoutMonthlyEstimate(bool hourly, string section, string tableHeader)
    {
        var path = Path.Combine(Path.GetTempPath(), $"ProductionReporting-{Guid.NewGuid():N}.csv");
        try
        {
            var readings = new[] { Reading };
            var daily = ReportCalculator.BuildDailySummaries(readings);
            var title = hourly
                ? "Custom Production Report from 08 Aug 2026 19:00 - 20:00 to 08 Aug 2026 19:00 - 20:00"
                : "Custom Production Report from 08 Aug 2026 to 08 Aug 2026";

            ReportExporter.WriteCustomCsv(path, "Production Site", title, readings, daily, hourly);
            var text = File.ReadAllText(path);

            Assert.Contains(title, text);
            Assert.Contains(section, text);
            Assert.Contains(tableHeader, text);
            Assert.Contains("Total Actual Input MT,\"8.095 MT\"", text);
            Assert.Contains("Total Actual Production MT,\"4.084 MT\"", text);
            Assert.Contains("Hours run,\"0.75 h\"", text);
            Assert.DoesNotContain("Estimated monthly production", text, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(true, "Hourly Summary")]
    [InlineData(false, "Daily Summary")]
    public void CustomPdfContainsRequestedSummaryAndTotalsWithoutMonthlyEstimate(bool hourly, string section)
    {
        var path = Path.Combine(Path.GetTempPath(), $"ProductionReporting-{Guid.NewGuid():N}.pdf");
        try
        {
            var readings = new[] { Reading };
            var daily = ReportCalculator.BuildDailySummaries(readings);
            var title = hourly
                ? "Custom Production Report from 08 Aug 2026 19:00 - 20:00 to 08 Aug 2026 19:00 - 20:00"
                : "Custom Production Report from 08 Aug 2026 to 08 Aug 2026";

            ReportExporter.WriteCustomPdf(path, "Production Site", title, readings, daily, hourly);
            var text = Encoding.ASCII.GetString(File.ReadAllBytes(path));

            Assert.Contains(title, text);
            Assert.Contains(section, text);
            Assert.Contains("Average hourly input", text);
            Assert.Contains("10.793 MT/h", text);
            Assert.Contains("Average hourly production", text);
            Assert.Contains("5.445 MT/h", text);
            Assert.Contains("Average yield", text);
            Assert.Contains("50.45 %", text);
            Assert.Contains("Total Actual Input MT", text);
            Assert.Contains("8.095 MT", text);
            Assert.Contains("Total Actual Production MT", text);
            Assert.Contains("4.084 MT", text);
            Assert.Contains("Hours run", text);
            Assert.Contains("0.750 h", text);
            Assert.DoesNotContain("Estimated monthly production", text, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void DetailedCsvUsesDashesForMissingActualValuesAndRecovery()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ProductionReporting-{Guid.NewGuid():N}.csv");
        try
        {
            var reading = new ProductionReading
            {
                Date = new DateTime(2026, 8, 8),
                TimeSlot = "19:00 - 20:00",
                FeedKg = 15,
                Boom1Kg = 5,
                Boom2Kg = 5
            };

            ReportExporter.WriteCsv(path, [reading]);
            var text = File.ReadAllText(path);

            Assert.Contains("Hourly Production MT,Actual Input MT,Actual Production MT,Recovery %", text);
            Assert.Contains(",-,-,-,", text);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
