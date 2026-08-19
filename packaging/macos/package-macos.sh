#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
VERSION="${1:-$(grep -oE '<Version>[^<]+' "$REPO_ROOT/Directory.Build.props" | head -1 | cut -d'>' -f2)}"
ARCH="${2:-arm64}"
if [[ "$ARCH" != "arm64" && "$ARCH" != "x64" ]]; then
  echo "Architecture must be arm64 or x64." >&2
  exit 1
fi

RID="osx-$ARCH"
MARKETING_VERSION="${VERSION%%-*}"
BUILD_VERSION="${GITHUB_RUN_NUMBER:-1}"
PUBLISH_DIR="$REPO_ROOT/artifacts/macos/$RID/publish"
BUNDLE_ROOT="$REPO_ROOT/artifacts/macos/$RID/ProductionReporting.app"
CONTENTS="$BUNDLE_ROOT/Contents"
MACOS_DIR="$CONTENTS/MacOS"
RESOURCES_DIR="$CONTENTS/Resources"
OUTPUT_DIR="$REPO_ROOT/packaging/output"
DMG_PATH="$OUTPUT_DIR/ProductionReporting-macOS-$ARCH-$VERSION.dmg"
DMG_STAGE="$REPO_ROOT/artifacts/macos/$RID/dmg"

rm -rf "$REPO_ROOT/artifacts/macos/$RID"
mkdir -p "$PUBLISH_DIR" "$MACOS_DIR" "$RESOURCES_DIR" "$OUTPUT_DIR"

dotnet publish "$REPO_ROOT/src/ProductionReporting.Desktop/ProductionReporting.Desktop.csproj" \
  -c Release -r "$RID" --self-contained true \
  -p:PublishSingleFile=true \
  -p:DebugType=None -p:DebugSymbols=false -p:Version="$VERSION" \
  -o "$PUBLISH_DIR"
cp -R "$PUBLISH_DIR/"* "$MACOS_DIR/"
chmod +x "$MACOS_DIR/ProductionReporting"
rm -rf "$PUBLISH_DIR"

sed -e "s/__MARKETING_VERSION__/$MARKETING_VERSION/g" -e "s/__BUILD_VERSION__/$BUILD_VERSION/g" \
  "$SCRIPT_DIR/Info.plist.template" > "$CONTENTS/Info.plist"
plutil -lint "$CONTENTS/Info.plist"

ICONSET="$REPO_ROOT/artifacts/macos/$RID/AppIcon.iconset"
mkdir -p "$ICONSET"
for size in 16 32 128 256 512; do
  sips -z "$size" "$size" "$REPO_ROOT/Assets/AppLogo.png" --out "$ICONSET/icon_${size}x${size}.png" >/dev/null
  double=$((size * 2))
  sips -z "$double" "$double" "$REPO_ROOT/Assets/AppLogo.png" --out "$ICONSET/icon_${size}x${size}@2x.png" >/dev/null
done
iconutil -c icns "$ICONSET" -o "$RESOURCES_DIR/AppIcon.icns"

if [[ -n "${APPLE_SIGNING_IDENTITY:-}" ]]; then
  while IFS= read -r binary; do
    codesign --force --options runtime --timestamp --entitlements "$SCRIPT_DIR/ProductionReporting.entitlements" --sign "$APPLE_SIGNING_IDENTITY" "$binary"
  done < <(find "$MACOS_DIR" -type f \( -perm -111 -o -name '*.dylib' \))
  codesign --force --options runtime --timestamp --entitlements "$SCRIPT_DIR/ProductionReporting.entitlements" --sign "$APPLE_SIGNING_IDENTITY" "$BUNDLE_ROOT"
else
  # Keep the bundle structurally valid on Apple Silicon when Developer ID
  # credentials are unavailable. Gatekeeper still requires a user override
  # until the app is Developer ID-signed and notarized.
  codesign --force --deep --sign - "$BUNDLE_ROOT"
fi
codesign --verify --deep --strict --verbose=2 "$BUNDLE_ROOT"

# GitHub's macOS runners can be tight on disk after the test and publish steps.
# Once the signed app bundle exists, the intermediate build output and NuGet
# cache are no longer needed and can be cleared before hdiutil copies the app.
if [[ "${CI:-}" == "true" ]]; then
  rm -rf \
    "$REPO_ROOT/src/ProductionReporting.Core/bin" \
    "$REPO_ROOT/src/ProductionReporting.Core/obj" \
    "$REPO_ROOT/src/ProductionReporting.Desktop/bin" \
    "$REPO_ROOT/src/ProductionReporting.Desktop/obj" \
    "$REPO_ROOT/tests/ProductionReporting.Core.Tests/bin" \
    "$REPO_ROOT/tests/ProductionReporting.Core.Tests/obj"
  dotnet nuget locals all --clear >/dev/null
fi

rm -f "$DMG_PATH"
mkdir -p "$DMG_STAGE"
mv "$BUNDLE_ROOT" "$DMG_STAGE/"
ln -s /Applications "$DMG_STAGE/Applications"
hdiutil create -volname "Production Reporting" -srcfolder "$DMG_STAGE" -ov -format UDZO "$DMG_PATH"

if [[ -n "${APPLE_SIGNING_IDENTITY:-}" ]]; then
  codesign --force --timestamp --sign "$APPLE_SIGNING_IDENTITY" "$DMG_PATH"
  codesign --verify --verbose=2 "$DMG_PATH"
fi

if [[ -n "${APPLE_ID:-}" && -n "${APPLE_TEAM_ID:-}" && -n "${APPLE_APP_SPECIFIC_PASSWORD:-}" ]]; then
  xcrun notarytool submit "$DMG_PATH" --apple-id "$APPLE_ID" --team-id "$APPLE_TEAM_ID" \
    --password "$APPLE_APP_SPECIFIC_PASSWORD" --wait
  xcrun stapler staple "$DMG_PATH"
fi

echo "$DMG_PATH"
