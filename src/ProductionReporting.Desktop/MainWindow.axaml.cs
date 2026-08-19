using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using ProductionReporting;
using System.Globalization;

namespace ProductionReporting.Desktop;

public partial class MainWindow : Window
{
    private readonly LocalStore _store = new();
    private AppData _data = new();
    private Guid? _editingId;
    private bool _initialized;
    private string? _startupWarning;
    private readonly List<string> _timeSlots = Enumerable.Range(0, 24).Select(hour => $"{hour:00}:00 - {(hour + 1) % 24:00}:00").ToList();
    private List<ProductionReading>? _activeCustomReadings;
    private List<DailySummary>? _activeCustomDaily;
    private bool _activeCustomHourly;
    private string? _activeCustomTitle;
    private List<ProductionReading>? _returnReadings;
    private List<DailySummary>? _returnDaily;
    private string? _returnTitle;
    private string? _returnCustomReportTitle;
    private bool _returnWasCustom;
    private bool _suppressDailySelection;

    public MainWindow()
    {
        InitializeComponent();
        var months = Enumerable.Range(0, 1212).Select(index => new MonthOption(new DateTime(2000, 1, 1).AddMonths(index))).ToList();
        MonthPicker.ItemsSource = months;
        MonthPicker.SelectedItem = months.First(month => month.Value.Year == DateTime.Today.Year && month.Value.Month == DateTime.Today.Month);
        EntryDate.SelectedDate = new DateTimeOffset(DateTime.Today);
        TimeSlotCombo.ItemsSource = _timeSlots;
        TimeSlotCombo.SelectedIndex = DateTime.Now.Hour;
        try { _data = _store.Load(); }
        catch (Exception exception) { _startupWarning = exception.Message; }
        LoadSettings();
        RefreshAll();
        UpdateNavigation(0);
        _initialized = true;
        Opened += async (_, _) =>
        {
            if (_startupWarning is not null)
                await ShowMessageAsync("Local data warning", _startupWarning);
        };
    }

    private DateTime SelectedMonth => MonthPicker.SelectedItem is MonthOption month
        ? month.Value
        : new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

    private List<ProductionReading> MonthReadings() => _data.Readings
        .Where(reading => reading.Date.Year == SelectedMonth.Year && reading.Date.Month == SelectedMonth.Month)
        .OrderByDescending(reading => reading.Date).ThenByDescending(reading => reading.TimeSlot).ToList();

    private void RefreshAll()
    {
        var rows = MonthReadings();
        var overview = ReportCalculator.CalculateOverview(rows);
        DashInput.Text = $"{overview.AverageHourlyInputMt:N3} MT/h";
        DashProduction.Text = $"{overview.AverageHourlyProductionMt:N3} MT/h";
        DashRecovery.Text = $"{overview.AverageYield:P1}";
        DashReadings.Text = overview.ReadingCount.ToString("N0");
        DashInputRate.Text = $"{overview.AverageHourlyInputMt:N2} MT/h";
        DashProductionRate.Text = $"{overview.AverageHourlyProductionMt:N2} MT/h";
        DashEstimate.Text = $"{overview.EstimatedMonthlyProductionMt:N3} MT";
        RecentGrid.ItemsSource = rows.Take(8).ToList();
        RecordsGrid.ItemsSource = rows;
        var daily = DailySummaries(rows);
        ShowDefaultDailySummary(rows, daily);
        ReportInput.Text = $"{overview.AverageHourlyInputMt:N3} MT/h";
        ReportProduction.Text = $"{overview.AverageHourlyProductionMt:N3} MT/h";
        ReportRecovery.Text = $"{overview.AverageYield:P1}";
        ReportEstimate.Text = $"{overview.EstimatedMonthlyProductionMt:N3} MT";
        ReportStatus.Text = $"{rows.Count} reading{(rows.Count == 1 ? "" : "s")} in {SelectedMonth:MMMM yyyy}";
    }

