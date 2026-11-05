using System.Linq;
using Abp.AutoMapper;
using Abp.Modules;
using Abp.Reflection.Extensions;
using Abp.Threading.BackgroundWorkers;
using Capstone.Authorization;
using Capstone.BackgroundWorkers;

namespace Capstone;

[DependsOn(
    typeof(CapstoneCoreModule),
    typeof(AbpAutoMapperModule))]
public class CapstoneApplicationModule : AbpModule
{
    public override void PreInitialize()
    {
    }

    public override void Initialize()
    {
        var thisAssembly = typeof(CapstoneApplicationModule).GetAssembly();

        IocManager.RegisterAssemblyByConvention(thisAssembly);

        Configuration.Modules.AbpAutoMapper().Configurators.Add(
            cfg => cfg.AddMaps(thisAssembly)
        );
    }

    public override void PostInitialize()
    {
        var workerManager = IocManager.Resolve<IBackgroundWorkerManager>();
        var workerTypes = typeof(CapstoneApplicationModule).GetAssembly().GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(FarmPeriodicWorkerBase).IsAssignableFrom(t));
        foreach (var type in workerTypes)
        {
            workerManager.Add((IBackgroundWorker)IocManager.Resolve(type));
        }

        if (Configuration.BackgroundJobs.IsJobExecutionEnabled)
        {
            workerManager.Add(IocManager.Resolve<Capstone.Health.AuditLogPurgeWorker>());
        }
    }
}
