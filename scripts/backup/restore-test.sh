#!/usr/bin/env bash
set -euo pipefail
. "$(dirname "$0")/lib.sh"
DEST_ROOT="${1:-$BACKUP_ROOT}"
TEST_DB="${DB}_RestoreTest"
job_guard $KIND_RESTORE_TEST
latest="$(ls -d "$DEST_ROOT"/20[0-9][0-9][0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9][0-9][0-9] | sort | tail -1)"
[ -n "$latest" ] || { echo "no full backup found"; false; }

T0="$(date +%s)"
LOGS_ARG=()
[ -d "$DEST_ROOT/logs" ] && LOGS_ARG=(--logs "$DEST_ROOT/logs")
bash "$(dirname "$0")/restore.sh" "$latest" "$TEST_DB" "${LOGS_ARG[@]}"
OUT="$(sqlcmd -Q "DBCC CHECKDB ([$TEST_DB]) WITH NO_INFOMSGS, ALL_ERRORMSGS" 2>&1)" || { echo "$OUT" >&2; false; }
[ -z "$OUT" ] || { echo "$OUT" >&2; false; }
ROWS="$(sqlcmd -d "$TEST_DB" -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM AbpTenants")"
sqlcmd -Q "ALTER DATABASE [$TEST_DB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$TEST_DB];"
SECS=$(( $(date +%s) - T0 ))
trap - ERR
record_run $KIND_RESTORE_TEST "$JOB_STARTED" 1 "$(basename "$latest")" "" "restored with log chain and CHECKDB passed in ${SECS}s ($ROWS farm account rows)"
echo "RESTORE TEST PASSED in ${SECS}s"
