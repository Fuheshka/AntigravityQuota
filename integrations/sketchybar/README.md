# SketchyBar Integration for AntigravityQuota

Real-time Google Antigravity model quota monitor for [SketchyBar](https://github.com/FelixKratz/SketchyBar) on macOS.

Displays live Gemini and Claude quota percentages with color-coded status alerts and optional reset countdown timers.

---

## Preview

```text
󰛩 85.0% (4h 12m) · 󰚩 100.0%
```

- **Sparkle icon (`󰛩`)**: Gemini pool (Flash / Pro)
- **Robot icon (`󰚩`)**: Claude & GPT-OSS pool (Sonnet / Opus / GPT-OSS)
- **Dynamic colors**:
  - 🟢 **Green** (`0xff4ade80`): Quota > 50%
  - 🟠 **Orange** (`0xfffb923c`): Quota 20% - 50%
  - 🔴 **Red** (`0xfff87171`): Quota < 20%
  - ⚪ **Gray** (`0xff94a3b8`): Offline / unreachable daemon

---

## Quick Setup

### 1. Copy or Symlink the Script

Copy or symlink `antigravity_quota.sh` to your SketchyBar plugins directory:

```bash
mkdir -p ~/.config/sketchybar/plugins
cp integrations/sketchybar/antigravity_quota.sh ~/.config/sketchybar/plugins/
chmod +x ~/.config/sketchybar/plugins/antigravity_quota.sh
```

### 2. Add Item to `~/.config/sketchybar/sketchybarrc`

Open `~/.config/sketchybar/sketchybarrc` and add the item:

```bash
# Antigravity Quota Monitor
sketchybar --add item antigravity_quota right \
           --set antigravity_quota \
                 update_freq=30 \
                 icon.drawing=off \
                 label.font="JetBrainsMono Nerd Font:Regular:12.0" \
                 click_script="open -a AntigravityQuota" \
                 script="$PLUGIN_DIR/antigravity_quota.sh"
```

Then reload SketchyBar:

```bash
sketchybar --reload
```

---

## Styling Variations

### Option A: Compact Mode (No Timers)

If you prefer a minimal status bar without countdown timers:

```bash
sketchybar --set antigravity_quota script="$PLUGIN_DIR/antigravity_quota.sh --compact"
```

Displays: `󰛩 85.0% · 󰚩 100.0%`

### Option B: Pill Background Style

```bash
sketchybar --add item antigravity_quota right \
           --set antigravity_quota \
                 update_freq=30 \
                 icon.drawing=off \
                 background.color=0x20ffffff \
                 background.corner_radius=6 \
                 background.height=24 \
                 background.padding_left=4 \
                 background.padding_right=4 \
                 click_script="open -a AntigravityQuota" \
                 script="$PLUGIN_DIR/antigravity_quota.sh"
```

---

## Environment Variables & Configuration

The script can be customized via environment variables:

| Variable | Default | Description |
| :--- | :--- | :--- |
| `GEMINI_ICON` | `󰛩` | Custom glyph or text for Gemini (e.g. `✦`, `G:`) |
| `CLAUDE_ICON` | `󰚩` | Custom glyph or text for Claude (e.g. `󰳆`, `🤖`, `C:`) |
| `COMPACT` | `0` | Set to `1` to omit countdown timers |
| `NO_COLOR` | _unset_ | Disable ANSI colors in terminal stdout |
| `ANTIGRAVITY_QUOTA_BIN` | _auto_ | Path to `antigravity-quota` executable |

### Custom Icons Example

```bash
export GEMINI_ICON="✦"
export CLAUDE_ICON="🤖"
./antigravity_quota.sh
```

---

## Direct Terminal / Scripting Usage

The script also runs directly in any terminal, tmux status bar, or shell script:

```bash
# Standard output with 24-bit TrueColor ANSI codes
./antigravity_quota.sh

# Compact format
./antigravity_quota.sh --compact

# Plain text output (no ANSI escape codes)
./antigravity_quota.sh --no-color
```

---

## Binary Resolution

The script searches for `antigravity-quota` in the following priority:
1. `ANTIGRAVITY_QUOTA_BIN` environment variable (if specified)
2. `antigravity-quota` in `$PATH`
3. `/Applications/AntigravityQuota.app/Contents/MacOS/AntigravityQuota`
4. `~/Applications/AntigravityQuota.app/Contents/MacOS/AntigravityQuota`
5. Relative to script directory or current working directory
6. Local development builds in `.build/release` or `.build/debug`

If none is found, it exits with code `1` and prints a clear resolution tip.
