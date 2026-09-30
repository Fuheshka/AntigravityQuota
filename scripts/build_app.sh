#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."

echo "🔨 Building AntigravityQuota (Release)..."
swift build -c release

APP_NAME="AntigravityQuota"
APP_BUNDLE="${APP_NAME}.app"
CONTENTS="${APP_BUNDLE}/Contents"
MACOS="${CONTENTS}/MacOS"
RESOURCES="${CONTENTS}/Resources"

rm -rf "${APP_BUNDLE}"
mkdir -p "${MACOS}" "${RESOURCES}"

cp ".build/release/${APP_NAME}" "${MACOS}/${APP_NAME}"
chmod +x "${MACOS}/${APP_NAME}"

if [ ! -f "Resources/AppIcon.icns" ] && [ -x "scripts/generate_icns.sh" ]; then
    ./scripts/generate_icns.sh
fi

if [ -f "Resources/AppIcon.icns" ]; then
    cp "Resources/AppIcon.icns" "${RESOURCES}/AppIcon.icns"
fi
if [ -f "Resources/AppIcon.png" ]; then
    cp "Resources/AppIcon.png" "${RESOURCES}/AppIcon.png"
fi

cat > "${CONTENTS}/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleExecutable</key>
    <string>${APP_NAME}</string>
    <key>CFBundleIdentifier</key>
    <string>com.fuheshka.antigravity-quota</string>
    <key>CFBundleName</key>
    <string>${APP_NAME}</string>
    <key>CFBundleDisplayName</key>
    <string>Antigravity Quota</string>
    <key>CFBundleVersion</key>
    <string>1.1.0</string>
    <key>CFBundleShortVersionString</key>
    <string>1.1.0</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleIconFile</key>
    <string>AppIcon</string>
    <key>NSHumanReadableCopyright</key>
    <string>Copyright © 2026 Daniil K. (Fuheshka). All rights reserved.</string>
    <key>LSMinimumSystemVersion</key>
    <string>13.0</string>
    <key>LSUIElement</key>
    <true/>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
EOF

codesign --force --deep --sign - "${APP_BUNDLE}" >/dev/null 2>&1 || true

rm -rf "/Applications/${APP_BUNDLE}"
cp -R "${APP_BUNDLE}" "/Applications/${APP_BUNDLE}"

echo "✅ Installed to /Applications/${APP_BUNDLE}"

if pgrep -x "${APP_NAME}" >/dev/null 2>&1; then
    echo "🔄 Restarting running ${APP_NAME}..."
    killall "${APP_NAME}" 2>/dev/null || true
    sleep 0.5
fi
open "/Applications/${APP_BUNDLE}"
echo "🚀 Launched /Applications/${APP_BUNDLE}"
