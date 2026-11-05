# farm.shrimp.io — Farm Production Management System

A web system for managing production on *Penaeus vannamei* (whiteleg shrimp) farms in Sarangani and General Santos City. It covers ponds, fry deliveries, production cycles and pond batches, daily logbooks, inventory, harvests, sales, compliance documents and reports.

This repository is the clean development environment for the capstone project. It is set up to run in GitHub Codespaces.

## Tech stack

| Layer | Technology |
| --- | --- |
| Backend | ASP.NET Boilerplate 10.2.0 on .NET 9 (ASP.NET Core Web API) |
| Database | SQL Server 2022 (Docker), Entity Framework Core 9 |
| Frontend | Angular 19 (standalone components), AdminLTE / Bootstrap |
| Dev environment | GitHub Codespaces / Dev Container (Ubuntu 24.04, .NET 9, Node 22, Docker-in-Docker) |

## Quick start (Codespaces)

1. Optional: under **Settings → Secrets and variables → Codespaces**, add `SQL_SA_PASSWORD` and `ADMIN_INITIAL_PASSWORD`. If you leave them out, passwords are generated for you.
2. Click **Code → Codespaces → Create codespace on main**.
3. Wait for the start script to finish. On first start it installs packages, starts SQL Server, generates the database migration, creates and seeds the database, then starts the API and the Angular app. It prints:

   ```
   Database: FPMS
   App:     https://<codespace>-4200.app.github.dev
   Swagger: https://<codespace>-44311.app.github.dev/swagger/index.html
   Login:   admin / (see .devcontainer/local.env)
   Backups: /home/vscode/FPMS-backups (log: /tmp/backup.log)
   ```

4. Open the **App** URL. Sign in with a blank farm code, user name `admin`, and the `ADMIN_PASSWORD` from `.devcontainer/local.env`. You are asked to change the password at first sign-in.
5. Create a farm with **Register a farm account** on the login page, or from **System → Farms** as the administrator.

The same steps work in a local VS Code Dev Container. See [RUNBOOK.md](RUNBOOK.md) for running without a container, resetting the database, backups and troubleshooting.

## User roles

| Role | Scope | Typical work |
| --- | --- | --- |
| System Administrator | Whole system (no farm code) | Farms, account recovery, reference data, system health, security and error logs |
| Farm Owner | One farm | Farm profile and standards, users, ponds, resources, production, harvests, sales, reports |
| Farm Technician | One farm | Daily operations: logbooks, feeding, water quality, growth sampling, mortality |

Each farm is a separate tenant, so farm data is isolated. Users sign in with their farm code and user name.

## Repository layout

```
.devcontainer/          Codespace definition and start script (start-fpms.sh)
.config/                .NET tool manifest (dotnet-ef)
scripts/backup/         Backup, restore and health-check scripts (bash for Linux, PowerShell for Windows)
angular/                Angular client
  src/app/fpms/         Feature pages (dashboard, ponds, production, logbook, harvest, sales, reports, system, ...)
  src/shared/           Shared services, UI helpers, layout and theme
aspnet-core/
  Capstone.sln
  docker/sqlserver/     SQL Server docker-compose file
  src/
    Capstone.Core/                  Domain entities, rules and domain services
    Capstone.Application/           Application services (the API) and DTOs
    Capstone.EntityFrameworkCore/   DbContext, model configuration, seeders
    Capstone.Web.Core/              Authentication, token controller, web module
    Capstone.Web.Host/              API host (Swagger, health check, startup)
    Capstone.Migrator/              Console app that creates/upgrades and seeds the database
```

## Seed data

A new database contains only:

- the System Administrator account (`admin`)
- reference data: editions, languages, settings, water-quality parameter definitions and stocking density bands

There are no sample or test farms. See [RUNBOOK.md](RUNBOOK.md#seed-data) for adding a demo data seed.

## Backups

The database is backed up by the scripts in `scripts/backup/`: a full backup daily, a transaction-log backup every 30 minutes, a weekly integrity check and encrypted off-site copy, retention, and restore tests. In a Codespace they run automatically in the background. Every run is recorded in the database, and **System → System health** shows the runs and raises alerts when a backup is missing or failed.

See [RUNBOOK.md](RUNBOOK.md#backup-and-restore) for how it works, restoring (including point-in-time recovery), and setting it up on a farm server.

## Ports

| Port | Service |
| --- | --- |
| 4200 | Angular app (also proxies `/api`, `/AbpUserConfiguration` and `/signalr` to the API) |
| 44311 | API (HTTPS); Swagger at `/swagger`, health check at `/health` |
| 1433 | SQL Server |

## Notes

- Database migrations are not stored in the repository. A fresh `Initial` migration is generated from the current model when the Codespace starts, so the database always matches the code. Changing the model means rebuilding the database; see the runbook.
- Secrets never go in `appsettings.json`. They come from environment variables set by the start script or from Codespaces secrets.
