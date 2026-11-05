# Runbook

Day-to-day operations for the FPMS development environment: starting and stopping, credentials, the database, seed data, backup and restore, logs and common problems.

All commands run from the repository root unless a step says otherwise.

## Contents

- [Start and stop](#start-and-stop)
- [Credentials](#credentials)
- [Configuration](#configuration)
- [Database](#database)
- [Changing the data model](#changing-the-data-model)
- [Seed data](#seed-data)
- [Backup and restore](#backup-and-restore)
- [Logs](#logs)
- [Running without a Codespace](#running-without-a-codespace)
- [Production build](#production-build)
- [Troubleshooting](#troubleshooting)

## Start and stop

The Codespace runs `.devcontainer/start-fpms.sh` every time it starts. The script:

1. creates `.devcontainer/local.env` with generated secrets (first run only)
2. works out the app and API URLs (Codespace URLs or `localhost`)
3. starts SQL Server with Docker Compose
4. generates a fresh `Initial` migration if the `Migrations` folder does not exist, and drops the old `FPMS` database when it does so
5. runs the Migrator to create or upgrade the database and seed it
6. turns on read-committed snapshot isolation for the database
7. starts the backup scheduler (see [Backup and restore](#backup-and-restore))
8. starts the API and the Angular dev server in the background, then waits until both answer

**Restart everything** (safe to run any time):

```bash
bash .devcontainer/start-fpms.sh
```

**Stop the API, Angular and the backup scheduler:**

```bash
pkill -f Capstone.Web.Host; pkill -f "ng serve"; pkill -f scripts/backup/scheduler.sh
```

**Stop SQL Server** (the data stays in the Docker volume):

```bash
cd aspnet-core/docker/sqlserver && docker compose down
```

Stopping the Codespace from GitHub stops all of it. The database survives a stop and restart of the same Codespace, but not deleting the Codespace.

## Credentials

| What | Where it comes from |
| --- | --- |
| SQL Server `sa` password | Codespaces secret `SQL_SA_PASSWORD`, otherwise generated |
| Admin initial password | Codespaces secret `ADMIN_INITIAL_PASSWORD`, otherwise generated |
| JWT signing key | Always generated |

All three are written once to `.devcontainer/local.env` (git-ignored, `chmod 600`) and reused after that. To see them:

```bash
cat .devcontainer/local.env
```

The `admin` account signs in with a **blank farm code** and must change its password at first sign-in. After that, `ADMIN_PASSWORD` in `local.env` is only the original password, not the current one.

The SQL Server volume keeps the `sa` password it was created with. Do not delete or edit `local.env` while the volume still exists; see [SQL Server will not start or login fails](#sql-server-will-not-start-or-login-fails).

## Configuration

The start script passes settings to the API and the Migrator as environment variables, which override `appsettings.json`:

| Variable | Purpose |
| --- | --- |
| `ConnectionStrings__Default` | SQL Server connection to the `FPMS` database |
| `Authentication__JwtBearer__SecurityKey` | JWT signing key (the API refuses to start without one) |
| `SystemAdmin__InitialPassword` | Password given to `admin` when the account is first created |
| `App__CorsOrigins`, `App__ClientRootAddress`, `App__ServerRootAddress` | App and API URLs |

The Angular app reads `angular/src/assets/appconfig.json`, which the start script writes each run (git-ignored). In development the browser talks only to the Angular dev server, which forwards `/api`, `/AbpUserConfiguration` and `/signalr` to the API (`angular/proxy.conf.json`).

Farm self-registration is controlled by the application setting `Fpms.Registration.Enabled` (on by default).

## Database

SQL Server 2022 Developer edition runs in Docker (`aspnet-core/docker/sqlserver/docker-compose.yml`). Data is kept in the `fpms-sqldata` volume.

**Open a SQL prompt:**

```bash
set -a; . .devcontainer/local.env; set +a
cd aspnet-core/docker/sqlserver
docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$SA_PASSWORD" -d FPMS
```

You can also connect from a SQL client to `localhost,1433` (forwarded port) as `sa`.

**Reset the database** (deletes all data and starts from admin plus reference data):

```bash
set -a; . .devcontainer/local.env; set +a
cd aspnet-core/docker/sqlserver
docker compose exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$SA_PASSWORD" \
  -Q "ALTER DATABASE [FPMS] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [FPMS];"
cd ../../.. && bash .devcontainer/start-fpms.sh
```

**Wipe SQL Server completely** (also resets the `sa` password to the one in `local.env`):

```bash
cd aspnet-core/docker/sqlserver && docker compose down -v
cd ../../.. && bash .devcontainer/start-fpms.sh
```

**Run the Migrator by hand** (uses the same environment variables as the start script, so run the script once first in the same terminal or export them yourself):

```bash
cd aspnet-core/src/Capstone.Migrator && dotnet run -- -q
```

`-q` skips the "press ENTER" prompt and exits with code 1 on failure.

## Changing the data model

Migrations are not kept in the repository: `aspnet-core/src/Capstone.EntityFrameworkCore/Migrations/` is git-ignored. Each Codespace generates one `Initial` migration from the current model.

After changing an entity or `FpmsModelConfiguration.cs`:

```bash
rm -rf aspnet-core/src/Capstone.EntityFrameworkCore/Migrations
bash .devcontainer/start-fpms.sh
```

The script regenerates `Initial`, drops the old `FPMS` database (a new `Initial` cannot upgrade a database built from an older one) and recreates it. **All data in the database is lost**, so do this before entering data you want to keep.

## Seed data

The Migrator (and the API at startup) runs the host seeders in `aspnet-core/src/Capstone.EntityFrameworkCore/EntityFrameworkCore/Seed/Host/`:

| Seeder | Creates |
| --- | --- |
| `DefaultEditionCreator` | Default edition |
| `DefaultLanguagesCreator` | Languages |
| `DefaultSettingsCreator` | Default settings |
| `HostRoleAndUserCreator` | System Administrator role and the `admin` user |
| `ParameterDefinitionCreator` | Water-quality parameter definitions |
| `DensityBandCreator` | Stocking density bands |

`FarmRoleSynchronizer` then keeps the Farm Owner and Farm Technician roles and permissions in step for every farm.

The seeders only add what is missing, so running them again is safe.

### Adding a demo data seed

There is no sample farm. If you add one (for a demo or the defense), follow these rules:

- **Go through the app's own logic.** Create farms with `FarmProvisioningManager.CreateFarmAsync`, users with `CreateFarmUserAsync`, and records through the application services or domain managers. The model has many check constraints, unique indexes and per-farm number sequences, and direct inserts can create data the app would never allow.
- **Make it opt-in.** Turn it on with one setting (for example `Seed:DemoData=true`), use a separate database such as `FPMS_Demo`, and leave it off by default.
- **Make it repeatable.** Skip a farm whose farm code already exists, and use a fixed random seed.
- **Date it from today** (Asia/Manila), so the dashboard and daily status always show recent activity.
- **Keep the numbers consistent.** Inventory on hand must equal the stock movements, sales must not exceed harvest weights, and every log must fall inside its batch's active dates.
- **Keep passwords in configuration**, not in code.
- **Check it on screen.** After seeding, open the dashboard, ponds, batches, logbook, inventory, harvests, sales and reports.

## Backup and restore

### How it works

The app does not back itself up. Scripts in `scripts/backup/` make the backups, and every run (success or failure) adds a row to the `FpmsBackupRuns` table. The app only reads that table: **System → System health** lists the runs, and the health check turns missing or failed runs into alerts. Critical alerts appear in the banner at the top of every page for the System Administrator and Farm Owners.

A backup is the database only. The system stores no uploaded files (compliance records are database rows), so nothing else needs copying.

| Job | Script | How often | What it does |
| --- | --- | --- | --- |
| Full backup | `backup.sh` | Daily | Full SQL Server backup with CHECKSUM, verified, into a timestamped folder with `SHA256SUMS` and `manifest.txt`. Switches the database to the FULL recovery model if needed. |
| Log backup | `backup-log.sh` | Every 30 minutes | Transaction-log backup, so at most about 30 minutes of data can be lost. If the log chain has not started yet, takes a full backup instead. |
| Retention | `retention.sh` | Daily, after the full backup | Deletes old backups (see below). |
| Integrity check | `integrity-check.sh` | Weekly | `DBCC CHECKDB` on the live database. |
| Off-site copy | `offsite-copy.sh` | Weekly | Newest full backup plus the log backups since, as one AES-256 encrypted archive, written to `FPMS_OFFSITE_DIR` and test-opened. Keeps the last 8 copies. |
| Restore test | `restore-test.sh` | Quarterly and after each release | Restores the newest backup and its logs into `FPMS_RestoreTest`, runs CHECKDB, times it, drops it. |
| Restore rehearsal | `restore-check.sh` | Before a release, or to demonstrate | Backs up, restores into `FPMS_RestoreCheck` and compares row counts table by table. Prints `RESTORE CHECK PASSED`. |
| Restore | `restore.sh` | When needed | Restores a backup folder, optionally with the log chain and a point in time. |

**Alerts the health check raises**

| Alert | Severity | When |
| --- | --- | --- |
| Full backup | Critical | No full backup recorded, or the last one is older than 26 hours |
| Log backup | Critical | No log backup recorded, or the last one is older than 1 hour |
| Run failed | Critical | The latest run of any job failed (clears at its next success) |
| Integrity check | Warning | None in the last 7 days |
| Off-site copy | Warning | None in the last 7 days |

**Retention:** every full backup from the last 14 days, Sunday backups for 12 weeks, and the first backup of each month for 5 years (financial records). Log backups are kept 14 days, never deleting any that a kept full backup still needs. Nothing is deleted while there are fewer than 3 full backups.

### Settings

The scripts read these environment variables:

| Variable | Default | Purpose |
| --- | --- | --- |
| `FPMS_DB` | `FPMS` | Database to back up |
| `FPMS_SQL_MODE` | `docker` | `docker` (SQL Server in this repo's container) or `local` (SQL Server installed on the machine) |
| `MSSQL_SA_PASSWORD` | read from `aspnet-core/docker/sqlserver/.env` | `sa` password |
| `FPMS_BACKUP_ROOT` | `~/FPMS-backups` | Where backups are written |
| `FPMS_OFFSITE_DIR`, `FPMS_OFFSITE_PASSPHRASE` | not set | Off-site copy folder and encryption passphrase |
| `FPMS_SQL_DATA_DIR` | not set | SQL Server data folder; needed by `restore.sh` in `local` mode |

Backup compression is used automatically, except on SQL Server Express, which does not support it.

### In a Codespace

The start script runs `scripts/backup/scheduler.sh` in the background. It:

- takes a full backup straight away, then one every 24 hours, followed by retention
- takes a log backup straight after the first full backup, then every 30 minutes
- runs the integrity check straight away, then once a week
- makes the off-site copy once a week, if `FPMS_OFFSITE_DIR` and `FPMS_OFFSITE_PASSPHRASE` are set

Its output goes to `/tmp/backup.log`, and backups go to `~/FPMS-backups`. To turn it off, start with `FPMS_BACKUPS=0 bash .devcontainer/start-fpms.sh`.

To turn on the off-site copy, add two lines to `.devcontainer/local.env` and rerun the start script:

```bash
FPMS_OFFSITE_DIR=/workspaces/FPMS-offsite
FPMS_OFFSITE_PASSPHRASE=<choose a long passphrase>
```

Backups in a Codespace live inside that Codespace. Deleting the Codespace deletes them. Download a backup folder (right-click it in the VS Code Explorer → **Download**) if you need to keep it.

**Run a job by hand** (from the repository root):

```bash
bash scripts/backup/backup.sh
bash scripts/backup/backup-log.sh
bash scripts/backup/integrity-check.sh
bash scripts/backup/restore-check.sh
bash scripts/backup/restore-test.sh
```

**Demonstrate backup and recovery** (for example in a presentation):

1. Sign in as `admin` and open **System → System health**. A minute or two after the Codespace starts, the scheduler has recorded a full backup, a log backup and an integrity check, so the only backup alert left is the off-site copy warning (unless you turned the off-site copy on).
2. Run `bash scripts/backup/restore-check.sh`. It backs up the live database, restores the copy into `FPMS_RestoreCheck` and shows the table-by-table comparison ending in `RESTORE CHECK PASSED`.
3. Run `bash scripts/backup/restore-test.sh`. It prints `RESTORE TEST PASSED in <n>s`, which is how long a real recovery takes.
4. Refresh **System health**: the new full backup, log backup and restore test runs are listed.
5. Optionally, show point-in-time recovery (next section) after deleting a record by mistake.

Afterwards drop the rehearsal database: `DROP DATABASE [FPMS_RestoreCheck]` (see [Database](#database) for opening a SQL prompt).

### Restoring

Restoring over the live database replaces everything in it. Data entered after the restore point has to be re-entered.

1. Tell users the system is going offline.
2. Stop the scheduler and the API:

   ```bash
   pkill -f scripts/backup/scheduler.sh; pkill -f Capstone.Web.Host
   ```

3. Choose a backup folder, then restore. The script checks `SHA256SUMS` first.

   ```bash
   ls ~/FPMS-backups

   bash scripts/backup/restore.sh ~/FPMS-backups/<timestamp>

   bash scripts/backup/restore.sh ~/FPMS-backups/<timestamp> --logs ~/FPMS-backups/logs

   bash scripts/backup/restore.sh ~/FPMS-backups/<timestamp> --logs ~/FPMS-backups/logs --stopat '2026-11-06 14:25:00'
   ```

   In order:
   - the first command lists the backup folders
   - the second restores to the time of that full backup
   - the third also replays every log backup after it, which loses at most about 30 minutes
   - the fourth stops replaying at a moment in time, for example just before a mistake

   `--stopat` uses the SQL Server's clock, which is UTC in the Codespace container.

4. Start everything again: `bash .devcontainer/start-fpms.sh`. The scheduler takes a new full backup straight away, which starts a new log chain.
5. Sign in as a Farm Owner and check the dashboard, a batch, a report and the compliance records.
6. Note the restore (date, backup used, who did it, result) in the farm's maintenance log.

To restore into a different database instead, give its name after the folder: `bash scripts/backup/restore.sh <folder> FPMS_Copy`.

Do not restore a backup taken before a data-model change into the rebuilt database (see [Changing the data model](#changing-the-data-model)); the tables no longer match. Restore it under another name if you need to look at the old data.

### On a farm server

Backups must go to a separate drive (for example an external drive), never the same disk as the database. The off-site copy is kept away from the farm by the Farm Owner. Keep the passphrase somewhere other than the farm PC, or the copy cannot be opened after a disaster.

**Ubuntu** (SQL Server installed on the machine, `sqlcmd` from `mssql-tools18`):

1. Put the secrets in `/etc/fpms/backup.env` (`chmod 600`):

   ```bash
   export MSSQL_SA_PASSWORD='<sa password>'
   export FPMS_OFFSITE_PASSPHRASE='<passphrase>'
   ```

2. Install the schedule in `scripts/backup/schedule/crontab.example` with `crontab -e`, adjusting the paths. It runs log backups every 30 minutes, the full backup at 20:00, retention at 20:45, the integrity check on Sundays at 21:00, the off-site copy on Sundays at 22:00, and a restore test each quarter.
3. For restores, also set `FPMS_SQL_MODE=local` and `FPMS_SQL_DATA_DIR=/var/opt/mssql/data`.

**Windows with SQL Server Express** (no SQL Agent, so Task Scheduler is used):

1. Copy the repository to the farm PC, for example `C:\FPMS`.
2. As Administrator, register the tasks:

   ```powershell
   powershell -File C:\FPMS\scripts\backup\windows\register-tasks.ps1 -ScriptPath C:\FPMS\scripts\backup\windows\fpms-jobs.ps1 -BackupRoot E:\FPMS-backups -OffsiteDir F:\FPMS-offsite
   setx /M FPMS_OFFSITE_PASSPHRASE "<passphrase>"
   ```

   This creates the same schedule as the Ubuntu one, minus the restore test. The off-site copy needs 7-Zip on the `PATH`. The SQL Server service account needs write access to the backup folder.

3. Run one job by hand to check it: `powershell -File C:\FPMS\scripts\backup\windows\fpms-jobs.ps1 -Job Full`.
4. To restore, stop the FPMS API first, then:

   ```powershell
   sqlcmd -S .\SQLEXPRESS -E -b -Q "ALTER DATABASE [FPMS] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; RESTORE DATABASE [FPMS] FROM DISK = N'E:\FPMS-backups\<timestamp>\FPMS.bak' WITH REPLACE, CHECKSUM; ALTER DATABASE [FPMS] SET MULTI_USER"
   ```

### HTTPS certificate

On a farm server the health check also warns 30 days before the HTTPS certificate expires. `scripts/backup/renew-certificate.sh` helps renew it:

```bash
bash scripts/backup/renew-certificate.sh csr fpms.farm.local
FPMS_CERT_PASSWORD='<password>' bash scripts/backup/renew-certificate.sh install cert.pem fpms.farm.local.key.pem fpms.pfx
FPMS_CERT_PASSWORD='<password>' bash scripts/backup/renew-certificate.sh check fpms.pfx
```

In order:

1. The first command makes a private key and a certificate request (CSR). Send the request to the issuer.
2. The second bundles the issued certificate for the API.
3. The third shows the new expiry date.

Then point the API at the `.pfx` (`Kestrel__Certificates__Default__Path` and `Kestrel__Certificates__Default__Password`) and restart it. A Codespace uses GitHub's certificate, so this does not apply there.

## Logs

| Log | Location |
| --- | --- |
| API console output | `/tmp/api.log` |
| Angular dev server | `/tmp/angular.log` |
| Last Migrator run | `/tmp/migrator.log` |
| Read-committed snapshot step | `/tmp/rcsi.log` |
| Backup scheduler and backup jobs | `/tmp/backup.log` |
| API file log (log4net, rolling, 30 files kept) | `aspnet-core/src/Capstone.Web.Host/App_Data/Logs/Logs.txt` |

Follow the API log live:

```bash
tail -f /tmp/api.log
```

Inside the app, the System Administrator can see audit, security and error entries under **System → Security and error logs**, and database and system status under **System → System health**. Audit log entries older than 12 months are purged automatically each day.

## Running without a Codespace

**VS Code Dev Container:** open the folder in VS Code with the Dev Containers extension and choose **Reopen in Container**. Everything works as in a Codespace, with `localhost` URLs.

**Directly on your machine** (Linux, macOS or WSL) you need Docker, .NET 9 SDK, Node 22 with Corepack/Yarn, `openssl` and `curl`. Then:

```bash
cd angular && corepack enable && yarn install && cd ..
bash .devcontainer/start-fpms.sh
```

The app is at `http://localhost:4200` and the API at `https://localhost:44311`. Run `dotnet dev-certs https --trust` once if the browser rejects the API certificate.

## Production build

There is no deployment target in this repository. To check that release builds work:

```bash
cd angular && npx ng build --configuration production
cd ../aspnet-core
dotnet publish src/Capstone.Web.Host -c Release -o ../out/api
dotnet publish src/Capstone.Migrator -c Release -o ../out/migrator
```

The production Angular build uses `src/assets/appconfig.production.json`, and a non-Development API uses `log4net.Production.config` and has no Swagger page.

## Troubleshooting

### The app URL shows nothing or "502"

The API or Angular may still be starting, or crashed. Check:

```bash
tail -n 50 /tmp/api.log /tmp/angular.log
curl -sk https://localhost:44311/health
```

Then rerun `bash .devcontainer/start-fpms.sh`.

### "Migration failed. See /tmp/migrator.log"

Read `/tmp/migrator.log`. Common causes:

- SQL Server was still starting. Rerun the start script.
- "There is already an object named ...": the database was built from an older `Initial` migration. Delete the `Migrations` folder and rerun the script (see [Changing the data model](#changing-the-data-model)).
- Login failed for `sa`: see the next section.

### SQL Server will not start or login fails

The volume was created with a different `sa` password than the one now in `local.env` (for example after deleting `local.env`). Either restore the old `local.env`, or wipe SQL Server with `docker compose down -v` as shown under [Database](#database).

Also check `docker compose logs sqlserver` in `aspnet-core/docker/sqlserver`. SQL Server needs about 2 GB of memory; the Codespace asks for 8 GB.

### "Docker is not running."

Docker-in-Docker takes a moment after the Codespace starts. Wait a few seconds and rerun the start script. If it keeps failing, rebuild the container (**Codespaces: Rebuild Container**).

### API refuses to start: JwtBearer SecurityKey must be set

The JWT key is missing. Make sure `local.env` contains `JWT_KEY` and start through the script rather than `dotnet run` on its own.

### Sign-in works but API calls fail with CORS errors

`App__CorsOrigins` must match the app URL. The script sets it from the Codespace name, so rerun the script after opening the Codespace in a new browser session or a different port setup.

### Forgot the admin password

The original password in `local.env` stops working after the first-sign-in change. Either reset the database (admin is recreated with `ADMIN_PASSWORD`), or update the password hash in `AbpUsers` with SQL if you need to keep the data.

### Farm user locked out or forgot password

Sign in as `admin` and use **System → Account recovery**.
