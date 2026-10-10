#!/bin/bash
# Upload files to public_html/historymap on the cPanel host (overwrites).
# Usage: tools/deploy/upload.sh web/index.html web/summaries.json ...
# Needs env vars (never commit them): CPANEL_HOST (e.g. myhost.example.com), CPANEL_USER, CPANEL_API_TOKEN
set -euo pipefail
: "${CPANEL_HOST:?set CPANEL_HOST}" "${CPANEL_USER:?set CPANEL_USER}" "${CPANEL_API_TOKEN:?set CPANEL_API_TOKEN}"
DIR="${UPLOAD_DIR:-public_html/historymap}"
args=(); i=1
for f in "$@"; do
  if [[ "$f" == *.json ]]; then python3 -c "import json,sys; json.load(open(sys.argv[1], encoding='utf-8'))" "$f" || { echo "bad json: $f"; exit 1; }; fi
  args+=(-F "file-$i=@$f"); i=$((i+1))
done
curl -sS --max-time 300 -H "Authorization: cpanel $CPANEL_USER:$CPANEL_API_TOKEN" \
  -F "dir=$DIR" -F "overwrite=1" "${args[@]}" "https://$CPANEL_HOST:2083/execute/Fileman/upload_files" \
| python3 -c "import json,sys; d=json.load(sys.stdin); print('status', d.get('status'), d.get('errors')); print([(x.get('file'), x.get('size')) for x in (d.get('data') or {}).get('uploads', [])])"
