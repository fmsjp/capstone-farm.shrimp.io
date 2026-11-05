using Abp.Application.Services.Dto;
using Abp.Auditing;
using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Capstone.Authorization;
using Capstone.Common;
using Capstone.Domain;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Capstone.Health;

public class BackupRunDto
{
    public long Id { get; set; }
    public BackupKind Kind { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public bool Succeeded { get; set; }
    public string FileName { get; set; }
    public long? SizeBytes { get; set; }
    public string Message { get; set; }
    public string Host { get; set; }
}

public class SystemHealthDto
{
    public string AppVersion { get; set; }
    public DateTime BuildDateUtc { get; set; }
    public DateTime ServerTimeUtc { get; set; }
    public string TimeZone { get; set; }
    public double? DatabaseSizeGb { get; set; }
    public double? FreeDiskGb { get; set; }
    public double? FreeDiskPct { get; set; }
    public DateTime? LastFullBackupUtc { get; set; }
    public DateTime? LastLogBackupUtc { get; set; }
    public DateTime? LastIntegrityCheckUtc { get; set; }
    public DateTime? LastRestoreTestUtc { get; set; }
    public DateTime? LastOffsiteCopyUtc { get; set; }
    public DateTime? CertificateExpiresUtc { get; set; }
    public int ErrorsLast24Hours { get; set; }
    public List<HealthAlertDto> Alerts { get; set; } = new();
}

public class BackupRunsInput : PagedInput
{
    public BackupKind? Kind { get; set; }
    public bool FailedOnly { get; set; }
}

public class HealthFactsReader : Abp.Dependency.ITransientDependency
{
    private readonly IRepository<BackupRun, long> _runs;
    private readonly IRepository<AuditLog, long> _auditLogs;
    private readonly IDatabaseMetrics _metrics;
    private readonly IUnitOfWorkManager _uow;

    public HealthFactsReader(IRepository<BackupRun, long> runs, IRepository<AuditLog, long> auditLogs, IDatabaseMetrics metrics, IUnitOfWorkManager uow)
    {
        _runs = runs;
        _auditLogs = auditLogs;
        _metrics = metrics;
        _uow = uow;
    }

    public async Task<(HealthFacts Facts, SystemHealthDto Dto)> ReadAsync()
    {
        var now = DateTime.UtcNow;
        var runs = await _runs.GetAll().AsNoTracking().Where(r => r.StartedAtUtc > now.AddDays(-60)).ToListAsync();

        DateTime? LastOk(BackupKind kind) => runs.Where(r => r.Kind == kind && r.Succeeded).Select(r => (DateTime?)(r.FinishedAtUtc ?? r.StartedAtUtc)).Max();

        var latestPerKind = runs.GroupBy(r => r.Kind).Select(g => g.OrderByDescending(r => r.StartedAtUtc).First()).ToList();
        var metrics = await _metrics.GetAsync();

        int errors;
        using (_uow.Current.DisableFilter(AbpDataFilters.MayHaveTenant))
        {
            var since = now.AddHours(-24);
            errors = await _auditLogs.GetAll().AsNoTracking().CountAsync(a => a.ExecutionTime >= since && a.Exception != null);
        }

        var facts = new HealthFacts
        {
            NowUtc = now,
            DatabaseSizeGb = metrics.DataSizeGb,
            FreeDiskPct = metrics.FreeDiskPct,
            LastFullBackupUtc = LastOk(BackupKind.Full),
            LastLogBackupUtc = LastOk(BackupKind.Log),
            LastIntegrityCheckUtc = LastOk(BackupKind.IntegrityCheck),
            LastOffsiteCopyUtc = LastOk(BackupKind.OffsiteCopy),
            LatestRunPerKind = latestPerKind,
            CertificateExpiresUtc = CertificateExpiry(),
            ErrorsLast24Hours = errors,
        };

        var dto = new SystemHealthDto
        {
            AppVersion = AppVersionHelper.Version,
            BuildDateUtc = AppVersionHelper.ReleaseDate.ToUniversalTime(),
            ServerTimeUtc = now,
            TimeZone = FarmClock.DefaultTimeZone,
            DatabaseSizeGb = metrics.DataSizeGb is { } g ? Math.Round(g, 2) : null,
            FreeDiskGb = metrics.FreeDiskGb is { } d ? Math.Round(d, 1) : null,
            FreeDiskPct = metrics.FreeDiskPct is { } p ? Math.Round(p, 1) : null,
            LastFullBackupUtc = facts.LastFullBackupUtc,
            LastLogBackupUtc = facts.LastLogBackupUtc,
            LastIntegrityCheckUtc = facts.LastIntegrityCheckUtc,
            LastRestoreTestUtc = LastOk(BackupKind.RestoreTest),
            LastOffsiteCopyUtc = facts.LastOffsiteCopyUtc,
            CertificateExpiresUtc = facts.CertificateExpiresUtc,
            ErrorsLast24Hours = errors,
            Alerts = HealthEvaluator.Evaluate(facts),
        };
        return (facts, dto);
    }

