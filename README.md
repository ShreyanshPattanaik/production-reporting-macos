# Production Reporting for macOS

A separate macOS desktop application for production reporting. It targets macOS Tahoe 26, uses local offline storage, and exports detailed CSV, monthly CSV, and monthly PDF reports.

The existing Windows project is independent and is not modified by this repository.

## Compatibility

- Primary package: Apple Silicon (`osx-arm64`) for current Macs.
- Secondary package: Intel (`osx-x64`) for supported Intel Macs running macOS 26.
- Minimum operating system declared by the app: macOS 26.0.
- Runtime: self-contained .NET 10 LTS; users do not install .NET separately.

## Data and backups

Local data is saved at:

`~/Library/Application Support/ProductionReporting/production-data.json`

Windows and Mac backups use the same schema. A JSON backup exported by either app can be restored by the other app. Automated compatibility tests protect that contract.

## Calculations

- Hourly input (MT) = Feed kg / Feed seconds / 1,000 x 3,600
- Hourly production (MT) = (Boom 1 kg / Boom 1 seconds x 3,600 / 1,000) + (Boom 2 kg / Boom 2 seconds x 3,600 / 1,000)
- Recovery (%) = Hourly production / Hourly input x 100
- Estimated monthly production = Average hourly production x 27 x 22

## Build

```bash
export AVALONIA_TELEMETRY_OPTOUT=1
dotnet build ProductionReporting.slnx --configuration Release
dotnet test tests/ProductionReporting.Core.Tests/ProductionReporting.Core.Tests.csproj --configuration Release
./packaging/macos/package-macos.sh "" arm64
```

The DMG is written to `packaging/output`. Public distribution requires Apple Developer ID signing and notarization credentials; GitHub Actions supports both when the documented repository secrets are configured.
