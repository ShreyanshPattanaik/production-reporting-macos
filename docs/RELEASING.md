# Releasing the macOS app

The application version is defined once in `Directory.Build.props`. Use semantic versions: patch for compatible fixes, minor for compatible features, and major for breaking workflow or backup changes.

## Branches and releases

1. Develop on a short-lived `feature/*` branch.
2. Merge through a pull request after the macOS workflow passes.
3. Keep `main` releasable and do not commit `artifacts/`, `.tools/`, or generated DMGs.
4. Create an immutable tag matching the application version, such as `v1.0.0-beta.1`.
5. GitHub Actions produces an Apple Silicon DMG and attaches it to the release.

## Apple distribution credentials

Create a protected GitHub environment named `release` and configure:

- `MACOS_CERTIFICATE_BASE64`: base64-encoded Developer ID Application `.p12` certificate.
- `MACOS_CERTIFICATE_PASSWORD`: certificate password.
- `APPLE_SIGNING_IDENTITY`: complete Developer ID Application identity.
- `APPLE_ID`: Apple developer account email.
- `APPLE_TEAM_ID`: Apple Developer Team ID.
- `APPLE_APP_SPECIFIC_PASSWORD`: app-specific password used by `notarytool`.

Without these credentials the workflow can generate an unsigned DMG for internal testing. Public downloads should be signed, hardened, notarized, and stapled.

## Backup compatibility

Do not change `AppData.CurrentSchemaVersion` without adding a migration and tests. The release gate must continue proving that legacy Windows backups import successfully and that a backup can round-trip between independent Windows and Mac storage locations.
