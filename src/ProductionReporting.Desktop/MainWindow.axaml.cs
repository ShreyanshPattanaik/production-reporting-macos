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

    public MainWindow()
    {
        InitializeComponent();
        MonthPicker.SelectedDate = new DateTimeOffset(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1));
        EntryDate.SelectedDate = new DateTimeOffset(DateTime.Today);
        TimeSlotCombo.ItemsSource = Enumerable.Range(0, 24).Select(hour => $"{hour:00}:00 - {(hour + 1) % 24:00}:00").ToList();
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

    private DateTime SelectedMonth => MonthPicker.SelectedDate is { } date
        ? new DateTime(date.Year, date.Month, 1)
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
        DailyGrid.ItemsSource = ReportCalculator.BuildDailySummaries(rows);
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
    private void MonthPicker_Changed(object? sender, DatePickerSelectedValueChangedEventArgs e) { if (_initialized) RefreshAll(); }

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
            !TryPositive(Boom2SecondsBox.Text, "Boom 2 measurement time", out var boom2Seconds)) return;
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
}
