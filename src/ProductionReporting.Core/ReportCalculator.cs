namespace ProductionReporting;

public static class ReportCalculator
{
    public static ReportOverview CalculateOverview(IReadOnlyCollection<ProductionReading> readings)
    {
        var input = readings.Count > 0 ? readings.Average(x => x.HourlyInputMtPerHour) : 0;
        var production = readings.Count > 0 ? readings.Average(x => x.HourlyProductionMtPerHour) : 0;
        var yield = input > 0 ? production / input : 0;
        return new ReportOverview(input, production, yield, production * 27d * 22d, readings.Count);
    }

    public static List<DailySummary> BuildDailySummaries(IEnumerable<ProductionReading> readings) =>
        readings.GroupBy(x => x.Date.Date).OrderBy(x => x.Key).Select(group => new DailySummary
        {
            Date = group.Key,
            Readings = group.Count(),
            AverageHourlyInputMt = group.Average(x => x.HourlyInputMtPerHour),
            AverageHourlyProductionMt = group.Average(x => x.HourlyProductionMtPerHour)
        }).ToList();
}
