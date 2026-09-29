#!/usr/bin/env bash
set -euo pipefail

# Scripts: install_cli_symlink.sh
# Creates a symlink pointing to AntigravityQuota executable

APP_PATH="/Applications/AntigravityQuota.app/Contents/MacOS/AntigravityQuota"
LOCAL_BUILD_PATH="$(cd "$(dirname "$0")/.." && pwd)/.build/release/AntigravityQuota"

SOURCE_PATH=""
if [ -f "${APP_PATH}" ]; then
    SOURCE_PATH="${APP_PATH}"
elif [ -f "${LOCAL_BUILD_PATH}" ]; then
    SOURCE_PATH="${LOCAL_BUILD_PATH}"
    echo "ℹ️  Using build binary: ${LOCAL_BUILD_PATH}"
else
    echo "❌ AntigravityQuota binary not found in /Applications or .build/release."
    echo "Please build the project with 'swift build -c release' or run './scripts/build_app.sh' first."
    exit 1
fi

TARGET_DIR="/usr/local/bin"
TARGET_LINK="${TARGET_DIR}/antigravity-quota"
USER_BIN="${HOME}/.local/bin"

# 1. Try /usr/local/bin if writable or if --sudo passed
if [ -w "${TARGET_DIR}" ]; then
    ln -sf "${SOURCE_PATH}" "${TARGET_LINK}"
    echo "✅ Successfully linked ${TARGET_LINK} -> ${SOURCE_PATH}"
elif [ "${1:-}" = "--sudo" ]; then
    echo "🔐 Linking to ${TARGET_DIR} using sudo..."
    sudo ln -sf "${SOURCE_PATH}" "${TARGET_LINK}"
    echo "✅ Successfully linked ${TARGET_LINK} -> ${SOURCE_PATH}"
else
    echo "ℹ️  /usr/local/bin requires administrator privileges."
    echo "   To create symlink in /usr/local/bin, run:"
    echo "   sudo ln -sf \"${SOURCE_PATH}\" /usr/local/bin/antigravity-quota"
fi

# 2. Also install into ~/.local/bin if available (no sudo needed)
if [ -d "${USER_BIN}" ] && [ -w "${USER_BIN}" ]; then
    ln -sf "${SOURCE_PATH}" "${USER_BIN}/antigravity-quota"
    echo "✅ Created user-level symlink: ${USER_BIN}/antigravity-quota"
fi

echo ""
echo "Testing CLI command:"
if command -v antigravity-quota >/dev/null 2>&1; then
    antigravity-quota --status
else
    "${SOURCE_PATH}" --status
fi
