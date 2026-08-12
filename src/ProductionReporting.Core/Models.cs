namespace ProductionReporting;

public sealed class ProductionReading
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Date { get; set; } = DateTime.Today;
    public string TimeSlot { get; set; } = "00:00 - 01:00";
    public double FeedKg { get; set; }
    public double FeedSeconds { get; set; }
    public double Boom1Kg { get; set; }
    public double Boom1Seconds { get; set; }
    public double Boom2Kg { get; set; }
    public double Boom2Seconds { get; set; }
    public double RunningMinutes { get; set; }
    public string Notes { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public double ActualInputMt => FeedKg / 1000d;
    public double ActualProductionMt => (Boom1Kg + Boom2Kg) / 1000d;
    public double HourlyInputMtPerHour => FeedSeconds > 0 ? FeedKg / FeedSeconds / 1000d * 3600d : 0;
    public double HourlyProductionMtPerHour =>
        (Boom1Seconds > 0 ? Boom1Kg / Boom1Seconds * 3600d / 1000d : 0) +
        (Boom2Seconds > 0 ? Boom2Kg / Boom2Seconds * 3600d / 1000d : 0);
    public double RecoveryPercent => HourlyInputMtPerHour > 0 ? HourlyProductionMtPerHour / HourlyInputMtPerHour * 100d : 0;
}

public sealed class AppSettings
{
    public string PlantName { get; set; } = "Production Site";
    public double? MonthlyCapacityMt { get; set; }
}

public sealed class AppData
{
    public const int CurrentSchemaVersion = 1;
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public AppSettings Settings { get; set; } = new();
    public List<ProductionReading> Readings { get; set; } = [];
}

public sealed record ReportOverview(
    double AverageHourlyInputMt,
    double AverageHourlyProductionMt,
    double AverageYield,
    double EstimatedMonthlyProductionMt,
    int ReadingCount);

public sealed class DailySummary
{
    public DateTime Date { get; set; }
    public int Readings { get; set; }
    public double AverageHourlyInputMt { get; set; }
    public double AverageHourlyProductionMt { get; set; }
    public double RunningMinutes { get; set; }
    public double AverageYield => AverageHourlyInputMt > 0 ? AverageHourlyProductionMt / AverageHourlyInputMt : 0;
    public double? ProductionForDayMt => RunningMinutes > 0 ? RunningMinutes / 60d * AverageHourlyProductionMt : null;
    public string ProductionForDayDisplay => ProductionForDayMt is { } production ? production.ToString("N3") : "Not Applicable";
}
