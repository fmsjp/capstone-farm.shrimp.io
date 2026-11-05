using Abp.Domain.Entities;
using System;

namespace Capstone.Health;

public enum BackupKind
{
    Full = 1,
    Log = 2,
    IntegrityCheck = 3,
    RestoreTest = 4,
    OffsiteCopy = 5,
}

public class BackupRun : Entity<long>
{
    public const int MaxFileNameLength = 260;
    public const int MaxMessageLength = 1000;
    public const int MaxHostLength = 100;

    public BackupKind Kind { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public bool Succeeded { get; set; }
    public string FileName { get; set; }
    public long? SizeBytes { get; set; }
    public string Message { get; set; }
    public string Host { get; set; }
}

public class DatabaseMetrics
{
    public double? DataSizeGb { get; set; }
    public double? FreeDiskGb { get; set; }
    public double? FreeDiskPct { get; set; }
}

public interface IDatabaseMetrics
{
    System.Threading.Tasks.Task<DatabaseMetrics> GetAsync();
}
