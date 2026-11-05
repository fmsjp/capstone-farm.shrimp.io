#!/usr/bin/env bash
set -euo pipefail
. "$(dirname "$0")/lib.sh"
job_guard $KIND_CHECK
OUT="$(sqlcmd -Q "DBCC CHECKDB ([$DB]) WITH NO_INFOMSGS, ALL_ERRORMSGS" 2>&1)" || { echo "$OUT" >&2; false; }
if [ -n "$OUT" ]; then echo "$OUT" >&2; false; fi
trap - ERR
record_run $KIND_CHECK "$JOB_STARTED" 1 "" "" "CHECKDB found no errors"
echo "CHECKDB passed for $DB"
