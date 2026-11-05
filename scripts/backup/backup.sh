#!/usr/bin/env bash
set -euo pipefail
. "$(dirname "$0")/lib.sh"
DEST_ROOT="${1:-$BACKUP_ROOT}"
STAMP="$(date +%Y%m%d-%H%M%S)"
DEST="$DEST_ROOT/$STAMP"
job_guard $KIND_FULL
COMP="$(compression)"

mkdir -p "$DEST"
echo "[$(date '+%H:%M:%S')] backing up database $DB"
sqlcmd -Q "IF (SELECT recovery_model_desc FROM sys.databases WHERE name = N'$DB') <> 'FULL' ALTER DATABASE [$DB] SET RECOVERY FULL" >/dev/null
if [ "$MODE" = docker ]; then
  docker_backup_dir
  sqlcmd -Q "BACKUP DATABASE [$DB] TO DISK = N'/var/opt/mssql/backup/$DB-$STAMP.bak' WITH INIT, CHECKSUM$COMP, NAME = N'FPMS $STAMP'" >/dev/null
  sqlcmd -Q "RESTORE VERIFYONLY FROM DISK = N'/var/opt/mssql/backup/$DB-$STAMP.bak' WITH CHECKSUM"
  docker compose -f "$COMPOSE" cp "sqlserver:/var/opt/mssql/backup/$DB-$STAMP.bak" "$DEST/$DB.bak"
  docker compose -f "$COMPOSE" exec -T -u root sqlserver rm -f "/var/opt/mssql/backup/$DB-$STAMP.bak"
else
  sqlcmd -Q "BACKUP DATABASE [$DB] TO DISK = N'$DEST/$DB.bak' WITH INIT, CHECKSUM$COMP, NAME = N'FPMS $STAMP'"
  sqlcmd -Q "RESTORE VERIFYONLY FROM DISK = N'$DEST/$DB.bak' WITH CHECKSUM"
fi

( cd "$DEST" && sha256 "$DB.bak" > SHA256SUMS )
cat > "$DEST/manifest.txt" <<MANIFEST
FPMS backup $STAMP
database: $DB  (full backup, CHECKSUM verified)

created on: $(hostname)
MANIFEST
SIZE="$(file_size "$DEST/$DB.bak")"
trap - ERR
record_run $KIND_FULL "$JOB_STARTED" 1 "$STAMP/$DB.bak" "$SIZE" "verified with CHECKSUM"
echo "[$(date '+%H:%M:%S')] done: $DEST"
ls -lh "$DEST"
