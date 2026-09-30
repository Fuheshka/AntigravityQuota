#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."

APP_NAME="AntigravityQuota"
VERSION="1.1.0"
DIST_DIR="dist"
APP_BUNDLE="${DIST_DIR}/${APP_NAME}.app"
ZIP_NAME="${APP_NAME}-v${VERSION}-macOS.zip"
DMG_NAME="${APP_NAME}-v${VERSION}-macOS.dmg"

echo "=== 1. Generating Retina Multi-Resolution TIFF Background Artwork ==="
python3 scripts/build_dmg_background.py

echo "=== 2. Building ${APP_NAME} Release Binary ==="
swift build -c release

echo "=== 3. Assembling macOS ${APP_NAME}.app Bundle ==="
rm -rf "${DIST_DIR}"
mkdir -p "${APP_BUNDLE}/Contents/MacOS"
mkdir -p "${APP_BUNDLE}/Contents/Resources"

cp ".build/release/${APP_NAME}" "${APP_BUNDLE}/Contents/MacOS/${APP_NAME}"
chmod +x "${APP_BUNDLE}/Contents/MacOS/${APP_NAME}"

if [ ! -f "Resources/AppIcon.icns" ] && [ -x "scripts/generate_icns.sh" ]; then
    ./scripts/generate_icns.sh
fi

if [ -f "Resources/AppIcon.icns" ]; then
    cp "Resources/AppIcon.icns" "${APP_BUNDLE}/Contents/Resources/AppIcon.icns"
fi
if [ -f "Resources/AppIcon.png" ]; then
    cp "Resources/AppIcon.png" "${APP_BUNDLE}/Contents/Resources/AppIcon.png"
fi

cat > "${APP_BUNDLE}/Contents/Info.plist" <<EOF
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
    <string>${VERSION}</string>
    <key>CFBundleShortVersionString</key>
    <string>${VERSION}</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleIconFile</key>
    <string>AppIcon</string>
    <key>LSMinimumSystemVersion</key>
    <string>13.0</string>
    <key>LSUIElement</key>
    <true/>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
EOF

echo "=== 4. Applying Ad-Hoc Code Signature ==="
codesign --force --deep -r="designated => identifier \"com.fuheshka.antigravity-quota\"" --sign - "${APP_BUNDLE}"

echo "=== 5. Packaging ${ZIP_NAME} Archive ==="
ditto -c -k --keepParent "${APP_BUNDLE}" "${DIST_DIR}/${ZIP_NAME}"

echo "=== 6. Packaging Styled macOS ${DMG_NAME} Disk Image ==="
if command -v create-dmg &> /dev/null; then
    create-dmg \
        --overwrite \
        --volname "${APP_NAME}" \
        --volicon "Resources/AppIcon.icns" \
        --background "Resources/dmg_background.tiff" \
        --window-pos 200 120 \
        --window-size 660 440 \
        --app-drop-link 500 140 \
        --icon "${APP_NAME}.app" 160 140 \
        --hide-extension "${APP_NAME}.app" \
        "${DIST_DIR}/${DMG_NAME}" \
        "${APP_BUNDLE}" || {
            echo "create-dmg returned non-zero, using hdiutil fallback..."
            STAGING_DIR="${DIST_DIR}/dmg_staging"
            mkdir -p "${STAGING_DIR}"
            cp -R "${APP_BUNDLE}" "${STAGING_DIR}/"
            ln -s /Applications "${STAGING_DIR}/Applications"
            hdiutil create -volname "${APP_NAME}" -srcfolder "${STAGING_DIR}" -ov -format UDZO "${DIST_DIR}/${DMG_NAME}"
            rm -rf "${STAGING_DIR}"
        }
else
    STAGING_DIR="${DIST_DIR}/dmg_staging"
    mkdir -p "${STAGING_DIR}"
    cp -R "${APP_BUNDLE}" "${STAGING_DIR}/"
    ln -s /Applications "${STAGING_DIR}/Applications"
    hdiutil create -volname "${APP_NAME}" -srcfolder "${STAGING_DIR}" -ov -format UDZO "${DIST_DIR}/${DMG_NAME}"
    rm -rf "${STAGING_DIR}"
fi

echo "=== 7. Finalizing DMG Bounds & Removing Scrollbars ==="
python3 scripts/finalize_dmg_layout.py "${DIST_DIR}/${DMG_NAME}" "${APP_NAME}"

echo "=========================================="
echo "🎉 Release Packaging Successful!"
echo "App Bundle:  ${APP_BUNDLE}"
echo "ZIP Archive: ${DIST_DIR}/${ZIP_NAME}"
echo "DMG Image:   ${DIST_DIR}/${DMG_NAME}"
echo "=========================================="
