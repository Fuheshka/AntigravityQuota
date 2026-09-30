#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."

RAW_IMAGE="${1:-}"
MASTER_PNG="Resources/AppIcon.png"
OUTPUT_ICNS="Resources/AppIcon.icns"
ICONSET_DIR="$(mktemp -d)/AppIcon.iconset"

mkdir -p "Resources" "${ICONSET_DIR}"
trap 'rm -rf "$(dirname "${ICONSET_DIR}")"' EXIT

if [ -n "${RAW_IMAGE}" ] && [ -f "${RAW_IMAGE}" ]; then
    echo "✂️  Cropping and masking squircle edges into ${MASTER_PNG}..."
    python3 - "${RAW_IMAGE}" "${MASTER_PNG}" <<'PYEOF'
import sys
from PIL import Image, ImageDraw, ImageFilter
import numpy as np

raw_path, out_path = sys.argv[1], sys.argv[2]
src = Image.open(raw_path).convert("RGBA")

# Auto-detect squircle bounding box from solid dark or light background
arr = np.array(src.convert("L"))
if arr[0, 0] > 200:
    mask_bg = arr < 240
else:
    mask_bg = arr > 20

y_indices, x_indices = np.where(mask_bg)
if len(x_indices) > 0 and len(y_indices) > 0:
    x_min, x_max = int(x_indices.min()), int(x_indices.max())
    y_min, y_max = int(y_indices.min()), int(y_indices.max())
    w, h = x_max - x_min, y_max - y_min
    dim = max(w, h)
    cx, cy = (x_min + x_max) // 2, (y_min + y_max) // 2
    crop_box = (cx - dim // 2, cy - dim // 2, cx + dim // 2, cy + dim // 2)
    crop = src.crop(crop_box)
else:
    crop = src

body_size = 824
corner_radius = 185
body = crop.resize((body_size, body_size), Image.Resampling.LANCZOS)

# Build 4x supersampled anti-aliased squircle mask (Apple HIG proportions)
scale = 4
ss_size = body_size * scale
mask_ss = Image.new("L", (ss_size, ss_size), 0)
draw = ImageDraw.Draw(mask_ss)
draw.rounded_rectangle((0, 0, ss_size - 1, ss_size - 1), radius=corner_radius * scale, fill=255)
mask = mask_ss.resize((body_size, body_size), Image.Resampling.LANCZOS)
body.putalpha(mask)

# Compose onto 1024x1024 canvas with subtle Apple HIG contact shadow
canvas = Image.new("RGBA", (1024, 1024), (0, 0, 0, 0))
offset = (1024 - body_size) // 2

shadow = Image.new("RGBA", (1024, 1024), (0, 0, 0, 0))
s_draw = ImageDraw.Draw(shadow)
s_box = [offset, offset + 10, offset + body_size, offset + body_size + 10]
s_draw.rounded_rectangle(s_box, radius=corner_radius, fill=(0, 0, 0, 145))
shadow = shadow.filter(ImageFilter.GaussianBlur(20))

canvas = Image.alpha_composite(canvas, shadow)
canvas.paste(body, (offset, offset), body)
canvas.save(out_path, "PNG")
PYEOF
fi

if [ ! -f "${MASTER_PNG}" ]; then
    echo "❌ Master PNG not found at ${MASTER_PNG}" >&2
    exit 1
fi

echo "🎨 Generating iconset sizes via sips..."
for size in 16 32 128 256 512; do
    size2x=$((size * 2))
    sips -s format png -z "${size}" "${size}" "${MASTER_PNG}" --out "${ICONSET_DIR}/icon_${size}x${size}.png" >/dev/null
    sips -s format png -z "${size2x}" "${size2x}" "${MASTER_PNG}" --out "${ICONSET_DIR}/icon_${size}x${size}@2x.png" >/dev/null
done

echo "📦 Packing ${OUTPUT_ICNS} via iconutil..."
iconutil -c icns "${ICONSET_DIR}" -o "${OUTPUT_ICNS}"
echo "✅ Generated ${OUTPUT_ICNS}"
