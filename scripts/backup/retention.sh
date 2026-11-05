#!/usr/bin/env bash
set -euo pipefail
ROOT_DIR="${1:-${FPMS_BACKUP_ROOT:-$HOME/FPMS-backups}}"
TODAY_S="$(date +%s)"
age_days() { echo $(( (TODAY_S - $(date -d "${1:0:4}-${1:4:2}-${1:6:2}" +%s 2>/dev/null || date -j -f %Y%m%d "${1:0:8}" +%s)) / 86400 )); }

folders=( $(ls -d "$ROOT_DIR"/20[0-9][0-9][0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9][0-9][0-9] 2>/dev/null | sort) )
if [ "${#folders[@]}" -lt 3 ]; then echo "fewer than 3 full backups; nothing to delete"; exit 0; fi

seen_months=" "
removed=0
for f in "${folders[@]}"; do
  stamp="$(basename "$f")"; ymd="${stamp:0:8}"; month="${stamp:0:6}"
  first=0
  if [[ "$seen_months" != *" $month "* ]]; then seen_months="$seen_months$month "; first=1; fi
  age="$(age_days "$ymd")"
  dow="$(date -d "${ymd:0:4}-${ymd:4:2}-${ymd:6:2}" +%u 2>/dev/null || date -j -f %Y%m%d "$ymd" +%u)"
  keep=0
  [ "$age" -le 14 ] && keep=1
  [ "$dow" = 7 ] && [ "$age" -le 84 ] && keep=1
  [ "$first" = 1 ] && [ "$age" -le 1826 ] && keep=1
  if [ "$keep" = 0 ]; then rm -rf "$f"; removed=$((removed+1)); echo "removed $stamp"; fi
done

recent=""
for f in $(ls -d "$ROOT_DIR"/20[0-9][0-9][0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9][0-9][0-9] 2>/dev/null | sort); do
  stamp="$(basename "$f")"
  if [ "$(age_days "${stamp:0:8}")" -le 14 ]; then recent="$stamp"; break; fi
done
if [ -n "$recent" ] && [ -d "$ROOT_DIR/logs" ]; then
  while IFS= read -r t; do
    lstamp="$(basename "$t" .trn | sed 's/.*-\([0-9]\{8\}-[0-9]\{6\}\)$/\1/')"
    [ "$(age_days "${lstamp:0:8}")" -gt 14 ] && [[ "$lstamp" < "$recent" ]] && rm -f "$t" && removed=$((removed+1))
  done < <(ls "$ROOT_DIR"/logs/*.trn 2>/dev/null)
fi
echo "retention done: $removed item(s) removed"
