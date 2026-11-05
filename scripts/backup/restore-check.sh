#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../.."
ROOT="$PWD"
DB="${FPMS_DB:-FPMS}"
CHECK_DB="${DB}_RestoreCheck"

COMPOSE="$ROOT/aspnet-core/docker/sqlserver/docker-compose.yml"
WORK="${TMPDIR:-/tmp}/fpms-restore-check-$$"
mkdir -p "$WORK"

if [ -z "${MSSQL_SA_PASSWORD:-}" ] && [ -f "$ROOT/aspnet-core/docker/sqlserver/.env" ]; then
  export MSSQL_SA_PASSWORD="$(grep -E '^MSSQL_SA_PASSWORD=' "$ROOT/aspnet-core/docker/sqlserver/.env" | head -1 | cut -d= -f2- | tr -d '"' )"
fi
sqlcmd() { docker compose -f "$COMPOSE" exec -T -e SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" sqlserver /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -b "$@"; }

counts() {
  sqlcmd -d "$1" -h -1 -W -s '|' -Q "SET NOCOUNT ON;
SELECT t.name, SUM(p.rows) FROM sys.tables t JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0,1)
WHERE t.name LIKE 'Fpms%' OR t.name IN ('AbpTenants','AbpUsers','AbpAuditLogs')
GROUP BY t.name ORDER BY t.name;"
}

echo "== 1. backup"
bash scripts/backup/backup.sh "$WORK/backups" | tail -6
SRC="$(ls -d "$WORK"/backups/*/ | head -1)"
counts "$DB" > "$WORK/live.txt"

echo "== 2. restore into [$CHECK_DB]"
bash scripts/backup/restore.sh "$SRC" "$CHECK_DB" | tail -3
counts "$CHECK_DB" > "$WORK/restored.txt"

echo "== 3. compare"
ok=1
if diff -u "$WORK/live.txt" "$WORK/restored.txt"; then
  echo "row counts identical across $(wc -l < "$WORK/live.txt" | tr -d ' ') tables:"
  grep -E "^Fpms(ProductionLogs|WaterQualityMeasurements|StockMovements|PondBatches|Sales|Transactions|ComplianceDocuments|DocumentPresets)\|" "$WORK/live.txt" | sed 's/^/   /'
else
  ok=0; echo "ROW COUNT MISMATCH"
fi

[ "$ok" = 1 ] && echo "RESTORE CHECK PASSED" || { echo "RESTORE CHECK FAILED"; exit 1; }
