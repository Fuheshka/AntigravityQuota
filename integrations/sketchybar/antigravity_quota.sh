#!/usr/bin/env bash
# ==============================================================================
# AntigravityQuota - SketchyBar Integration Script
#
# Real-time Google Antigravity model quota monitor for SketchyBar.
# Displays live Gemini & Claude quota percentages with color-coded alerts
# and optional countdown timers.
#
# Author: Daniil K. (Fuheshka)
# GitHub: https://github.com/Fuheshka/AntigravityQuota
# ==============================================================================

set -euo pipefail

# --- Configuration & Customization ---
GEMINI_ICON="${GEMINI_ICON:-󰛩}"
CLAUDE_ICON="${CLAUDE_ICON:-󰚩}"

# SketchyBar ARGB Hex Colors (Tailwind v4 palette)
SKETCHY_GREEN="0xff4ade80"   # > 50%
SKETCHY_ORANGE="0xfffb923c"  # 20% - 50%
SKETCHY_RED="0xfff87171"     # < 20%
SKETCHY_GRAY="0xff94a3b8"    # Unknown / Offline

# Terminal 24-bit TrueColor ANSI Codes
ANSI_GREEN="\033[38;2;74;222;128m"
ANSI_ORANGE="\033[38;2;251;146;60m"
ANSI_RED="\033[38;2;248;113;113m"
ANSI_GRAY="\033[38;2;148;163;184m"
ANSI_RESET="\033[0m"

# Parse Command-line Options
COMPACT="${COMPACT:-0}"
FORCE_COLOR=""

print_usage() {
    cat << 'EOF'
AntigravityQuota - SketchyBar Integration

Usage:
  antigravity_quota.sh [options]

Options:
  -c, --compact       Omit reset countdown timers (e.g. "󰛩 85.0% · 󰚩 100.0%")
  --color             Force ANSI colors in stdout output
  --no-color          Disable ANSI colors in stdout output
  -h, --help          Show this help message

Environment Variables:
  GEMINI_ICON         Custom icon for Gemini (default: 󰛩)
  CLAUDE_ICON         Custom icon for Claude (default: 󰚩)
  ANTIGRAVITY_QUOTA_BIN  Explicit path to antigravity-quota executable
  COMPACT             Set to 1 to enable compact mode
  NO_COLOR            Disable ANSI color output (standard conformant)

SketchyBar Configuration Example (~/.config/sketchybar/sketchybarrc):
  sketchybar --add item antigravity_quota right \
             --set antigravity_quota update_freq=30 \
                                     icon.drawing=off \
                                     click_script="open -a AntigravityQuota" \
                                     script="$PLUGIN_DIR/antigravity_quota.sh"
EOF
}

while [ $# -gt 0 ]; do
    case "$1" in
        -c|--compact)
            COMPACT=1
            shift
            ;;
        --color)
            FORCE_COLOR="1"
            shift
            ;;
        --no-color)
            FORCE_COLOR="0"
            shift
            ;;
        -h|--help)
            print_usage
            exit 0
            ;;
        *)
            shift
            ;;
    esac
done

# --- 1. Locate antigravity-quota Binary ---
find_cli_bin() {
    # 0. User override via environment variable
    if [ -n "${ANTIGRAVITY_QUOTA_BIN:-}" ] && [ -x "$ANTIGRAVITY_QUOTA_BIN" ]; then
        echo "$ANTIGRAVITY_QUOTA_BIN"
        return 0
    fi

    # 1. PATH lookup
    if command -v antigravity-quota >/dev/null 2>&1; then
        command -v antigravity-quota
        return 0
    fi

    # 2. System-wide /Applications
    if [ -x "/Applications/AntigravityQuota.app/Contents/MacOS/AntigravityQuota" ]; then
        echo "/Applications/AntigravityQuota.app/Contents/MacOS/AntigravityQuota"
        return 0
    fi

    # 3. User ~/Applications
    if [ -x "$HOME/Applications/AntigravityQuota.app/Contents/MacOS/AntigravityQuota" ]; then
        echo "$HOME/Applications/AntigravityQuota.app/Contents/MacOS/AntigravityQuota"
        return 0
    fi

    # 4. Relative to this script directory
    local script_dir
    script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" 2>/dev/null && pwd)"
    if [ -x "$script_dir/../../AntigravityQuota.app/Contents/MacOS/AntigravityQuota" ]; then
        echo "$script_dir/../../AntigravityQuota.app/Contents/MacOS/AntigravityQuota"
        return 0
    fi

    # 5. Relative to current working directory
    if [ -x "$PWD/AntigravityQuota.app/Contents/MacOS/AntigravityQuota" ]; then
        echo "$PWD/AntigravityQuota.app/Contents/MacOS/AntigravityQuota"
        return 0
    fi

    # 6. Local SwiftPM build artifacts (.build)
    if [ -x "$script_dir/../../.build/release/antigravity-quota" ]; then
        echo "$script_dir/../../.build/release/antigravity-quota"
        return 0
    fi
    if [ -x "$script_dir/../../.build/debug/antigravity-quota" ]; then
        echo "$script_dir/../../.build/debug/antigravity-quota"
        return 0
    fi

    return 1
}

