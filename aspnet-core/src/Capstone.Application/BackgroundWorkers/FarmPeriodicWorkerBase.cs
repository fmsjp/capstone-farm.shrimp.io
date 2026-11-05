using System;
using System.Collections.Generic;
using System.Linq;
using Abp.Dependency;
using Abp.Domain.Repositories;
using Abp.Threading.BackgroundWorkers;
using Abp.Threading.Timers;
using Capstone.MultiTenancy;

namespace Capstone.BackgroundWorkers;

public abstract class FarmPeriodicWorkerBase : PeriodicBackgroundWorkerBase, ISingletonDependency
{
    private readonly IRepository<Tenant> _tenantRepository;

    protected FarmPeriodicWorkerBase(AbpTimer timer, IRepository<Tenant> tenantRepository, TimeSpan period)
        : base(timer)
    {
        _tenantRepository = tenantRepository;
        Timer.Period = (int)period.TotalMilliseconds;
    }

    protected override void DoWork()
    {
        List<int> farmIds;
        using (var uow = UnitOfWorkManager.Begin())
        {
            farmIds = _tenantRepository.GetAll().Where(t => t.IsActive).Select(t => t.Id).ToList();
            uow.Complete();
        }

        foreach (var farmId in farmIds)
        {
            try
            {
                using var uow = UnitOfWorkManager.Begin();
                using (UnitOfWorkManager.Current.SetTenantId(farmId))
                {
                    DoWorkForFarm(farmId);
                }
                uow.Complete();
            }
            catch (Exception ex)
            {
                Logger.Error($"{GetType().Name} failed for farm {farmId}.", ex);
            }
        }
    }

    protected abstract void DoWorkForFarm(int farmId);
}
