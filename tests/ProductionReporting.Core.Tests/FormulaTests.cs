using ProductionReporting;

namespace ProductionReporting.Core.Tests;

public sealed class FormulaTests
{
    [Fact]
    public void Reading_UsesConfirmedHourlyFormulas()
    {
        var reading = new ProductionReading
        {
            FeedKg = 15.8,
            FeedSeconds = 5.27,
            Boom1Kg = 10.6,
            Boom1Seconds = 10.1,
            Boom2Kg = 5,
            Boom2Seconds = 10.8
        };

        Assert.Equal(10.7931688805, reading.HourlyInputMtPerHour, 8);
        Assert.Equal(5.4448844884, reading.HourlyProductionMtPerHour, 8);
        Assert.Equal(50.447505721, reading.RecoveryPercent, 8);
    }

    [Fact]
    public void Overview_UsesAveragesAndMonthlyProjection()
    {
        var readings = new[]
        {
            new ProductionReading { FeedKg = 10, FeedSeconds = 5, Boom1Kg = 5, Boom1Seconds = 10, Boom2Kg = 5, Boom2Seconds = 10 },
            new ProductionReading { FeedKg = 20, FeedSeconds = 5, Boom1Kg = 10, Boom1Seconds = 10, Boom2Kg = 10, Boom2Seconds = 10 }
        };

        var overview = ReportCalculator.CalculateOverview(readings);

        Assert.Equal(10.8, overview.AverageHourlyInputMt, 8);
        Assert.Equal(5.4, overview.AverageHourlyProductionMt, 8);
        Assert.Equal(0.5, overview.AverageYield, 8);
        Assert.Equal(3207.6, overview.EstimatedMonthlyProductionMt, 8);
        Assert.Equal(2, overview.ReadingCount);
    }

    [Fact]
    public void DailySummary_UsesSummedRunningMinutesAndDailyAverageProduction()
    {
        var date = new DateTime(2026, 8, 8);
        var readings = new[]
        {
            new ProductionReading { Date = date, RunningMinutes = 60, Boom1Kg = 10, Boom1Seconds = 10 },
            new ProductionReading { Date = date, RunningMinutes = 30, Boom1Kg = 20, Boom1Seconds = 10 }
        };

        var summary = Assert.Single(ReportCalculator.BuildDailySummaries(readings));

        Assert.Equal(90, summary.RunningMinutes);
        Assert.Equal(5.4, summary.AverageHourlyProductionMt, 8);
        Assert.Equal(8.1, summary.ProductionForDayMt!.Value, 8);
        Assert.Equal("8.100", summary.ProductionForDayDisplay);
    }

    [Fact]
    public void DailySummary_WithNoRunningMinutes_IsNotApplicable()
    {
        var summary = Assert.Single(ReportCalculator.BuildDailySummaries([
            new ProductionReading { Date = new DateTime(2026, 8, 8), Boom1Kg = 10, Boom1Seconds = 10 }
        ]));

        Assert.Null(summary.ProductionForDayMt);
        Assert.Equal("Not Applicable", summary.ProductionForDayDisplay);
    }
}
