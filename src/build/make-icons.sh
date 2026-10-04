#!/bin/zsh
# アイコンの SVG（src/FileLauncher.Desktop/Assets/*.svg）から配布物用の画像を作る（macOS で実行。SPEC §3.9 H13）。
#   AppIcon.icns（macOS の .app）/ app.ico（Windows の exe）/ app-256.png（窓のアイコン）/ tray-*.png（トレイ・メニューバー）
# SVG の描画は Quick Look（qlmanage）、.icns は iconutil、.ico は PNG を詰めるだけの小さな Python。
set -euo pipefail
ASSETS=${0:A:h:h}/FileLauncher.Desktop/Assets
TMP=$(mktemp -d)
trap 'rm -rf "$TMP"' EXIT

render() { # render <svg> <px> <out.png>
  qlmanage -t -s "$2" -o "$TMP" "$1" > /dev/null 2>&1
  mv "$TMP/$(basename "$1").png" "$3"
  sips -z "$2" "$2" "$3" > /dev/null # Quick Look は 1 px ずれることがあるので大きさをそろえる
}

echo "==> アプリアイコン"
ICONSET=$TMP/AppIcon.iconset
mkdir -p "$ICONSET"
for s in 16 32 128 256 512; do
  render "$ASSETS/icon.svg" $s "$ICONSET/icon_${s}x${s}.png"
  render "$ASSETS/icon.svg" $((s * 2)) "$ICONSET/icon_${s}x${s}@2x.png"
done
iconutil -c icns "$ICONSET" -o "$ASSETS/AppIcon.icns"
cp "$ICONSET/icon_256x256.png" "$ASSETS/app-256.png"

mkdir -p "$TMP/ico"
for s in 16 24 32 48 64 128 256; do render "$ASSETS/icon.svg" $s "$TMP/ico/$s.png"; done
python3 - "$TMP/ico" "$ASSETS/app.ico" <<'PY'
import struct, sys, os
d, out = sys.argv[1], sys.argv[2]
sizes = [16, 24, 32, 48, 64, 128, 256]
blobs = [open(os.path.join(d, f"{s}.png"), "rb").read() for s in sizes]
head = struct.pack("<HHH", 0, 1, len(sizes))
offset = 6 + 16 * len(sizes)
entries = b""
for s, b in zip(sizes, blobs):
    entries += struct.pack("<BBBBHHII", s % 256, s % 256, 0, 0, 1, 32, len(b), offset)
    offset += len(b)
open(out, "wb").write(head + entries + b"".join(blobs))
PY

echo "==> トレイ / メニューバー"
render "$ASSETS/tray-template.svg" 32 "$ASSETS/tray-template.png"
render "$ASSETS/tray-color.svg" 32 "$ASSETS/tray-color.png"
render "$ASSETS/tray-warning.svg" 32 "$ASSETS/tray-warning.png"
ls -la "$ASSETS"/*.icns "$ASSETS"/*.ico "$ASSETS"/*.png
