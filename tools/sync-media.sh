#!/usr/bin/env bash
# Re-populate the large bundled media (artwork photos) into StreamingAssets from
# the Camera Obscura web prototype. The Unity project ships with the photos
# already on disk; this is only needed after a fresh git clone (the art/ tree is
# git-ignored to keep the repo lean).
#
# Usage:  tools/sync-media.sh [path-to-web-prototype]
set -euo pipefail

HERE="$(cd "$(dirname "$0")/.." && pwd)"
WEB="${1:-/Users/miller/Desktop/RaulDuke-Games/Photography/camera-obscura}"
DEST="$HERE/Assets/StreamingAssets"

if [ ! -d "$WEB/public/art" ]; then
  echo "Web prototype art not found at: $WEB/public/art"
  echo "Pass the path to the web project as the first argument."
  exit 1
fi

echo "Syncing artwork → $DEST/art"
mkdir -p "$DEST/art" "$DEST/models" "$DEST/fonts"
cp -R "$WEB/public/art/." "$DEST/art/"
cp -f "$WEB/public/models/statue.glb" "$DEST/models/" 2>/dev/null || true
cp -f "$WEB/public/models/plant.glb"  "$DEST/models/" 2>/dev/null || true
cp -f "$WEB/public/fonts/main.ttf"    "$DEST/fonts/LiberationSerif.ttf" 2>/dev/null || true

echo "Re-generating content JSON…"
if command -v node >/dev/null 2>&1 && [ -f "$WEB/tools/run-extract.mjs" ]; then
  ( cd "$WEB" && node tools/run-extract.mjs "$DEST/content" ) || echo "(content already present; skipped)"
fi

echo "Done. $(find "$DEST/art" -type f | wc -l | tr -d ' ') art files present."
