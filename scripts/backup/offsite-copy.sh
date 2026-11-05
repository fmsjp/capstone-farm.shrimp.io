#!/usr/bin/env bash
set -euo pipefail
. "$(dirname "$0")/lib.sh"
DEST_ROOT="${1:-$BACKUP_ROOT}"
: "${FPMS_OFFSITE_DIR:?Set FPMS_OFFSITE_DIR}"
: "${FPMS_OFFSITE_PASSPHRASE:?Set FPMS_OFFSITE_PASSPHRASE}"
job_guard $KIND_OFFSITE

latest="$(ls -d "$DEST_ROOT"/20[0-9][0-9][0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9][0-9][0-9] | sort | tail -1)"
[ -n "$latest" ] || { echo "no full backup found"; false; }
stamp="$(basename "$latest")"
mkdir -p "$FPMS_OFFSITE_DIR"
OUT="$FPMS_OFFSITE_DIR/FPMS-offsite-$stamp.tgz.enc"
WORK="$(mktemp -d)"; trap 'rm -rf "$WORK"' EXIT
mkdir -p "$WORK/FPMS-offsite"
cp -R "$latest" "$WORK/FPMS-offsite/"
mkdir -p "$WORK/FPMS-offsite/logs"
if [ -d "$DEST_ROOT/logs" ]; then
  find "$DEST_ROOT/logs" -name '*.trn' -newer "$latest/$DB.bak" -exec cp {} "$WORK/FPMS-offsite/logs/" \;
fi
tar -C "$WORK" -cz FPMS-offsite | openssl enc -aes-256-cbc -pbkdf2 -salt -pass env:FPMS_OFFSITE_PASSPHRASE -out "$OUT"
openssl enc -d -aes-256-cbc -pbkdf2 -in "$OUT" -pass env:FPMS_OFFSITE_PASSPHRASE | tar tz >/dev/null
ls -t "$FPMS_OFFSITE_DIR"/FPMS-offsite-*.tgz.enc | tail -n +9 | xargs -r rm -f
SIZE="$(file_size "$OUT")"
trap - ERR
record_run $KIND_OFFSITE "$JOB_STARTED" 1 "$(basename "$OUT")" "$SIZE" "encrypted, opened and listed after writing"
echo "off-site copy: $OUT"
