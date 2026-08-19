using System.Globalization;
using System.Text;
using System.IO;

namespace ProductionReporting;

public static class ReportExporter
{
    public static void WriteCsv(string path, IEnumerable<ProductionReading> readings)
    {
        var csv = new StringBuilder();
        AppendHourlyCsv(csv, readings);
        File.WriteAllText(path, csv.ToString(), new UTF8Encoding(true));
    }

    public static void WriteMonthlyCsv(string path, string plantName, DateTime month, IReadOnlyList<ProductionReading> readings, IReadOnlyList<DailySummary> daily)
    {
        var input = readings.Count > 0 ? readings.Average(x => x.HourlyInputMtPerHour) : 0;
        var production = readings.Count > 0 ? readings.Average(x => x.HourlyProductionMtPerHour) : 0;
        var yield = input > 0 ? production / input : 0;
        var monthlyEstimate = production * 27d * 22d;
        string N(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
        string Percent(double value) => $"{(value * 100d).ToString("0.00", CultureInfo.InvariantCulture)}%";

        var csv = new StringBuilder();
        csv.AppendLine(Quote(plantName));
        csv.AppendLine(Quote($"Monthly Production Report - {month:MMMM yyyy}"));
        csv.AppendLine();
        csv.AppendLine("Metric,Value");
        csv.AppendLine($"Average hourly input,{Quote($"{N(input)} MT/h")}");
        csv.AppendLine($"Average hourly production,{Quote($"{N(production)} MT/h")}");
        csv.AppendLine($"Average yield,{Quote(Percent(yield))}");
        csv.AppendLine($"Estimated monthly production,{Quote($"{N(monthlyEstimate)} MT")}");
        csv.AppendLine($"Operating readings,{readings.Count.ToString(CultureInfo.InvariantCulture)}");
        csv.AppendLine();
        csv.AppendLine("Daily Summary");
        csv.AppendLine("Date,Readings,Average hourly input MT/h,Average hourly production MT/h,Average yield,Production for the day MT");
        foreach (var d in daily)
        {
            csv.AppendLine(string.Join(',',
                Quote(d.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                d.Readings.ToString(CultureInfo.InvariantCulture),
                N(d.AverageHourlyInputMt), N(d.AverageHourlyProductionMt),
                Quote(Percent(d.AverageYield)),
                d.ProductionForDayMt is { } dayProduction ? N(dayProduction) : Quote("Not Applicable")));
        }
        File.WriteAllText(path, csv.ToString(), new UTF8Encoding(true));
    }

    public static void WriteCustomCsv(string path, string plantName, string reportTitle, IReadOnlyList<ProductionReading> readings, IReadOnlyList<DailySummary> daily, bool hourly)
    {
        var (input, production, yield) = CalculateAverages(readings);
        var csv = new StringBuilder();
        csv.AppendLine(Quote(plantName));
        csv.AppendLine(Quote(reportTitle));
        csv.AppendLine();
        csv.AppendLine("Metric,Value");
        csv.AppendLine($"Average hourly input,{Quote($"{CsvNumber(input)} MT/h")}");
        csv.AppendLine($"Average hourly production,{Quote($"{CsvNumber(production)} MT/h")}");
        csv.AppendLine($"Average yield,{Quote(CsvPercent(yield))}");
        csv.AppendLine($"Operating readings,{readings.Count.ToString(CultureInfo.InvariantCulture)}");
        csv.AppendLine($"Total feed input,{Quote($"{CsvNumber(readings.Sum(x => x.FeedKg) / 1000d)} MT")}");
        csv.AppendLine($"Total Boom 1,{Quote($"{CsvNumber(readings.Sum(x => x.Boom1Kg) / 1000d)} MT")}");
        csv.AppendLine($"Total Boom 2,{Quote($"{CsvNumber(readings.Sum(x => x.Boom2Kg) / 1000d)} MT")}");
        csv.AppendLine($"Hours run,{Quote($"{CsvNumber(readings.Sum(x => x.RunningMinutes) / 60d)} h")}");
        csv.AppendLine();
        csv.AppendLine(hourly ? "Hourly Summary" : "Daily Summary");
        if (hourly)
            AppendHourlyCsv(csv, readings);
        else
            AppendDailyCsv(csv, daily);
        File.WriteAllText(path, csv.ToString(), new UTF8Encoding(true));
    }

    private static void AppendHourlyCsv(StringBuilder csv, IEnumerable<ProductionReading> readings)
    {
        csv.AppendLine("Date,Time Slot,Feed KG,Feed Seconds,Boom 1 KG,Boom 1 Seconds,Boom 2 KG,Boom 2 Seconds,Running Minutes,Hourly Input MT,Hourly Production MT,Recovery %,Notes");
        foreach (var r in readings)
            csv.AppendLine(string.Join(',', Quote(r.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), Quote(r.TimeSlot), CsvNumber(r.FeedKg), CsvNumber(r.FeedSeconds), CsvNumber(r.Boom1Kg), CsvNumber(r.Boom1Seconds), CsvNumber(r.Boom2Kg), CsvNumber(r.Boom2Seconds), CsvNumber(r.RunningMinutes), CsvNumber(r.HourlyInputMtPerHour), CsvNumber(r.HourlyProductionMtPerHour), CsvNumber(r.RecoveryPercent), Quote(r.Notes)));
    }

    private static void AppendDailyCsv(StringBuilder csv, IEnumerable<DailySummary> daily)
    {
        csv.AppendLine("Date,Readings,Average hourly input MT/h,Average hourly production MT/h,Average yield,Production for the day MT");
        foreach (var d in daily)
            csv.AppendLine(string.Join(',', Quote(d.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), d.Readings.ToString(CultureInfo.InvariantCulture), CsvNumber(d.AverageHourlyInputMt), CsvNumber(d.AverageHourlyProductionMt), Quote(CsvPercent(d.AverageYield)), d.ProductionForDayMt is { } production ? CsvNumber(production) : Quote("Not Applicable")));
    }

    private static (double Input, double Production, double Yield) CalculateAverages(IReadOnlyList<ProductionReading> readings)
    {
        var input = readings.Count > 0 ? readings.Average(x => x.HourlyInputMtPerHour) : 0;
        var production = readings.Count > 0 ? readings.Average(x => x.HourlyProductionMtPerHour) : 0;
        return (input, production, input > 0 ? production / input : 0);
    }

    private static string CsvNumber(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static string CsvPercent(double value) => $"{(value * 100d).ToString("0.00", CultureInfo.InvariantCulture)}%";

    private static string Quote(string value) => $"\"{(value ?? "").Replace("\"", "\"\"")}\"";

    public static void WritePdf(string path, string plantName, DateTime month, IReadOnlyList<ProductionReading> readings, IReadOnlyList<DailySummary> daily)
    {
        var input = readings.Count > 0 ? readings.Average(x => x.HourlyInputMtPerHour) : 0;
        var production = readings.Count > 0 ? readings.Average(x => x.HourlyProductionMtPerHour) : 0;
        var yield = input > 0 ? production / input : 0;
        var monthlyEstimate = production * 27d * 22d;
        SimplePdf.WriteReport(path, plantName, month, input, production, yield, monthlyEstimate, readings.Count, daily);
    }

    public static void WriteCustomPdf(string path, string plantName, string reportTitle, IReadOnlyList<ProductionReading> readings, IReadOnlyList<DailySummary> daily, bool hourly)
    {
        var (input, production, yield) = CalculateAverages(readings);
        SimplePdf.WriteCustomReport(path, plantName, reportTitle, input, production, yield, readings, daily, hourly);
    }

    private static class SimplePdf
    {
        private const double PageWidth = 842;
        private const double PageHeight = 595;
        private const double TableLeft = 36;
        private const double RowHeight = 19;
        private static readonly double[] ColumnWidths = [100, 70, 125, 155, 90, 230];

        public static void WriteReport(string path, string plantName, DateTime month, double input, double production, double yield, double monthlyEstimate, int readingCount, IReadOnlyList<DailySummary> daily)
        {
            var pageRows = new List<IReadOnlyList<DailySummary>>();
            var remaining = daily.ToList();
            pageRows.Add(remaining.Take(17).ToList());
            remaining = remaining.Skip(17).ToList();
            while (remaining.Count > 0)
            {
                pageRows.Add(remaining.Take(25).ToList());
                remaining = remaining.Skip(25).ToList();
            }

            var streams = new List<string>();
            for (var pageIndex = 0; pageIndex < pageRows.Count; pageIndex++)
            {
                var content = new StringBuilder();
                if (pageIndex == 0)
                {
                    AddText(content, plantName, 36, 550, 18, true);
                    AddText(content, $"Monthly Production Report - {ReportMonth(month)}", 36, 525, 13, true);
                    AddText(content, $"Average hourly input: {Number(input)} MT/h", 36, 488, 10);
                    AddText(content, $"Average hourly production: {Number(production)} MT/h", 285, 488, 10);
                    AddText(content, $"Average yield: {Percent(yield)}", 575, 488, 10);
                    AddText(content, $"Estimated monthly production: {Number(monthlyEstimate)} MT", 36, 466, 10);
                    AddText(content, $"Operating readings: {Count(readingCount)}", 365, 466, 10);
                    AddText(content, "Daily Summary", 36, 427, 13, true);
                    AddTable(content, pageRows[pageIndex], 410);
                }
                else
                {
                    AddText(content, $"{plantName} - Daily Summary (continued)", 36, 550, 14, true);
                    AddText(content, ReportMonth(month), 36, 529, 10);
                    AddTable(content, pageRows[pageIndex], 505);
                }
                streams.Add(content.ToString());
            }

            WriteStreams(path, streams);
        }

        public static void WriteCustomReport(string path, string plantName, string reportTitle, double input, double production, double yield, IReadOnlyList<ProductionReading> readings, IReadOnlyList<DailySummary> daily, bool hourly)
        {
            var streams = hourly
                ? BuildCustomHourlyPages(plantName, reportTitle, input, production, yield, readings)
                : BuildCustomDailyPages(plantName, reportTitle, input, production, yield, readings, daily);
            WriteStreams(path, streams);
        }

        private static List<string> BuildCustomHourlyPages(string plantName, string reportTitle, double input, double production, double yield, IReadOnlyList<ProductionReading> readings)
        {
            var pages = Paginate(readings, 17, 24);
            var streams = new List<string>();
            for (var pageIndex = 0; pageIndex < pages.Count; pageIndex++)
            {
                var content = new StringBuilder();
                if (pageIndex == 0)
                {
                    AddCustomHeader(content, plantName, reportTitle, input, production, yield, readings);
                    AddText(content, "Hourly Summary", 36, 414, 13, true);
                    AddHourlyTable(content, pages[pageIndex], 397);
                }
                else
                {
                    AddText(content, $"{plantName} - Hourly Summary (continued)", 36, 550, 14, true);
                    AddText(content, reportTitle, 36, 529, 9);
                    AddHourlyTable(content, pages[pageIndex], 505);
                }
                streams.Add(content.ToString());
            }
            return streams;
        }

        private static List<string> BuildCustomDailyPages(string plantName, string reportTitle, double input, double production, double yield, IReadOnlyList<ProductionReading> readings, IReadOnlyList<DailySummary> daily)
        {
            var pages = Paginate(daily, 17, 24);
            var streams = new List<string>();
            for (var pageIndex = 0; pageIndex < pages.Count; pageIndex++)
            {
                var content = new StringBuilder();
                if (pageIndex == 0)
                {
                    AddCustomHeader(content, plantName, reportTitle, input, production, yield, readings);
                    AddText(content, "Daily Summary", 36, 414, 13, true);
                    AddTable(content, pages[pageIndex], 397);
                }
                else
                {
                    AddText(content, $"{plantName} - Daily Summary (continued)", 36, 550, 14, true);
                    AddText(content, reportTitle, 36, 529, 9);
                    AddTable(content, pages[pageIndex], 505);
                }
                streams.Add(content.ToString());
            }
            return streams;
        }

        private static void AddCustomHeader(StringBuilder content, string plantName, string reportTitle, double input, double production, double yield, IReadOnlyList<ProductionReading> readings)
        {
            AddText(content, plantName, 36, 550, 18, true);
            AddText(content, reportTitle, 36, 525, 9, true);
            AddText(content, $"Average hourly input: {Number(input)} MT/h", 36, 488, 10);
            AddText(content, $"Average hourly production: {Number(production)} MT/h", 285, 488, 10);
            AddText(content, $"Average yield: {Percent(yield)}", 575, 488, 10);
            AddText(content, $"Total feed input: {Number(readings.Sum(x => x.FeedKg) / 1000d)} MT", 36, 466, 9);
            AddText(content, $"Total Boom 1: {Number(readings.Sum(x => x.Boom1Kg) / 1000d)} MT", 235, 466, 9);
            AddText(content, $"Total Boom 2: {Number(readings.Sum(x => x.Boom2Kg) / 1000d)} MT", 425, 466, 9);
            AddText(content, $"Hours run: {Number(readings.Sum(x => x.RunningMinutes) / 60d)} h", 615, 466, 9);
            AddText(content, $"Operating readings: {Count(readings.Count)}", 36, 444, 9);
        }

        private static List<IReadOnlyList<T>> Paginate<T>(IReadOnlyList<T> rows, int firstPageCount, int laterPageCount)
        {
            var pages = new List<IReadOnlyList<T>> { rows.Take(firstPageCount).ToList() };
            var offset = firstPageCount;
            while (offset < rows.Count)
            {
                pages.Add(rows.Skip(offset).Take(laterPageCount).ToList());
                offset += laterPageCount;
            }
            return pages;
        }

        private static void WriteStreams(string path, IReadOnlyList<string> streams)
        {
            var objects = new List<string>();
            objects.Add("<< /Type /Catalog /Pages 2 0 R >>");
            var pageIds = Enumerable.Range(0, streams.Count).Select(i => 5 + i * 2).ToArray();
            objects.Add($"<< /Type /Pages /Kids [{string.Join(' ', pageIds.Select(x => $"{x} 0 R"))}] /Count {streams.Count} >>");
            objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
            objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>");
            for (var i = 0; i < streams.Count; i++)
            {
                var pageId = pageIds[i];
                var streamId = pageId + 1;
                objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PageWidth} {PageHeight}] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {streamId} 0 R >>");
                var bytes = Encoding.ASCII.GetBytes(streams[i]);
                objects.Add($"<< /Length {bytes.Length} >>\nstream\n{streams[i]}\nendstream");
            }
            using var output = new MemoryStream();
            void W(string text) { var b = Encoding.ASCII.GetBytes(text); output.Write(b); }
            W("%PDF-1.4\n");
            var offsets = new List<long> { 0 };
            for (var i = 0; i < objects.Count; i++) { offsets.Add(output.Position); W($"{i + 1} 0 obj\n{objects[i]}\nendobj\n"); }
            var xref = output.Position;
            W($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
            for (var i = 1; i < offsets.Count; i++) W($"{offsets[i]:D10} 00000 n \n");
            W($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
            File.WriteAllBytes(path, output.ToArray());
        }

        private static void AddHourlyTable(StringBuilder content, IReadOnlyList<ProductionReading> rows, double top)
        {
            double[] widths = [58, 68, 43, 43, 42, 42, 42, 42, 42, 52, 58, 50, 90];
            var headers = new[] { "Date", "Time slot", "Feed kg", "Feed sec", "Boom1 kg", "Boom1 sec", "Boom2 kg", "Boom2 sec", "Run min", "Input MT/h", "Prod MT/h", "Recovery %", "Notes" };
            AddGrid(content, widths, headers, rows.Count, top, 5.8);
            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];
                var values = new[]
                {
                    row.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), row.TimeSlot,
                    Number(row.FeedKg), Number(row.FeedSeconds), Number(row.Boom1Kg), Number(row.Boom1Seconds),
                    Number(row.Boom2Kg), Number(row.Boom2Seconds), Number(row.RunningMinutes),
                    Number(row.HourlyInputMtPerHour), Number(row.HourlyProductionMtPerHour),
                    Percent(row.RecoveryPercent / 100d), Clip(row.Notes, 18)
                };
                var x = TableLeft;
                var baseline = top - RowHeight * (rowIndex + 1) - 13;
                for (var columnIndex = 0; columnIndex < values.Length; columnIndex++)
                {
                    AddText(content, values[columnIndex], x + 3, baseline, 6.2);
                    x += widths[columnIndex];
                }
            }
        }

        private static void AddGrid(StringBuilder content, IReadOnlyList<double> widths, IReadOnlyList<string> headers, int rowCount, double top, double headerSize)
        {
            var tableWidth = widths.Sum();
            var tableHeight = RowHeight * (rowCount + 1);
            content.AppendLine($"0.93 g {TableLeft} {F(top - RowHeight)} {F(tableWidth)} {F(RowHeight)} re f 0 g");
            content.AppendLine($"0.75 w 0.72 G {TableLeft} {F(top - tableHeight)} {F(tableWidth)} {F(tableHeight)} re S");
            var x = TableLeft;
            foreach (var width in widths.Take(widths.Count - 1))
            {
                x += width;
                content.AppendLine($"{F(x)} {F(top)} m {F(x)} {F(top - tableHeight)} l S");
            }
            for (var index = 1; index <= rowCount; index++)
            {
                var y = top - RowHeight * index;
                content.AppendLine($"{TableLeft} {F(y)} m {F(TableLeft + tableWidth)} {F(y)} l S");
            }
            x = TableLeft;
            for (var index = 0; index < headers.Count; index++)
            {
                AddText(content, headers[index], x + 3, top - 13, headerSize, true);
                x += widths[index];
            }
        }

        private static void AddTable(StringBuilder content, IReadOnlyList<DailySummary> rows, double top)
        {
            var tableWidth = ColumnWidths.Sum();
            var tableHeight = RowHeight * (rows.Count + 1);
            content.AppendLine($"0.93 g {TableLeft} {F(top - RowHeight)} {F(tableWidth)} {F(RowHeight)} re f 0 g");
            content.AppendLine($"0.75 w 0.72 G {TableLeft} {F(top - tableHeight)} {F(tableWidth)} {F(tableHeight)} re S");

            var x = TableLeft;
            foreach (var width in ColumnWidths.Take(ColumnWidths.Length - 1))
            {
                x += width;
                content.AppendLine($"{F(x)} {F(top)} m {F(x)} {F(top - tableHeight)} l S");
            }
            for (var index = 1; index <= rows.Count; index++)
            {
                var y = top - RowHeight * index;
                content.AppendLine($"{TableLeft} {F(y)} m {F(TableLeft + tableWidth)} {F(y)} l S");
            }

            var headers = new[] { "Date", "Readings", "Avg input MT/h", "Avg production MT/h", "Yield", "Production for day MT" };
            x = TableLeft;
            for (var index = 0; index < headers.Length; index++)
            {
                AddText(content, headers[index], x + 5, top - 13, 7.5, true);
                x += ColumnWidths[index];
            }

            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];
                var values = new[]
                {
                    ReportDate(row.Date), Count(row.Readings),
                    Number(row.AverageHourlyInputMt), Number(row.AverageHourlyProductionMt),
                    Percent(row.AverageYield), row.ProductionForDayMt is { } production ? Number(production) : "Not Applicable"
                };
                x = TableLeft;
                var baseline = top - RowHeight * (rowIndex + 1) - 13;
                for (var columnIndex = 0; columnIndex < values.Length; columnIndex++)
                {
                    AddText(content, values[columnIndex], x + 5, baseline, 8);
                    x += ColumnWidths[columnIndex];
                }
            }
        }

        private static void AddText(StringBuilder content, string text, double x, double y, double size, bool bold = false) =>
            content.AppendLine($"BT /{(bold ? "F2" : "F1")} {F(size)} Tf {F(x)} {F(y)} Td ({Escape(text)}) Tj ET");

        private static string F(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
        private static string Number(double value) => value.ToString("N3", CultureInfo.InvariantCulture);
        private static string Percent(double value) => value.ToString("P2", CultureInfo.InvariantCulture);
        private static string Count(int value) => value.ToString("N0", CultureInfo.InvariantCulture);
        private static string ReportDate(DateTime value) => value.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
        private static string ReportMonth(DateTime value) => value.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        private static string Clip(string? value, int length) => string.IsNullOrEmpty(value) || value.Length <= length ? value ?? "" : value[..(length - 3)] + "...";
        private static string Escape(string text) => string.Concat(text.Select(c => c < 128 ? c : '?')).Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
    }
}