    private void Navigate(int index, string title)
    {
        var pages = new Control[] { DashboardPage, EntryPage, RecordsPage, ReportsPage, SettingsPage };
        for (var pageIndex = 0; pageIndex < pages.Length; pageIndex++)
            pages[pageIndex].IsVisible = pageIndex == index;
        PageTitle.Text = title;
        UpdateNavigation(index);
    }

    private void UpdateNavigation(int selectedIndex)
    {
        var buttons = new[] { DashboardNavButton, EntryNavButton, RecordsNavButton, ReportsNavButton, SettingsNavButton };
        for (var index = 0; index < buttons.Length; index++)
            buttons[index].Classes.Set("selected", index == selectedIndex);
    }

    private void Dashboard_Click(object? sender, RoutedEventArgs e) => Navigate(0, "Dashboard");
    private void Entry_Click(object? sender, RoutedEventArgs e) => Navigate(1, _editingId is null ? "Production Entry" : "Edit Production Reading");
    private void Records_Click(object? sender, RoutedEventArgs e) { RefreshAll(); Navigate(2, "Records"); }
    private void Reports_Click(object? sender, RoutedEventArgs e) { RefreshAll(); Navigate(3, "Reports & Export"); }
    private void Settings_Click(object? sender, RoutedEventArgs e) => Navigate(4, "Settings");
    private void MonthPicker_Changed(object? sender, SelectionChangedEventArgs e) { if (_initialized) RefreshAll(); }

    private static List<DailySummary> DailySummaries(List<ProductionReading> rows) => ReportCalculator.BuildDailySummaries(rows);

    private void SaveEntry_Click(object? sender, RoutedEventArgs e)
    {
        ValidationText.Text = "";
        if (EntryDate.SelectedDate is not { } date || TimeSlotCombo.SelectedItem is not string slot)
        {
            ValidationText.Text = "Select a valid date and time slot.";
            return;
        }
        if (!TryPositive(FeedKgBox.Text, "Feed weight", out var feedKg) ||
            !TryPositive(FeedSecondsBox.Text, "Feed measurement time", out var feedSeconds) ||
            !TryNonNegative(Boom1KgBox.Text, "Boom 1 weight", out var boom1Kg) ||
            !TryPositive(Boom1SecondsBox.Text, "Boom 1 measurement time", out var boom1Seconds) ||
            !TryNonNegative(Boom2KgBox.Text, "Boom 2 weight", out var boom2Kg) ||
            !TryPositive(Boom2SecondsBox.Text, "Boom 2 measurement time", out var boom2Seconds) ||
            !TryRunningMinutes(RunningMinutesBox.Text, out var runningMinutes)) return;
        if (boom1Kg + boom2Kg <= 0)
        {
            ValidationText.Text = "Enter output weight for at least one boom.";
            return;
        }

        var reading = _editingId is Guid id ? _data.Readings.First(item => item.Id == id) : new ProductionReading();
        reading.Date = date.Date;
        reading.TimeSlot = slot;
        reading.FeedKg = feedKg;
        reading.FeedSeconds = feedSeconds;
        reading.Boom1Kg = boom1Kg;
        reading.Boom1Seconds = boom1Seconds;
        reading.Boom2Kg = boom2Kg;
        reading.Boom2Seconds = boom2Seconds;
        reading.RunningMinutes = runningMinutes;
        reading.Notes = NotesBox.Text?.Trim() ?? "";
        if (_editingId is null) _data.Readings.Add(reading);
        _store.Save(_data);
        ClearEntry();
        RefreshAll();
        Navigate(0, "Dashboard");
    }

    private bool TryPositive(string? text, string label, out double value)
    {
        if (!TryNumber(text, out value) || value <= 0)
        {
            ValidationText.Text = $"{label} must be a number greater than zero.";
            return false;
        }
        return true;
    }

    private bool TryNonNegative(string? text, string label, out double value)
    {
        if (!TryNumber(text, out value) || value < 0)
        {
            ValidationText.Text = $"{label} must be zero or greater.";
            return false;
        }
        return true;
    }

