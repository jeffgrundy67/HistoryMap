#!/bin/bash
# Photos helper for public_html/historymap/photos on the cPanel host.
# Usage: tools/deploy/upload_photos.sh list
#        tools/deploy/upload_photos.sh upload web/photos/*.jpg     (sent in batches of 9)
# Needs env vars (never commit them): CPANEL_HOST, CPANEL_USER, CPANEL_API_TOKEN
set -euo pipefail
: "${CPANEL_HOST:?set CPANEL_HOST}" "${CPANEL_USER:?set CPANEL_USER}" "${CPANEL_API_TOKEN:?set CPANEL_API_TOKEN}"
H="https://$CPANEL_HOST:2083/execute"; AUTH="Authorization: cpanel $CPANEL_USER:$CPANEL_API_TOKEN"
case "${1:-}" in
  list)
    curl -sS --max-time 60 -H "$AUTH" "$H/Fileman/list_files?dir=public_html%2Fhistorymap%2Fphotos" \
    | python3 -c "import json,sys; d=json.load(sys.stdin)['data']; j=[x for x in d if x['file'].endswith('.jpg')]; print(len(j), 'jpgs', sum(int(x['size']) for x in j), 'bytes')" ;;
  upload)
    shift
    while [ $# -gt 0 ]; do
      args=(); i=1
      while [ $# -gt 0 ] && [ $i -le 9 ]; do
        case "$1" in *.jpg) ;; *) echo "only .jpg files: $1"; exit 1;; esac
        args+=(-F "file-$i=@$1"); i=$((i+1)); shift
      done
      curl -sS --max-time 300 -H "$AUTH" -F "dir=public_html/historymap/photos" -F "overwrite=1" "${args[@]}" "$H/Fileman/upload_files" \
      | python3 -c "import json,sys; d=json.load(sys.stdin); print('status', d.get('status'), 'ok', len(d['data'].get('uploads', [])) if d.get('data') else 0, d.get('errors'))"
    done ;;
  *) echo "usage: $0 list | upload file.jpg ..."; exit 1 ;;
esac
