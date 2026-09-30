#!/usr/bin/env bash

# Required parameters:
# @raycast.schemaVersion 1
# @raycast.title Antigravity Quota
# @raycast.mode inline
# @raycast.refreshTime 1m

# Optional parameters:
# @raycast.icon ⚡
# @raycast.packageName Developer Utilities

# Documentation:
# @raycast.description Real-time Google Antigravity model quotas and reset countdowns
# @raycast.author Daniil K. (Fuheshka)
# @raycast.authorURL https://github.com/Fuheshka
#
# Raycast Modes:
# - Default: "@raycast.mode inline" (compact one-line status in Raycast root search)
# - Alternative: replace "@raycast.mode inline" with "@raycast.mode fullOutput"
#   above to view a detailed multiline breakdown in a dedicated Raycast window.

set -euo pipefail

export PATH="/opt/homebrew/bin:/usr/local/bin:$HOME/.local/bin:$PATH"

# 1. Locate the antigravity-quota executable
find_cli() {
  if [ -n "${ANTIGRAVITY_QUOTA_BIN:-}" ] && [ -x "$ANTIGRAVITY_QUOTA_BIN" ]; then
    echo "$ANTIGRAVITY_QUOTA_BIN"
    return 0
  fi
  if command -v antigravity-quota >/dev/null 2>&1; then
    command -v antigravity-quota
    return 0
  fi
  if [ -x "/Applications/AntigravityQuota.app/Contents/MacOS/AntigravityQuota" ]; then
    echo "/Applications/AntigravityQuota.app/Contents/MacOS/AntigravityQuota"
    return 0
  fi
  if [ -x "$HOME/Applications/AntigravityQuota.app/Contents/MacOS/AntigravityQuota" ]; then
    echo "$HOME/Applications/AntigravityQuota.app/Contents/MacOS/AntigravityQuota"
    return 0
  fi
  local script_dir
  script_dir="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")" 2>/dev/null && pwd)"
  if [ -x "$script_dir/../../AntigravityQuota.app/Contents/MacOS/AntigravityQuota" ]; then
    echo "$script_dir/../../AntigravityQuota.app/Contents/MacOS/AntigravityQuota"
    return 0
  fi
  if [ -x "$PWD/AntigravityQuota.app/Contents/MacOS/AntigravityQuota" ]; then
    echo "$PWD/AntigravityQuota.app/Contents/MacOS/AntigravityQuota"
    return 0
  fi
  if [ -x "$script_dir/../../.build/release/AntigravityQuota" ]; then
    echo "$script_dir/../../.build/release/AntigravityQuota"
    return 0
  fi
  if [ -x "$script_dir/../../.build/release/antigravity-quota" ]; then
    echo "$script_dir/../../.build/release/antigravity-quota"
    return 0
  fi
  if [ -x "$script_dir/../../.build/debug/AntigravityQuota" ]; then
    echo "$script_dir/../../.build/debug/AntigravityQuota"
    return 0
  fi
  if [ -x "$script_dir/../../.build/debug/antigravity-quota" ]; then
    echo "$script_dir/../../.build/debug/antigravity-quota"
    return 0
  fi
  if [ -x "$HOME/.local/bin/antigravity-quota" ]; then
    echo "$HOME/.local/bin/antigravity-quota"
    return 0
  fi
  return 1
}

CLI_BIN="$(find_cli || true)"
if [ -z "$CLI_BIN" ]; then
  echo "Antigravity is not running"
  exit 0
fi

# 2. Check execution mode (inline vs fullOutput)
IS_FULL=0
SCRIPT_FILE="${BASH_SOURCE[0]:-$0}"
if grep -Eq "^#[[:space:]]*@raycast\.mode[[:space:]]+fullOutput" "$SCRIPT_FILE" 2>/dev/null; then
  IS_FULL=1
fi
for arg in "$@"; do
  case "$arg" in
    --full|-f|fullOutput)
      IS_FULL=1
      ;;
  esac
done

# 3. Retrieve status from CLI
STATUS_OUTPUT="$("$CLI_BIN" --status 2>/dev/null)" || true

# 4. Handle offline / daemon unreachable state
if [ -z "$STATUS_OUTPUT" ] || [[ "$STATUS_OUTPUT" == *"not reachable"* ]]; then
  if [ "$IS_FULL" -eq 1 ]; then
    echo "⚡ Antigravity Quota"
    echo "──────────────────────────────────────────"
    echo "Antigravity is not running"
    echo ""
    echo "Please open Google Antigravity or AntigravityQuota:"
    echo "  open -a Antigravity"
    echo "  open -a AntigravityQuota"
  else
    echo "Antigravity is not running"
  fi
  exit 0
fi

# 5. Output for inline mode (single line in Raycast search row)
if [ "$IS_FULL" -eq 0 ]; then
  echo "$STATUS_OUTPUT"
  exit 0
fi

# 6. Detailed output for fullOutput mode
echo "⚡ Antigravity Quotas"
echo "──────────────────────────────────────────"
echo "Status: $STATUS_OUTPUT"
echo ""

# Attempt detailed breakdown using jq if available
JSON_DATA=""
if command -v jq >/dev/null 2>&1; then
  JSON_DATA="$("$CLI_BIN" --json 2>/dev/null || true)"
fi

if [ -n "$JSON_DATA" ] && echo "$JSON_DATA" | jq empty >/dev/null 2>&1; then
  echo "Model Pools:"
  echo "$JSON_DATA" | jq -r '
    def fmt_pct: (. * 10 | round) / 10 | tostring | if test("\\.") then . else . + ".0" end + "%";
    .groups[] |
    "  • \(.displayName):",
    (
      (.buckets | sort_by(if .window == "5h" then 0 else 1 end))[] |
      (if .window == "5h" then "5h Rolling" else "Weekly" end) as $win |
      (if .resetCountdown and .resetCountdown != "" and .resetCountdown != "—" then " (Reset: \(.resetCountdown))" else "" end) as $cd |
      "      - \($win): \(.percentage | fmt_pct)\($cd)"
    )
  '
  echo ""
  echo "Individual Models:"
  echo "$JSON_DATA" | jq -r '
    def fmt_pct: (. * 10 | round) / 10 | tostring | if test("\\.") then . else . + ".0" end + "%";
    (.models | sort_by(.label))[] |
    (if .resetCountdown and .resetCountdown != "" and .resetCountdown != "—" then " (\(.resetCountdown))" else "" end) as $cd |
    "  • \(.label): \(.percentage | fmt_pct)\($cd)"
  '
fi

echo "──────────────────────────────────────────"
echo "Updated: $(date +"%H:%M:%S")"
exit 0
