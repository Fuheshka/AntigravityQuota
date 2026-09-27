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

# Crop the squircle body (inset to [155..869] to eliminate outer background halo)
crop = src.crop((155, 155, 869, 869))
body_size = 832
body = crop.resize((body_size, body_size), Image.Resampling.LANCZOS)

# Build 4x supersampled anti-aliased rounded-squircle mask (Apple HIG proportions)
scale = 4
ss_size = body_size * scale
mask_ss = Image.new("L", (ss_size, ss_size), 0)
draw = ImageDraw.Draw(mask_ss)
radius_ss = int(200 * scale)
inset_ss = int(2 * scale)
draw.rounded_rectangle(
    (inset_ss, inset_ss, ss_size - 1 - inset_ss, ss_size - 1 - inset_ss),
    radius=radius_ss,
    fill=255,
)
mask = mask_ss.resize((body_size, body_size), Image.Resampling.LANCZOS)
body.putalpha(mask)

# Compose onto 1024x1024 canvas with subtle Apple HIG drop shadow
canvas = Image.new("RGBA", (1024, 1024), (0, 0, 0, 0))
offset = (1024 - body_size) // 2

shadow_alpha = mask.point(lambda a: int(a * 0.48))
shadow = Image.new("RGBA", (1024, 1024), (0, 0, 0, 0))
shadow_layer = Image.new("RGBA", (body_size, body_size), (0, 0, 0, 255))
shadow_layer.putalpha(shadow_alpha)
shadow.paste(shadow_layer, (offset, offset + 12))
shadow = shadow.filter(ImageFilter.GaussianBlur(radius=18))

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
