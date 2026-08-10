using System.Text.Json;
using System.IO;
using System.Runtime.InteropServices;

namespace ProductionReporting;

public sealed class LocalStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public LocalStore(string? dataFolder = null) => DataFolder = dataFolder ?? GetDefaultDataFolder();

    public string DataFolder { get; }
    public string DataFile => Path.Combine(DataFolder, "production-data.json");

    public static string GetDefaultDataFolder()
    {
        var overrideFolder = Environment.GetEnvironmentVariable("PRODUCTION_REPORTING_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(overrideFolder)) return Path.GetFullPath(overrideFolder);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "ProductionReporting");
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProductionReporting");
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProductionReporting");
    }

    public AppData Load()
    {
        Directory.CreateDirectory(DataFolder);
        if (!File.Exists(DataFile)) return new AppData();
        try
        {
            return Deserialize(File.ReadAllText(DataFile));
        }
        catch (Exception ex)
        {
            var recovery = Path.Combine(DataFolder, $"production-data-unreadable-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            File.Copy(DataFile, recovery, true);
            throw new InvalidDataException($"The local data file could not be read. A recovery copy was saved to {recovery}.", ex);
        }
    }

    public void Save(AppData data)
    {
        data.SchemaVersion = AppData.CurrentSchemaVersion;
        Directory.CreateDirectory(DataFolder);
        var temporary = DataFile + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(data, JsonOptions));
        File.Move(temporary, DataFile, true);
    }

    public void CreateBackup(string destination, AppData data)
    {
        Save(data);
        File.Copy(DataFile, destination, true);
    }

    public AppData ReadBackup(string path) => Deserialize(File.ReadAllText(path));

    public AppData RestoreBackup(string path)
    {
        var restored = ReadBackup(path);
        Save(restored);
        return restored;
    }

    private static AppData Deserialize(string json)
    {
        var data = JsonSerializer.Deserialize<AppData>(json, JsonOptions) ?? new AppData();
        if (data.SchemaVersion > AppData.CurrentSchemaVersion)
            throw new InvalidDataException($"This backup uses data schema {data.SchemaVersion}, but this app supports up to schema {AppData.CurrentSchemaVersion}.");
        data.SchemaVersion = AppData.CurrentSchemaVersion;
        data.Settings ??= new AppSettings();
        data.Readings ??= [];
        return data;
    }
}