    private bool TryRunningMinutes(string? text, out double value)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = 0;
            return true;
        }
        if (!TryNumber(text, out value) || value < 0 || value > 60)
        {
            ValidationText.Text = "Running minutes must be a number from 0 to 60.";
            return false;
        }
        return true;
    }

    private static bool TryNumber(string? text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private void ClearEntry_Click(object? sender, RoutedEventArgs e) => ClearEntry();

    private void ClearEntry()
    {
        _editingId = null;
        EntryDate.SelectedDate = new DateTimeOffset(DateTime.Today);
        TimeSlotCombo.SelectedIndex = DateTime.Now.Hour;
        FeedKgBox.Clear();
        FeedSecondsBox.Clear();
        Boom1KgBox.Clear();
        Boom1SecondsBox.Clear();
        Boom2KgBox.Clear();
        Boom2SecondsBox.Clear();
        RunningMinutesBox.Text = "0";
        NotesBox.Clear();
        ValidationText.Text = "";
        SaveEntryButton.Content = "Save reading";
    }

    private async void EditSelected_Click(object? sender, RoutedEventArgs e)
    {
        if (RecordsGrid.SelectedItem is not ProductionReading reading)
        {
            await ShowMessageAsync("Edit reading", "Select a record to edit.");
            return;
        }
        _editingId = reading.Id;
        EntryDate.SelectedDate = new DateTimeOffset(reading.Date);
        TimeSlotCombo.SelectedItem = reading.TimeSlot;
        FeedKgBox.Text = reading.FeedKg.ToString(CultureInfo.CurrentCulture);
        FeedSecondsBox.Text = reading.FeedSeconds.ToString(CultureInfo.CurrentCulture);
        Boom1KgBox.Text = reading.Boom1Kg.ToString(CultureInfo.CurrentCulture);
        Boom1SecondsBox.Text = reading.Boom1Seconds.ToString(CultureInfo.CurrentCulture);
        Boom2KgBox.Text = reading.Boom2Kg.ToString(CultureInfo.CurrentCulture);
        Boom2SecondsBox.Text = reading.Boom2Seconds.ToString(CultureInfo.CurrentCulture);
        RunningMinutesBox.Text = reading.RunningMinutes.ToString(CultureInfo.CurrentCulture);
        NotesBox.Text = reading.Notes;
        SaveEntryButton.Content = "Update reading";
        Navigate(1, "Edit Production Reading");
    }

    private async void DeleteSelected_Click(object? sender, RoutedEventArgs e)
    {
        if (RecordsGrid.SelectedItem is not ProductionReading reading)
        {
            await ShowMessageAsync("Delete reading", "Select a record to delete.");
            return;
        }
        if (!await ConfirmAsync("Confirm delete", "Are you sure you want to delete the selected record?")) return;
        _data.Readings.RemoveAll(item => item.Id == reading.Id);
        _store.Save(_data);
        RefreshAll();
    }

    private void OpenContextMenu_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: { } menu } button)
            menu.Open(button);
    }

    private void ShowDefaultDailySummary(List<ProductionReading> readings, List<DailySummary> daily)
    {
        _activeCustomReadings = null;
        _activeCustomDaily = null;
        _activeCustomTitle = null;
        _returnReadings = null;
        _returnDaily = null;
        _returnCustomReportTitle = null;
        ShowDailySummary(readings, daily, "Daily Summary", false, false);
    }

    private void ShowDailySummary(List<ProductionReading> readings, List<DailySummary> daily, string title, bool custom, bool showBack)
    {
        _suppressDailySelection = true;
        DailyGrid.SelectedItem = null;
        DailyGrid.ItemsSource = daily;
        _suppressDailySelection = false;
        DailyGrid.IsVisible = true;
        HourlySummaryGrid.IsVisible = false;
        SummaryTitle.Text = title;
        SummaryBackButton.IsVisible = showBack;
        ViewCustomSummaryButton.IsVisible = !custom;
        ExportCustomSummaryButton.IsVisible = custom;
        if (custom)
        {
            _activeCustomReadings = readings;
            _activeCustomDaily = daily;
            _activeCustomHourly = false;
            _activeCustomTitle = title;
        }
    }

    private void ShowHourlySummary(List<ProductionReading> readings, string title, bool custom, bool showBack)
    {
        HourlySummaryGrid.ItemsSource = readings.OrderBy(reading => reading.Date).ThenBy(reading => SlotStartHour(reading.TimeSlot)).ToList();
        DailyGrid.IsVisible = false;
        HourlySummaryGrid.IsVisible = true;
        SummaryTitle.Text = title;
        SummaryBackButton.IsVisible = showBack;
        ViewCustomSummaryButton.IsVisible = !custom;
        ExportCustomSummaryButton.IsVisible = custom;
        if (custom)
        {
            _activeCustomReadings = readings;
            _activeCustomDaily = DailySummaries(readings);
            _activeCustomHourly = true;
            _activeCustomTitle = title;
        }
    }

    private void DailyGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressDailySelection || DailyGrid.SelectedItem is not DailySummary selected) return;
        var sourceReadings = _activeCustomReadings ?? MonthReadings();
        var dayReadings = sourceReadings.Where(reading => reading.Date.Date == selected.Date.Date).ToList();
        if (dayReadings.Count == 0) return;
        _returnReadings = sourceReadings;
        _returnDaily = DailyGrid.ItemsSource?.Cast<DailySummary>().ToList() ?? [];
        _returnTitle = SummaryTitle.Text;
        _returnWasCustom = _activeCustomReadings is not null;
        _returnCustomReportTitle = _activeCustomTitle;
        if (_returnWasCustom)
        {
            _activeCustomReadings = dayReadings;
            _activeCustomDaily = DailySummaries(dayReadings);
            _activeCustomHourly = true;
        }
        ShowHourlySummary(dayReadings, $"Hourly Summary - {selected.Date:dd MMM yyyy}", _returnWasCustom, true);
        if (_returnWasCustom)
        {
            var day = selected.Date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
            _activeCustomTitle = $"Custom Production Report from {day} to {day}";
        }
    }

    private void SummaryBack_Click(object? sender, RoutedEventArgs e)
    {
        if (_returnReadings is not null && _returnDaily is not null)
        {
            var readings = _returnReadings;
            var daily = _returnDaily;
            var title = _returnTitle ?? "Daily Summary";
            var custom = _returnWasCustom;
            _returnReadings = null;
            _returnDaily = null;
            ShowDailySummary(readings, daily, title, custom, custom);
            if (custom)
                _activeCustomTitle = _returnCustomReportTitle;
            _returnCustomReportTitle = null;
            return;
        }
        var rows = MonthReadings();
        ShowDefaultDailySummary(rows, DailySummaries(rows));
    }

    private async void ViewCustomSummary_Click(object? sender, RoutedEventArgs e)
    {
        var selection = await ShowSummaryRangeDialogAsync("View custom summary", "Choose the summary type and date range you want to view.");
        if (selection is null) return;
        var rows = SelectRange(selection);
        if (rows.Count == 0)
        {
            await ShowMessageAsync("Custom summary", "No production records were found in the selected range.");
            return;
        }
        var title = CustomReportTitle(selection);
        _returnReadings = null;
        _returnDaily = null;
        if (selection.Hourly)
            ShowHourlySummary(rows, "Hourly Summary", true, true);
        else
            ShowDailySummary(rows, DailySummaries(rows), "Daily Summary", true, true);
        _activeCustomTitle = title;
    }

    private async void ExportCsv_Click(object? sender, RoutedEventArgs e)
    {
        var path = await PickSavePathAsync("CSV file", ["*.csv"], $"{SafeName(_data.Settings.PlantName)}-{SelectedMonth:yyyy-MM}-readings.csv");
        if (path is null) return;
        ReportExporter.WriteCsv(path, MonthReadings().OrderBy(item => item.Date).ThenBy(item => item.TimeSlot));
        ReportStatus.Text = $"CSV saved to {path}";
    }

    private async void ExportMonthlyCsv_Click(object? sender, RoutedEventArgs e)
    {
        var rows = MonthReadings();
        var path = await PickSavePathAsync("CSV file", ["*.csv"], $"{SafeName(_data.Settings.PlantName)}-{SelectedMonth:yyyy-MM}-monthly-summary.csv");
        if (path is null) return;
        ReportExporter.WriteMonthlyCsv(path, _data.Settings.PlantName, SelectedMonth, rows, ReportCalculator.BuildDailySummaries(rows));
        ReportStatus.Text = $"Monthly CSV saved to {path}";
    }

    private async void ExportPdf_Click(object? sender, RoutedEventArgs e)
    {
        var rows = MonthReadings();
        var path = await PickSavePathAsync("PDF document", ["*.pdf"], $"{SafeName(_data.Settings.PlantName)}-{SelectedMonth:yyyy-MM}-report.pdf");
        if (path is null) return;
        ReportExporter.WritePdf(path, _data.Settings.PlantName, SelectedMonth, rows, ReportCalculator.BuildDailySummaries(rows));
        ReportStatus.Text = $"PDF saved to {path}";
    }

    private async void ExportCustomPdf_Click(object? sender, RoutedEventArgs e) => await ExportNewCustomSummaryAsync(true);
    private async void ExportCustomCsv_Click(object? sender, RoutedEventArgs e) => await ExportNewCustomSummaryAsync(false);
    private async void ExportActiveCustomPdf_Click(object? sender, RoutedEventArgs e) => await ExportActiveCustomSummaryAsync(true);
    private async void ExportActiveCustomCsv_Click(object? sender, RoutedEventArgs e) => await ExportActiveCustomSummaryAsync(false);

    private async Task ExportNewCustomSummaryAsync(bool pdf)
    {
        var selection = await ShowSummaryRangeDialogAsync("Export custom summary", "Choose the summary type and date range you want to export.");
        if (selection is null) return;
        var rows = SelectRange(selection);
        if (rows.Count == 0)
        {
            await ShowMessageAsync("Custom export", "No production records were found in the selected range.");
            return;
        }
        await ExportCustomAsync(rows, DailySummaries(rows), selection.Hourly, CustomReportTitle(selection), pdf);
    }

    private async Task ExportActiveCustomSummaryAsync(bool pdf)
    {
        if (_activeCustomReadings is null || _activeCustomDaily is null || _activeCustomTitle is null) return;
        await ExportCustomAsync(_activeCustomReadings, _activeCustomDaily, _activeCustomHourly, _activeCustomTitle, pdf);
    }

    private async Task ExportCustomAsync(List<ProductionReading> rows, List<DailySummary> daily, bool hourly, string title, bool pdf)
    {
        var type = hourly ? "hourly" : "daily";
        if (pdf)
        {
            var path = await PickSavePathAsync("PDF document", ["*.pdf"], $"{SafeName(_data.Settings.PlantName)}-custom-{type}-summary.pdf");
            if (path is null) return;
            ReportExporter.WriteCustomPdf(path, _data.Settings.PlantName, title, rows, daily, hourly);
            ReportStatus.Text = $"Custom PDF saved to {path}";
        }
        else
        {
            var path = await PickSavePathAsync("CSV file", ["*.csv"], $"{SafeName(_data.Settings.PlantName)}-custom-{type}-summary.csv");
            if (path is null) return;
            ReportExporter.WriteCustomCsv(path, _data.Settings.PlantName, title, rows, daily, hourly);
            ReportStatus.Text = $"Custom CSV saved to {path}";
        }
    }

    private async Task<SummaryRange?> ShowSummaryRangeDialogAsync(string title, string prompt)
    {
        var typeCombo = new ComboBox { ItemsSource = new[] { "Hourly summary", "Daily summary" }, SelectedIndex = 0, Width = 190 };
        var fromDate = new DatePicker { SelectedDate = new DateTimeOffset(SelectedMonth), Width = 150 };
        var toDate = new DatePicker { SelectedDate = new DateTimeOffset(SelectedMonth.AddMonths(1).AddDays(-1)), Width = 150 };
        var fromSlot = new ComboBox { ItemsSource = _timeSlots, SelectedIndex = 0, Width = 180 };
        var toSlot = new ComboBox { ItemsSource = _timeSlots, SelectedIndex = 23, Width = 180 };
        var fromSlotPanel = LabeledField("From time slot", fromSlot);
        var toSlotPanel = LabeledField("To time slot", toSlot);
        var error = new TextBlock { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap };
        var continueButton = new Button { Content = "Continue", MinWidth = 90 };
        var cancelButton = new Button { Content = "Cancel", MinWidth = 90 };
        cancelButton.Classes.Add("secondary");
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right, Children = { cancelButton, continueButton } };
        var content = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = prompt, FontSize = 14, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) },
                LabeledField("Summary type", typeCombo),
                LabeledField("From date", fromDate),
                fromSlotPanel,
                LabeledField("To date", toDate),
                toSlotPanel,
                error,
                actions
            }
        };
        var dialog = new Window
        {
            Title = title,
            Width = 480,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = content
        };
        SummaryRange? result = null;
        void UpdateFields()
        {
            var visible = typeCombo.SelectedIndex == 0;
            fromSlotPanel.IsVisible = visible;
            toSlotPanel.IsVisible = visible;
        }
        typeCombo.SelectionChanged += (_, _) => UpdateFields();
        cancelButton.Click += (_, _) => dialog.Close();
        continueButton.Click += (_, _) =>
        {
            if (fromDate.SelectedDate is not { } fromOffset || toDate.SelectedDate is not { } toOffset)
            {
                error.Text = "Select both a From date and a To date.";
                return;
            }
            var startDate = fromOffset.Date;
            var endDate = toOffset.Date;
            var hourly = typeCombo.SelectedIndex == 0;
            var startSlot = fromSlot.SelectedItem as string ?? _timeSlots[0];
            var endSlot = toSlot.SelectedItem as string ?? _timeSlots[^1];
            var start = startDate.AddHours(hourly ? SlotStartHour(startSlot) : 0);
            var end = endDate.AddHours(hourly ? SlotStartHour(endSlot) : 0);
            if (start > end)
            {
                error.Text = "The From date and time must be earlier than or equal to the To date and time.";
                return;
            }
            result = new SummaryRange(hourly, startDate, endDate, startSlot, endSlot);
            dialog.Close();
        };
        UpdateFields();
        await dialog.ShowDialog(this);
        return result;
    }

    private static StackPanel LabeledField(string label, Control control) => new()
    {
        Spacing = 5,
        Children =
        {
            new TextBlock { Text = label },
            control
        }
    };

    private List<ProductionReading> SelectRange(SummaryRange selection)
    {
        IEnumerable<ProductionReading> query = _data.Readings;
        if (selection.Hourly)
        {
            var start = selection.FromDate.AddHours(SlotStartHour(selection.FromSlot));
            var end = selection.ToDate.AddHours(SlotStartHour(selection.ToSlot));
            query = query.Where(reading =>
            {
                var timestamp = reading.Date.Date.AddHours(SlotStartHour(reading.TimeSlot));
                return timestamp >= start && timestamp <= end;
            });
        }
        else
        {
            query = query.Where(reading => reading.Date.Date >= selection.FromDate && reading.Date.Date <= selection.ToDate);
        }
        return query.OrderBy(reading => reading.Date).ThenBy(reading => SlotStartHour(reading.TimeSlot)).ToList();
    }

    private static string CustomReportTitle(SummaryRange selection)
    {
        var from = selection.FromDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
        var to = selection.ToDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
        return selection.Hourly
            ? $"Custom Production Report from {from} {selection.FromSlot} to {to} {selection.ToSlot}"
            : $"Custom Production Report from {from} to {to}";
    }

    private static int SlotStartHour(string? slot)
    {
        if (string.IsNullOrWhiteSpace(slot) || slot.Length < 2) return 0;
        return int.TryParse(slot.AsSpan(0, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var hour) ? hour : 0;
    }

    private static string SafeName(string value) => string.Concat(value.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '-' : character)).Replace(' ', '-');

    private void LoadSettings()
    {
        PlantNameBox.Text = _data.Settings.PlantName;
        CapacityBox.Text = _data.Settings.MonthlyCapacityMt?.ToString(CultureInfo.CurrentCulture) ?? "";
        SidePlantName.Text = _data.Settings.PlantName;
        StoragePathText.Text = $"Storage: {_store.DataFile}";
    }

    private async void SaveSettings_Click(object? sender, RoutedEventArgs e)
    {
        var name = PlantNameBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(name))
        {
            await ShowMessageAsync("Settings", "Enter a site name.");
            return;
        }
        double? capacity = null;
        if (!string.IsNullOrWhiteSpace(CapacityBox.Text))
        {
            if (!TryNumber(CapacityBox.Text, out var value) || value <= 0)
            {
                await ShowMessageAsync("Settings", "Monthly capacity must be a positive number or left blank.");
                return;
            }
            capacity = value;
        }
        _data.Settings.PlantName = name;
        _data.Settings.MonthlyCapacityMt = capacity;
        _store.Save(_data);
        LoadSettings();
        RefreshAll();
        await ShowMessageAsync("Settings", "Settings saved.");
    }

    private async void Backup_Click(object? sender, RoutedEventArgs e)
    {
        var path = await PickSavePathAsync("Production Reporting backup", ["*.json"], $"production-reporting-backup-{DateTime.Now:yyyyMMdd-HHmm}.json");
        if (path is null) return;
        _store.CreateBackup(path, _data);
        await ShowMessageAsync("Local data", "Backup created.");
    }

    private async void Restore_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Restore Production Reporting backup",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Production Reporting backup") { Patterns = ["*.json"] }]
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path is null) return;
        if (!await ConfirmAsync("Restore backup", "Restoring replaces the current local data. Continue?")) return;
        try
        {
            _data = _store.RestoreBackup(path);
            LoadSettings();
            RefreshAll();
            await ShowMessageAsync("Local data", "Backup restored.");
        }
        catch (Exception exception)
        {
            await ShowMessageAsync("Restore failed", exception.Message);
        }
    }

    private async Task<string?> PickSavePathAsync(string label, IReadOnlyList<string> patterns, string suggestedName)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = $"Save {label}",
            SuggestedFileName = suggestedName,
            FileTypeChoices = [new FilePickerFileType(label) { Patterns = patterns }]
        });
        return file?.TryGetLocalPath();
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        var closeButton = new Button { Content = "OK", MinWidth = 80, HorizontalAlignment = HorizontalAlignment.Right };
        var dialog = CreateDialog(title, message, closeButton);
        closeButton.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(this);
    }

    private async Task<bool> ConfirmAsync(string title, string message)
    {
        var result = false;
        var yesButton = new Button { Content = "Yes", MinWidth = 80 };
        var noButton = new Button { Content = "No", MinWidth = 80 };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right, Children = { noButton, yesButton } };
        var dialog = CreateDialog(title, message, buttons);
        yesButton.Click += (_, _) => { result = true; dialog.Close(); };
        noButton.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(this);
        return result;
    }

    private static Window CreateDialog(string title, string message, Control actions) => new()
    {
        Title = title,
        Width = 430,
        SizeToContent = SizeToContent.Height,
        CanResize = false,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        Content = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 20,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, FontSize = 14 },
                actions
            }
        }
    };

    private sealed record MonthOption(DateTime Value)
    {
        public string Display => Value.ToString("MM/yyyy", CultureInfo.InvariantCulture);
    }

    private sealed record SummaryRange(bool Hourly, DateTime FromDate, DateTime ToDate, string FromSlot, string ToSlot);
}
