using ProductionReporting;

namespace ProductionReporting.Core.Tests;

public sealed class BackupCompatibilityTests
{
    [Fact]
    public void LegacyWindowsBackupWithoutSchemaVersionImportsAndRoundTrips()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ProductionReportingTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var legacyBackup = Path.Combine(root, "windows-v1-backup.json");
            File.WriteAllText(legacyBackup, """
            {
              "Settings": { "PlantName": "Portable Site", "MonthlyCapacityMt": 4000 },
              "Readings": [
                {
                  "Id": "bd2f6404-4b74-4a65-8718-08e9f117d335",
                  "Date": "2026-08-08T00:00:00",
                  "TimeSlot": "19:00 - 20:00",
                  "FeedKg": 15.8,
                  "FeedSeconds": 5.27,
                  "Boom1Kg": 10.6,
                  "Boom1Seconds": 10.1,
                  "Boom2Kg": 5.0,
                  "Boom2Seconds": 10.8,
                  "Notes": "Windows backup",
                  "CreatedAt": "2026-08-10T12:00:00"
                }
              ]
            }
            """);

            var windowsStore = new LocalStore(Path.Combine(root, "windows"));
            var imported = windowsStore.RestoreBackup(legacyBackup);
            Assert.Equal(AppData.CurrentSchemaVersion, imported.SchemaVersion);
            Assert.Equal("Portable Site", imported.Settings.PlantName);
            Assert.Single(imported.Readings);
            Assert.Equal(0, imported.Readings[0].RunningMinutes);

            var portableBackup = Path.Combine(root, "portable-backup.json");
            windowsStore.CreateBackup(portableBackup, imported);
            var macStore = new LocalStore(Path.Combine(root, "mac"));
            var restoredOnMac = macStore.RestoreBackup(portableBackup);

            Assert.Equal(imported.Settings.PlantName, restoredOnMac.Settings.PlantName);
            Assert.Equal(imported.Readings[0].Id, restoredOnMac.Readings[0].Id);
            Assert.Equal(imported.Readings[0].HourlyProductionMtPerHour, restoredOnMac.Readings[0].HourlyProductionMtPerHour, 10);
            Assert.Equal(imported.Readings[0].RunningMinutes, restoredOnMac.Readings[0].RunningMinutes);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void FutureSchemaIsRejectedClearly()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ProductionReportingTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var backup = Path.Combine(root, "future.json");
            File.WriteAllText(backup, "{\"SchemaVersion\":999,\"Settings\":{},\"Readings\":[]}");
            var exception = Assert.Throws<InvalidDataException>(() => new LocalStore(root).ReadBackup(backup));
            Assert.Contains("schema 999", exception.Message);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