    internal static DateTime? CertificateExpiry()
    {
        try
        {
            var path = Environment.GetEnvironmentVariable("FPMS_CERT_PATH") ?? Environment.GetEnvironmentVariable("Kestrel__Certificates__Default__Path");
            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
            {
                return null;
            }

            var password = Environment.GetEnvironmentVariable("FPMS_CERT_PASSWORD") ?? Environment.GetEnvironmentVariable("Kestrel__Certificates__Default__Password");
            using var cert = new X509Certificate2(path, password);
            return cert.NotAfter.ToUniversalTime();
        }
        catch (Exception)
        {
            return null;
        }
    }
}

[AbpAuthorize(PermissionNames.System_Health)]
public class SystemHealthAppService : CapstoneAppServiceBase
{
    private readonly HealthFactsReader _reader;
    private readonly IRepository<BackupRun, long> _runs;

    public SystemHealthAppService(HealthFactsReader reader, IRepository<BackupRun, long> runs)
    {
        _reader = reader;
        _runs = runs;
    }

    public virtual async Task<SystemHealthDto> GetHealth() => (await _reader.ReadAsync()).Dto;

    public virtual async Task<PagedResultDto<BackupRunDto>> GetBackupRuns(BackupRunsInput input)
    {
        var query = _runs.GetAll().AsNoTracking()
            .Where(r => !input.Kind.HasValue || r.Kind == input.Kind)
            .Where(r => !input.FailedOnly || !r.Succeeded);
        var sort = SortSpec.Parse(input.Sorting, new[] { "startedAt", "kind", "succeeded", "sizeBytes" }, "startedAt", true);
        var ordered = sort.Key switch
        {
            "kind" => sort.Apply(query, r => r.Kind),
            "succeeded" => sort.Apply(query, r => r.Succeeded),
            "sizeBytes" => sort.Apply(query, r => r.SizeBytes),
            _ => sort.Apply(query, r => r.StartedAtUtc),
        };
        return await ordered.ThenByDescending(r => r.Id).Select(r => new BackupRunDto
        {
            Id = r.Id, Kind = r.Kind, StartedAtUtc = r.StartedAtUtc, FinishedAtUtc = r.FinishedAtUtc, Succeeded = r.Succeeded,
            FileName = r.FileName, SizeBytes = r.SizeBytes, Message = r.Message, Host = r.Host,
        }).ToPageAsync(input);
    }
}

public class HealthBannerDto
{
    public bool NeedsAttention { get; set; }

    public string Message { get; set; }

    public List<string> Details { get; set; } = new();
}

[AbpAuthorize]
public class HealthBannerAppService : CapstoneAppServiceBase
{
    private readonly HealthFactsReader _reader;

    public HealthBannerAppService(HealthFactsReader reader)
    {
        _reader = reader;
    }

    public virtual async Task<HealthBannerDto> Get()
    {
        var isAdmin = await IsGrantedAsync(PermissionNames.System_Health);
        if (!isAdmin && !await IsGrantedAsync(PermissionNames.Dashboard_Full))
        {
            return new HealthBannerDto();
        }

        var (_, dto) = await _reader.ReadAsync();
        var critical = dto.Alerts.Where(a => a.Severity == AlertSeverity.Critical).ToList();
        if (critical.Count == 0)
        {
            return new HealthBannerDto();
        }

        return new HealthBannerDto
        {
            NeedsAttention = true,
            Message = isAdmin
                ? $"{critical.Count} system alert{(critical.Count == 1 ? "" : "s")} need attention."
                : "The system's backups or storage need attention. Please tell the System Administrator.",
            Details = isAdmin ? critical.Select(a => a.Message).ToList() : new List<string>(),
        };
    }
}
