using Abp.EntityFrameworkCore.Configuration;
using Abp.Modules;
using Abp.Reflection.Extensions;
using Abp.Zero.EntityFrameworkCore;
using Capstone.EntityFrameworkCore.Seed;

namespace Capstone.EntityFrameworkCore;

[DependsOn(
    typeof(CapstoneCoreModule),
    typeof(AbpZeroCoreEntityFrameworkCoreModule))]
public class CapstoneEntityFrameworkModule : AbpModule
{
    public bool SkipDbContextRegistration { get; set; }

    public bool SkipDbSeed { get; set; }

    public override void PreInitialize()
    {
        if (!SkipDbContextRegistration)
        {
            Configuration.Modules.AbpEfCore().AddDbContext<CapstoneDbContext>(options =>
            {
                if (options.ExistingConnection != null)
                {
                    CapstoneDbContextConfigurer.Configure(options.DbContextOptions, options.ExistingConnection);
                }
                else
                {
                    CapstoneDbContextConfigurer.Configure(options.DbContextOptions, options.ConnectionString);
                }
            });
        }
    }

    public override void Initialize()
    {
        IocManager.RegisterAssemblyByConvention(typeof(CapstoneEntityFrameworkModule).GetAssembly());
    }

    public override void PostInitialize()
    {
        if (!SkipDbSeed)
        {
            SeedHelper.SeedHostDb(IocManager);
        }
    }
}
