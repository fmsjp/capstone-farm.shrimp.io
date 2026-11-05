ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
DB="${FPMS_DB:-FPMS}"
MODE="${FPMS_SQL_MODE:-docker}"
BACKUP_ROOT="${FPMS_BACKUP_ROOT:-$HOME/FPMS-backups}"
COMPOSE="$ROOT/aspnet-core/docker/sqlserver/docker-compose.yml"

if [ -z "${MSSQL_SA_PASSWORD:-}" ] && [ -f "$ROOT/aspnet-core/docker/sqlserver/.env" ]; then
  MSSQL_SA_PASSWORD="$(grep -E '^MSSQL_SA_PASSWORD=' "$ROOT/aspnet-core/docker/sqlserver/.env" | head -1 | cut -d= -f2- | tr -d '"')"
fi
: "${MSSQL_SA_PASSWORD:?Set MSSQL_SA_PASSWORD}"

sqlcmd() {
  if [ "$MODE" = docker ]; then
    docker compose -f "$COMPOSE" exec -T -e SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" sqlserver /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -b "$@"
  else
    SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" command sqlcmd -C -S localhost -U sa -b "$@"
  fi
}

KIND_FULL=1; KIND_LOG=2; KIND_CHECK=3; KIND_RESTORE_TEST=4; KIND_OFFSITE=5

sql_quote() { printf '%s' "${1//\'/\'\'}"; }

sha256() { if command -v sha256sum >/dev/null 2>&1; then sha256sum "$@"; else shasum -a 256 "$@"; fi; }

record_run() {
  local kind="$1" started="$2" ok="$3" file="${4:-}" size="${5:-}" msg="${6:-}"
  msg="${msg:0:900}"
  sqlcmd -d "$DB" -Q "SET NOCOUNT ON; INSERT INTO FpmsBackupRuns (Kind, StartedAtUtc, FinishedAtUtc, Succeeded, FileName, SizeBytes, Message, Host)
    VALUES ($kind, '$started', SYSUTCDATETIME(), $ok, N'$(sql_quote "$file")', ${size:-NULL}, N'$(sql_quote "$msg")', N'$(sql_quote "$(hostname)")')" >/dev/null 2>&1 \
    || echo "warning: could not record the run in FpmsBackupRuns" >&2
}

now_utc() { date -u +%Y-%m-%dT%H:%M:%S; }

job_guard() {
  JOB_KIND="$1"; JOB_STARTED="$(now_utc)"
  set -E
  trap 'rc=$?; trap - ERR; record_run "$JOB_KIND" "$JOB_STARTED" 0 "" "" "failed with exit code $rc at line $LINENO of $(basename "$0")"; echo "FAILED (exit $rc, line $LINENO)" >&2; exit $rc' ERR
}

file_size() { stat -c %s "$1" 2>/dev/null || stat -f %z "$1"; }

docker_backup_dir() {
  docker compose -f "$COMPOSE" exec -T -u root sqlserver bash -c 'mkdir -p /var/opt/mssql/backup && chown mssql /var/opt/mssql/backup'
}

log_chain_started() {
  local v
  v="$(sqlcmd -h -1 -W -Q "SET NOCOUNT ON; SELECT CASE WHEN d.recovery_model_desc = 'FULL' AND s.last_log_backup_lsn IS NOT NULL THEN 1 ELSE 0 END
    FROM sys.databases d JOIN sys.database_recovery_status s ON s.database_id = d.database_id WHERE d.name = N'$DB'" | tr -d '[:space:]')"
  [ "$v" = 1 ]
}

compression() {
  local e
  e="$(sqlcmd -h -1 -W -Q "SET NOCOUNT ON; SELECT CAST(SERVERPROPERTY('EngineEdition') AS int)" | tr -d '[:space:]')"
  [ "$e" = 4 ] && echo "" || echo ", COMPRESSION"
}
