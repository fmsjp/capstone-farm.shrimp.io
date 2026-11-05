using Abp.Auditing;
using Abp.Dependency;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Abp.Threading.BackgroundWorkers;
using Abp.Threading.Timers;
using System;
using System.Linq;

namespace Capstone.Health;

public class AuditLogPurgeWorker : PeriodicBackgroundWorkerBase, ISingletonDependency
{
    public const int RetentionMonths = 12;
    private const int BatchSize = 500;
    private readonly IRepository<AuditLog, long> _auditLogs;

    public AuditLogPurgeWorker(AbpTimer timer, IRepository<AuditLog, long> auditLogs) : base(timer)
    {
        _auditLogs = auditLogs;
        Timer.Period = (int)TimeSpan.FromHours(24).TotalMilliseconds;
    }

    protected override void DoWork()
    {
        try
        {
            Purge(DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            Logger.Error("The audit log purge failed.", ex);
        }
    }

    public int Purge(DateTime nowUtc)
    {
        var cutoff = nowUtc.AddMonths(-RetentionMonths);
        var deleted = 0;
        while (true)
        {
            using var uow = UnitOfWorkManager.Begin();
            using (UnitOfWorkManager.Current.DisableFilter(AbpDataFilters.MayHaveTenant))
            {
                var ids = _auditLogs.GetAll().Where(a => a.ExecutionTime < cutoff).OrderBy(a => a.Id).Select(a => a.Id).Take(BatchSize).ToList();
                if (ids.Count == 0)
                {
                    uow.Complete();
                    return deleted;
                }

                foreach (var id in ids)
                {
                    _auditLogs.Delete(id);
                }
            }

            uow.Complete();
            deleted += BatchSize;
            if (deleted >= 100_000)
            {
                return deleted;
            }
        }
    }
}
