#!/usr/bin/env bash

# <swiftbar.name>Antigravity Quota</swiftbar.name>
# <swiftbar.version>v1.2.0</swiftbar.version>
# <swiftbar.author>Daniil K. (Fuheshka)</swiftbar.author>
# <swiftbar.author.github>Fuheshka</swiftbar.author.github>
# <swiftbar.desc>Real-time Google Antigravity model quotas and reset countdowns in your macOS menu bar</swiftbar.desc>
# <swiftbar.dependencies>bash,jq</swiftbar.dependencies>
# <swiftbar.abouturl>https://github.com/Fuheshka/AntigravityQuota</swiftbar.abouturl>

set -o pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# 1. Locate the antigravity-quota binary gracefully
find_cli() {
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
  if [ -x "$SCRIPT_DIR/../../AntigravityQuota.app/Contents/MacOS/AntigravityQuota" ]; then
    echo "$SCRIPT_DIR/../../AntigravityQuota.app/Contents/MacOS/AntigravityQuota"
    return 0
  fi
  if [ -x "$PWD/AntigravityQuota.app/Contents/MacOS/AntigravityQuota" ]; then
    echo "$PWD/AntigravityQuota.app/Contents/MacOS/AntigravityQuota"
    return 0
  fi
  if [ -x "$SCRIPT_DIR/../../.build/release/AntigravityQuota" ]; then
    echo "$SCRIPT_DIR/../../.build/release/AntigravityQuota"
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
  echo "Antigravity Quota: CLI not found | color=red"
  echo "---"
  echo "Install AntigravityQuota app or add antigravity-quota to PATH"
  echo "Open Documentation | href=https://github.com/Fuheshka/AntigravityQuota"
  exit 0
fi

# 2. Locate jq binary
find_jq() {
  if command -v jq >/dev/null 2>&1; then
    command -v jq
    return 0
  fi
  if [ -x "/usr/bin/jq" ]; then
    echo "/usr/bin/jq"
    return 0
  fi
  if [ -x "/usr/local/bin/jq" ]; then
    echo "/usr/local/bin/jq"
    return 0
  fi
  if [ -x "/opt/homebrew/bin/jq" ]; then
    echo "/opt/homebrew/bin/jq"
    return 0
  fi
  return 1
}

JQ_BIN="$(find_jq || true)"

# 3. Retrieve JSON snapshot
JSON_DATA="$("$CLI_BIN" --json 2>/dev/null)" || true

# 4. If connection fails or Antigravity is offline, display offline status
if [ -z "$JSON_DATA" ]; then
  echo "⚡ Offline | color=#888888"
  echo "---"
  echo "Antigravity is not running or language_server is offline"
  echo "Open Antigravity | bash=\"open\" param1=\"-a\" param2=\"Antigravity\" terminal=false"
  echo "Refresh | refresh=true"
  exit 0
fi

# 5. Parse and render with jq if available
if [ -n "$JQ_BIN" ] && echo "$JSON_DATA" | "$JQ_BIN" empty >/dev/null 2>&1; then
  CURRENT_TIME="$(date +"%H:%M:%S")"
  echo "$JSON_DATA" | "$JQ_BIN" -r --arg time "$CURRENT_TIME" '
    def fmt_pct: (. * 10 | round) / 10 | tostring | if test("\\.") then . else . + ".0" end + "%";
    def color_for:
      if . > 50 then "#34c759"
      elif . >= 20 then "#ff9500"
      else "#ff3b30" end;

    .summary as $s |
    ([$s.gemini.percentage, $s.claude.percentage] | min) as $min_pct |
    ($min_pct | color_for) as $header_color |
    "G \($s.gemini.percentage | fmt_pct) · C \($s.claude.percentage | fmt_pct) | color=\($header_color) dropdown=false",
    "---",
    "Antigravity Quotas | size=14 header=true",
    "---",
    (
      .groups[] |
      "\(.displayName) | size=13 font=System-Bold",
      (
        (.buckets | sort_by(if .window == "5h" then 0 else 1 end))[] |
        (if .window == "5h" then "5h Rolling" else "Weekly" end) as $win |
        (.percentage | fmt_pct) as $pct |
        (.percentage | color_for) as $col |
        (if .resetCountdown and .resetCountdown != "" and .resetCountdown != "—" then " (Reset: \(.resetCountdown))" else "" end) as $cd |
        "  • \($win): \($pct)\($cd) | color=\($col) font=Menlo size=12 trim=false"
      )
    ),
    "---",
    "Individual Models Breakdown | size=13 font=System-Bold",
    (
      (.models | sort_by(.label))[] |
      (.percentage | fmt_pct) as $pct |
      (.percentage | color_for) as $col |
      (if .resetCountdown and .resetCountdown != "" and .resetCountdown != "—" then " (\(.resetCountdown))" else "" end) as $cd |
      "\(.label): \($pct)\($cd) | color=\($col) font=Menlo size=12"
    ),
    "---",
    "Open Antigravity | bash=\"open\" param1=\"-a\" param2=\"Antigravity\" terminal=false",
    "Open AntigravityQuota App | bash=\"open\" param1=\"-a\" param2=\"AntigravityQuota\" terminal=false",
    "Force Refresh Quotas | refresh=true",
    "---",
    "Updated: \($time) | size=11 color=#888888"
  '
  exit 0
fi

# 6. Fallback if jq is missing: use --status
STATUS="$("$CLI_BIN" --status 2>/dev/null)" || true
if [ -n "$STATUS" ]; then
  CURRENT_TIME="$(date +"%H:%M:%S")"
  echo "${STATUS} | dropdown=false"
  echo "---"
  echo "Antigravity Quotas | size=14 header=true"
  echo "---"
  echo "Status: ${STATUS}"
  echo "---"
  echo "Open Antigravity | bash=\"open\" param1=\"-a\" param2=\"Antigravity\" terminal=false"
  echo "Open AntigravityQuota App | bash=\"open\" param1=\"-a\" param2=\"AntigravityQuota\" terminal=false"
  echo "Force Refresh Quotas | refresh=true"
  echo "---"
  echo "Updated: ${CURRENT_TIME} | size=11 color=#888888"
  exit 0
fi

# 7. Fallback offline if status also fails
echo "⚡ Offline | color=#888888"
echo "---"
echo "Antigravity is not running or language_server is offline"
echo "Open Antigravity | bash=\"open\" param1=\"-a\" param2=\"Antigravity\" terminal=false"
echo "Refresh | refresh=true"
exit 0
