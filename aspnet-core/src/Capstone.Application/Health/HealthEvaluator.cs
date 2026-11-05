using Capstone.Health;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Capstone.Health;

public enum AlertSeverity { Warning = 1, Critical = 2 }

public class HealthAlertDto
{
    public string Code { get; set; }
    public AlertSeverity Severity { get; set; }
    public string Message { get; set; }
}

public class HealthFacts
{
    public DateTime NowUtc { get; set; }
    public double? DatabaseSizeGb { get; set; }
    public double? FreeDiskPct { get; set; }
    public DateTime? LastFullBackupUtc { get; set; }
    public DateTime? LastLogBackupUtc { get; set; }
    public DateTime? LastIntegrityCheckUtc { get; set; }
    public DateTime? LastOffsiteCopyUtc { get; set; }
    public List<BackupRun> LatestRunPerKind { get; set; } = new();
    public DateTime? CertificateExpiresUtc { get; set; }
    public int ErrorsLast24Hours { get; set; }
}

public static class HealthEvaluator
{
    public static readonly TimeSpan LogBackupMaxAge = TimeSpan.FromHours(1);
    public static readonly TimeSpan FullBackupMaxAge = TimeSpan.FromHours(26);
    public static readonly TimeSpan WeeklyJobMaxAge = TimeSpan.FromDays(8);
    public const double DatabaseWarnGb = 7;
    public const double DatabaseCriticalGb = 9;
    public const double FreeDiskCriticalPct = 15;
    public const int CertificateWarnDays = 30;
    public const int ErrorWarnCount = 25;

    public static List<HealthAlertDto> Evaluate(HealthFacts f)
    {
        var alerts = new List<HealthAlertDto>();

        void Add(string code, AlertSeverity severity, string message) => alerts.Add(new HealthAlertDto { Code = code, Severity = severity, Message = message });

        string Age(DateTime when) => (f.NowUtc - when).TotalHours >= 48 ? $"{(int)(f.NowUtc - when).TotalDays} days" : (f.NowUtc - when).TotalHours >= 2 ? $"{(int)(f.NowUtc - when).TotalHours} hours" : $"{(int)(f.NowUtc - when).TotalMinutes} minutes";

        if (f.LastLogBackupUtc == null)
        {
            Add("LOG_BACKUP", AlertSeverity.Critical, "No transaction-log backup has been recorded. Until one runs, a failure could lose everything since the last full backup.");
        }
        else if (f.NowUtc - f.LastLogBackupUtc > LogBackupMaxAge)
        {
            Add("LOG_BACKUP", AlertSeverity.Critical, $"The last transaction-log backup was {Age(f.LastLogBackupUtc.Value)} ago (it should run every 30 minutes).");
        }

        if (f.LastFullBackupUtc == null)
        {
            Add("FULL_BACKUP", AlertSeverity.Critical, "No full backup has been recorded.");
        }
        else if (f.NowUtc - f.LastFullBackupUtc > FullBackupMaxAge)
        {
            Add("FULL_BACKUP", AlertSeverity.Critical, $"The last full backup was {Age(f.LastFullBackupUtc.Value)} ago (it should run daily).");
        }

        foreach (var run in f.LatestRunPerKind.Where(r => !r.Succeeded))
        {
            Add("RUN_FAILED_" + run.Kind.ToString().ToUpperInvariant(), AlertSeverity.Critical,
                $"The latest {KindLabel(run.Kind)} failed {Age(run.StartedAtUtc)} ago{(string.IsNullOrWhiteSpace(run.Message) ? "." : ": " + run.Message.Trim())}");
        }

        if (f.LastIntegrityCheckUtc == null || f.NowUtc - f.LastIntegrityCheckUtc > WeeklyJobMaxAge)
        {
            Add("INTEGRITY_CHECK", AlertSeverity.Warning, f.LastIntegrityCheckUtc == null
                ? "No database integrity check (CHECKDB) has been recorded."
                : $"The last database integrity check was {Age(f.LastIntegrityCheckUtc.Value)} ago (it should run weekly).");
        }

        if (f.LastOffsiteCopyUtc == null || f.NowUtc - f.LastOffsiteCopyUtc > WeeklyJobMaxAge)
        {
            Add("OFFSITE_COPY", AlertSeverity.Warning, f.LastOffsiteCopyUtc == null
                ? "No off-site copy of the backups has been recorded."
                : $"The last off-site copy was {Age(f.LastOffsiteCopyUtc.Value)} ago (it should run weekly).");
        }

        if (f.DatabaseSizeGb >= DatabaseCriticalGb)
        {
            Add("DB_SIZE", AlertSeverity.Critical, $"The database is {f.DatabaseSizeGb:0.0} GB; SQL Server Express stops accepting data at 10 GB.");
        }
        else if (f.DatabaseSizeGb >= DatabaseWarnGb)
        {
            Add("DB_SIZE", AlertSeverity.Warning, $"The database is {f.DatabaseSizeGb:0.0} GB, close to the 10 GB limit of SQL Server Express.");
        }

        if (f.FreeDiskPct < FreeDiskCriticalPct)
        {
            Add("DISK_FREE", AlertSeverity.Critical, $"Only {f.FreeDiskPct:0.#}% of the database disk is free (alert below {FreeDiskCriticalPct}%).");
        }

        if (f.CertificateExpiresUtc != null)
        {
            var days = (int)Math.Floor((f.CertificateExpiresUtc.Value - f.NowUtc).TotalDays);
            if (days < 0)
            {
                Add("CERTIFICATE", AlertSeverity.Critical, "The HTTPS certificate has expired. Browsers will refuse to open the system.");
            }
            else if (days <= CertificateWarnDays)
            {
                Add("CERTIFICATE", AlertSeverity.Warning, $"The HTTPS certificate expires in {days} day{(days == 1 ? "" : "s")}. Renew it (runbook, certificate renewal).");
            }
        }

        if (f.ErrorsLast24Hours >= ErrorWarnCount)
        {
            Add("ERRORS", AlertSeverity.Warning, $"{f.ErrorsLast24Hours} server errors in the last 24 hours (see Security and error logs).");
        }

        return alerts.OrderByDescending(a => a.Severity).ThenBy(a => a.Code).ToList();
    }

    public static string KindLabel(BackupKind kind) => kind switch
    {
        BackupKind.Full => "full backup",
        BackupKind.Log => "transaction-log backup",
        BackupKind.IntegrityCheck => "integrity check",
        BackupKind.RestoreTest => "restore test",
        BackupKind.OffsiteCopy => "off-site copy",
        _ => kind.ToString(),
    };
}
