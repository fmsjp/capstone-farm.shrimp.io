#!/usr/bin/env bash
set -euo pipefail

export PATH="$PATH:$HOME/.dotnet/tools"
export DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1

ROOT="$(git rev-parse --show-toplevel)"
cd "$ROOT"

LOCAL_ENV="$ROOT/.devcontainer/local.env"
if [ ! -f "$LOCAL_ENV" ]; then
  {
    echo "SA_PASSWORD=${SQL_SA_PASSWORD:-Sa$(openssl rand -hex 8)A1!}"
    echo "JWT_KEY=$(openssl rand -base64 48 | tr -d '/+=')"
    echo "ADMIN_PASSWORD=${ADMIN_INITIAL_PASSWORD:-Admin$(openssl rand -hex 6)A1!}"
  } > "$LOCAL_ENV"
  chmod 600 "$LOCAL_ENV"
fi
set -a; . "$LOCAL_ENV"; set +a

if [ -n "${CODESPACE_NAME:-}" ]; then
  DOMAIN="${GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN:-app.github.dev}"
  API_URL="https://${CODESPACE_NAME}-44311.${DOMAIN}"
  WEB_URL="https://${CODESPACE_NAME}-4200.${DOMAIN}"
else
  API_URL="https://localhost:44311"
  WEB_URL="http://localhost:4200"
fi

DB_NAME="FPMS"

pkill -f "scripts/backup/scheduler.sh" || true

export ConnectionStrings__Default="Server=localhost,1433;Database=${DB_NAME};User Id=sa;Password=${SA_PASSWORD};TrustServerCertificate=True"
export Authentication__JwtBearer__SecurityKey="$JWT_KEY"
export SystemAdmin__InitialPassword="$ADMIN_PASSWORD"
export App__CorsOrigins="$WEB_URL"
export App__ClientRootAddress="$WEB_URL/"
export App__ServerRootAddress="$API_URL/"

printf 'MSSQL_SA_PASSWORD=%s\n' "$SA_PASSWORD" > aspnet-core/docker/sqlserver/.env
for _ in $(seq 1 60); do docker info >/dev/null 2>&1 && break; sleep 2; done
docker info >/dev/null 2>&1 || { echo "Docker is not running."; exit 1; }
(cd aspnet-core/docker/sqlserver && docker compose up -d)

if [ ! -d "$ROOT/aspnet-core/src/Capstone.EntityFrameworkCore/Migrations" ]; then
  (cd "$ROOT" && dotnet tool restore >/dev/null)
  (cd "$ROOT/aspnet-core" && dotnet ef migrations add Initial \
    --project src/Capstone.EntityFrameworkCore --startup-project src/Capstone.Web.Host)

  sqlcmd_q() {
    (cd "$ROOT/aspnet-core/docker/sqlserver" && docker compose exec -T sqlserver \
      /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$SA_PASSWORD" -b -Q "$1")
  }
  for _ in $(seq 1 30); do sqlcmd_q "SELECT 1" >/dev/null 2>&1 && break; sleep 2; done
  echo "Generated a new Initial migration; dropping the old FPMS database."
  for db in FPMS; do
    sqlcmd_q "IF DB_ID('$db') IS NOT NULL BEGIN ALTER DATABASE [$db] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$db]; END" \
      >/dev/null || { echo "Could not drop database $db."; exit 1; }
  done
fi

cd "$ROOT/aspnet-core/src/Capstone.Migrator"
migrator_args=(-q)
migrated=0
for attempt in $(seq 1 12); do
  if dotnet run -- "${migrator_args[@]}" >/tmp/migrator.log 2>&1; then migrated=1; break; fi
  echo "Migration attempt $attempt failed; retrying in 10s..."
  sleep 10
done
[ "$migrated" = 1 ] || { echo "Migration failed. See /tmp/migrator.log"; exit 1; }

(cd "$ROOT/aspnet-core/docker/sqlserver" && docker compose exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$SA_PASSWORD" \
  -Q "ALTER DATABASE [${DB_NAME}] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE") >/tmp/rcsi.log 2>&1 \
  || echo "Could not enable read-committed snapshot; see /tmp/rcsi.log"

if [ "${FPMS_BACKUPS:-1}" = 1 ]; then
  MSSQL_SA_PASSWORD="$SA_PASSWORD" FPMS_DB="$DB_NAME" setsid nohup bash "$ROOT/scripts/backup/scheduler.sh" >>/tmp/backup.log 2>&1 &
fi

pkill -f Capstone.Web.Host || true
pkill -f "ng serve" || true

cd "$ROOT/aspnet-core/src/Capstone.Web.Host"
setsid nohup dotnet run >/tmp/api.log 2>&1 &

cd "$ROOT/angular"
cat > src/assets/appconfig.json <<JSON
{
    "remoteServiceBaseUrl": "${WEB_URL}",
    "appBaseUrl": "${WEB_URL}",
    "localeMappings": [
        { "from": "pt-BR", "to": "pt" },
        { "from": "zh-CN", "to": "zh" },
        { "from": "he-IL", "to": "he" }
    ]
}
JSON
setsid nohup yarn start >/tmp/angular.log 2>&1 &

wait_for() {
  for _ in $(seq 1 90); do
    curl -sk -o /dev/null "$2" && return 0
    sleep 2
  done
  echo "$1 did not start in time. See $3"
  return 1
}
wait_for "API" https://localhost:44311/health /tmp/api.log || true
wait_for "Angular" http://localhost:4200 /tmp/angular.log || true

echo "Database: ${DB_NAME}"
echo "App:     $WEB_URL"
echo "Swagger: $API_URL/swagger/index.html"
echo "Login:   admin / (see .devcontainer/local.env)"
if [ "${FPMS_BACKUPS:-1}" = 1 ]; then
  echo "Backups: ${FPMS_BACKUP_ROOT:-$HOME/FPMS-backups} (log: /tmp/backup.log)"
fi
