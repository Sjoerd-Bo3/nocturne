#!/usr/bin/env bash
# Build the Nocturne DB schema explorer from an EF Core model snapshot.
#
# Regenerates, from the upstream snapshot:
#   README.md, schema.full.mmd, schema.html (interactive), schema-static.html (no-JS)
#
# Env:
#   SCHEMA_REPO    upstream repo (default nightscout/nocturne)
#   SCHEMA_BRANCH  upstream branch (default main)
#   SNAPSHOT_URL   raw URL of NocturneDbContextModelSnapshot.cs (default derived from repo/branch)
#   OUTDIR         where the 4 deliverables are written (default: this script's ../ i.e. docs/db-schema)
#   CHROME_PATH    optional path to a Chromium/Chrome binary for mermaid-cli
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export SCHEMA_REPO="${SCHEMA_REPO:-nightscout/nocturne}"
export SCHEMA_BRANCH="${SCHEMA_BRANCH:-main}"
SNAP_PATH="src/Infrastructure/Nocturne.Infrastructure.Data/Migrations/NocturneDbContextModelSnapshot.cs"
SNAPSHOT_URL="${SNAPSHOT_URL:-https://raw.githubusercontent.com/${SCHEMA_REPO}/${SCHEMA_BRANCH}/${SNAP_PATH}}"
OUTDIR="${OUTDIR:-$(cd "$HERE/.." && pwd)}"

export SCHEMA_WORKDIR="$(mktemp -d)"
echo "workdir: $SCHEMA_WORKDIR"
echo "outdir:  $OUTDIR"
echo "snapshot: $SNAPSHOT_URL"

# 1. fetch the EF model snapshot
curl -fsSL "$SNAPSHOT_URL" -o "$SCHEMA_WORKDIR/snapshot.cs"
echo "snapshot bytes: $(wc -c < "$SCHEMA_WORKDIR/snapshot.cs")"

# 2. resolve upstream commit metadata for the stamp (best-effort)
META_JSON="$(curl -fsSL "https://api.github.com/repos/${SCHEMA_REPO}/commits/${SCHEMA_BRANCH}" || true)"
if [ -n "$META_JSON" ]; then
  export SCHEMA_COMMIT_FULL="$(printf '%s' "$META_JSON" | python3 -c 'import sys,json;print(json.load(sys.stdin).get("sha","")[:40])' 2>/dev/null || true)"
  export SCHEMA_COMMIT="${SCHEMA_COMMIT_FULL:0:7}"
  export SCHEMA_DATE="$(printf '%s' "$META_JSON" | python3 -c 'import sys,json;print(json.load(sys.stdin)["commit"]["author"]["date"][:10])' 2>/dev/null || true)"
fi
echo "stamp: ${SCHEMA_REPO}@${SCHEMA_BRANCH} ${SCHEMA_COMMIT:-?} ${SCHEMA_DATE:-?}"

# 3. parse + generate sources
python3 "$HERE/parse.py"
python3 "$HERE/gen.py"
python3 "$HERE/gen_domains.py"

# 4. render each mermaid block in the README to inline SVG
N="$(python3 "$HERE/render_blocks.py")"
echo "mermaid blocks: $N"
PCFG="$SCHEMA_WORKDIR/puppeteer.json"
if [ -n "${CHROME_PATH:-}" ]; then
  printf '{ "executablePath": "%s", "args": ["--no-sandbox","--disable-setuid-sandbox"] }' "$CHROME_PATH" > "$PCFG"
else
  printf '{ "args": ["--no-sandbox","--disable-setuid-sandbox"] }' > "$PCFG"
fi
printf '{ "maxTextSize": 5000000, "maxEdges": 4000 }' > "$SCHEMA_WORKDIR/mmdcfg.json"
for f in "$SCHEMA_WORKDIR"/blk_*.mmd; do
  npx -y @mermaid-js/mermaid-cli@^11 -i "$f" -o "${f%.mmd}.svg" -p "$PCFG" -c "$SCHEMA_WORKDIR/mmdcfg.json"
done

# 5. build the HTML deliverables
python3 "$HERE/gen_html.py"
python3 "$HERE/gen_static.py"

# 6. publish to OUTDIR
mkdir -p "$OUTDIR"
cp "$SCHEMA_WORKDIR/README.md"            "$OUTDIR/README.md"
cp "$SCHEMA_WORKDIR/schema.full.mmd"      "$OUTDIR/schema.full.mmd"
cp "$SCHEMA_WORKDIR/schema.html"          "$OUTDIR/schema.html"
cp "$SCHEMA_WORKDIR/schema-static.html"   "$OUTDIR/schema-static.html"
echo "built:"
ls -la "$OUTDIR"/schema*.html "$OUTDIR/README.md" "$OUTDIR/schema.full.mmd"