CLI_BIN="$(find_cli_bin || true)"
if [ -z "$CLI_BIN" ]; then
    echo "Error: antigravity-quota binary not found." >&2
    echo "Please ensure AntigravityQuota.app is installed or 'antigravity-quota' is in your PATH." >&2
    exit 1
fi

# --- 2. Retrieve & Parse Quota Snapshot ---
G_PCT=""
G_RESET=""
C_PCT=""
C_RESET=""

# Attempt 1: Fetch structured JSON if jq is present
if command -v jq >/dev/null 2>&1; then
    JSON_OUTPUT="$("$CLI_BIN" --json 2>/dev/null || true)"
    if [ -n "$JSON_OUTPUT" ]; then
        G_RAW=$(echo "$JSON_OUTPUT" | jq -r '.summary.gemini.percentage // empty' 2>/dev/null || true)
        G_RESET=$(echo "$JSON_OUTPUT" | jq -r '.summary.gemini.resetCountdown // empty' 2>/dev/null || true)
        C_RAW=$(echo "$JSON_OUTPUT" | jq -r '.summary.claude.percentage // empty' 2>/dev/null || true)
        C_RESET=$(echo "$JSON_OUTPUT" | jq -r '.summary.claude.resetCountdown // empty' 2>/dev/null || true)

        if [ -n "$G_RAW" ]; then
            G_PCT=$(awk -v v="$G_RAW" 'BEGIN { printf "%.1f", v }')
        fi
        if [ -n "$C_RAW" ]; then
            C_PCT=$(awk -v v="$C_RAW" 'BEGIN { printf "%.1f", v }')
        fi
    fi
fi

# Attempt 2: Fallback to --status parsing
if [ -z "$G_PCT" ] || [ -z "$C_PCT" ]; then
    STATUS_OUTPUT="$("$CLI_BIN" --status 2>/dev/null || true)"
    if [ -n "$STATUS_OUTPUT" ]; then
        re="G[[:space:]]+([0-9.]+|—)%([[:space:]]+\(([^)]+)\))?[[:space:]]+·[[:space:]]+C[[:space:]]+([0-9.]+|—)%([[:space:]]+\(([^)]+)\))?"
        if [[ "$STATUS_OUTPUT" =~ $re ]]; then
            G_PCT="${BASH_REMATCH[1]}"
            G_RESET="${BASH_REMATCH[3]}"
            C_PCT="${BASH_REMATCH[4]}"
            C_RESET="${BASH_REMATCH[6]}"
        fi
    fi
fi

# Handle offline / unreachable daemon state gracefully
OFFLINE=0
if [ -z "$G_PCT" ] && [ -z "$C_PCT" ]; then
    OFFLINE=1
    G_PCT="—"
    C_PCT="—"
fi

# --- 3. Color Tier Determination ---
# Returns:
#   0 -> Red (< 20%)
#   1 -> Orange (20% - 50%)
#   2 -> Green (> 50%)
#   3 -> Gray (unknown / error)
get_color_tier() {
    local val="$1"
    awk -v v="$val" 'BEGIN {
        if (v == "" || v == "—" || v ~ /[^0-9.]/) { print 3; exit 0; }
        if (v < 20.0) { print 0; exit 0; }
        if (v <= 50.0) { print 1; exit 0; }
        print 2; exit 0;
    }'
}

