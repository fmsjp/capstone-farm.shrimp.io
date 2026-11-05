#!/usr/bin/env bash
set -uo pipefail
DIR="$(cd "$(dirname "$0")" && pwd)"
BACKUP_ROOT="${FPMS_BACKUP_ROOT:-$HOME/FPMS-backups}"
INTERVAL_MIN="${FPMS_LOG_BACKUP_MINUTES:-30}"
STATE="$BACKUP_ROOT/.scheduler"
mkdir -p "$STATE"

run() {
  echo "[$(date '+%F %T')] $1"
  if bash "$DIR/$1" "$BACKUP_ROOT"; then return 0; fi
  echo "[$(date '+%F %T')] $1 failed"
  return 1
}
due() { [ ! -f "$STATE/$1" ] || [ $(( $(date +%s) - $(cat "$STATE/$1") )) -ge "$2" ]; }
mark() { date +%s > "$STATE/$1"; }

first=1
while true; do
  if [ "$first" = 1 ] || due full 86400; then
    run backup.sh && mark full && run retention.sh
  fi
  run backup-log.sh
  first=0
  if due check 604800; then run integrity-check.sh && mark check; fi
  if [ -n "${FPMS_OFFSITE_DIR:-}" ] && [ -n "${FPMS_OFFSITE_PASSPHRASE:-}" ] && due offsite 604800; then
    run offsite-copy.sh && mark offsite
  fi
  sleep $(( INTERVAL_MIN * 60 ))
done