get_sketchy_color() {
    case "$1" in
        0) echo "$SKETCHY_RED" ;;
        1) echo "$SKETCHY_ORANGE" ;;
        2) echo "$SKETCHY_GREEN" ;;
        *) echo "$SKETCHY_GRAY" ;;
    esac
}

get_ansi_color() {
    case "$1" in
        0) echo "$ANSI_RED" ;;
        1) echo "$ANSI_ORANGE" ;;
        2) echo "$ANSI_GREEN" ;;
        *) echo "$ANSI_GRAY" ;;
    esac
}

G_TIER="$(get_color_tier "$G_PCT")"
C_TIER="$(get_color_tier "$C_PCT")"

# Compute overall tier (minimum between models for alert urgency)
if [ "$G_TIER" -eq 3 ] && [ "$C_TIER" -eq 3 ]; then
    OVERALL_TIER=3
elif [ "$G_TIER" -eq 3 ]; then
    OVERALL_TIER="$C_TIER"
elif [ "$C_TIER" -eq 3 ]; then
    OVERALL_TIER="$G_TIER"
elif [ "$G_TIER" -lt "$C_TIER" ]; then
    OVERALL_TIER="$G_TIER"
else
    OVERALL_TIER="$C_TIER"
fi

SKETCHY_COLOR="$(get_sketchy_color "$OVERALL_TIER")"
G_ANSI="$(get_ansi_color "$G_TIER")"
C_ANSI="$(get_ansi_color "$C_TIER")"

# --- 4. Format Labels ---
is_under_100() {
    local val="$1"
    awk -v v="$val" 'BEGIN {
        if (v == "" || v == "—" || v ~ /[^0-9.]/) { print 0; exit 0; }
        if (v < 99.99) { print 1; exit 0; }
        print 0; exit 0;
    }'
}

G_COUNTDOWN=""
if [ "$COMPACT" != "1" ] && [ -n "$G_RESET" ] && [ "$G_RESET" != "null" ] && [ "$G_RESET" != "—" ]; then
    if [ "$(is_under_100 "$G_PCT")" = "1" ]; then
        G_COUNTDOWN=" ($G_RESET)"
    fi
fi

C_COUNTDOWN=""
if [ "$COMPACT" != "1" ] && [ -n "$C_RESET" ] && [ "$C_RESET" != "null" ] && [ "$C_RESET" != "—" ]; then
    if [ "$(is_under_100 "$C_PCT")" = "1" ]; then
        C_COUNTDOWN=" ($C_RESET)"
    fi
fi

# Clean formatted parts
G_PART="${GEMINI_ICON} ${G_PCT}%${G_COUNTDOWN}"
C_PART="${CLAUDE_ICON} ${C_PCT}%${C_COUNTDOWN}"

PLAIN_LABEL="${G_PART} · ${C_PART}"
COLORED_OUTPUT="${G_ANSI}${G_PART}${ANSI_RESET} · ${C_ANSI}${C_PART}${ANSI_RESET}"

# --- 5. SketchyBar Integration ---
# If $NAME is set (invoked as SketchyBar item script) or if sketchybar command exists
ITEM_NAME="${NAME:-antigravity_quota}"

if command -v sketchybar >/dev/null 2>&1; then
    sketchybar --set "$ITEM_NAME" label="$PLAIN_LABEL" label.color="$SKETCHY_COLOR" 2>/dev/null || true
fi

# --- 6. Print Formatted String to stdout ---
should_use_color() {
    if [ "$FORCE_COLOR" = "0" ] || [ -n "${NO_COLOR:-}" ]; then
        return 1
    fi
    if [ "$FORCE_COLOR" = "1" ] || [ -n "${CLICOLOR_FORCE:-}" ]; then
        return 0
    fi
    if [ -t 1 ]; then
        return 0
    fi
    return 1
}

if should_use_color; then
    printf "%b\n" "$COLORED_OUTPUT"
else
    printf "%s\n" "$PLAIN_LABEL"
fi

if [ "$OFFLINE" -eq 1 ]; then
    exit 1
fi
exit 0
