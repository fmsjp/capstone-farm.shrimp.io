using Abp;
using Abp.Auditing;
using Abp.Localization;
using Abp.Modules;
using Abp.Reflection.Extensions;
using Abp.Runtime.Security;
using Abp.Timing;
using Abp.Zero;
using Abp.Zero.Configuration;
using Capstone.Authorization;
using Capstone.Authorization.Roles;
using Capstone.Authorization.Users;
using Capstone.Configuration;
using Capstone.Domain;
using Capstone.Farms;
using Capstone.Localization;
using Capstone.MultiTenancy;
using Capstone.Compliance;
using Capstone.Harvests;
using Capstone.Inventory;
using Capstone.Ponds;
using Capstone.Production;
using Capstone.Resources;
using Capstone.Sales;
using Capstone.Timing;
using System;
using System.Linq;

namespace Capstone;

[DependsOn(typeof(AbpZeroCoreModule))]
public class CapstoneCoreModule : AbpModule
{
    private static readonly Type[] TrackedEntityTypes =
    {
        typeof(FarmProfile), typeof(HatcherySource), typeof(HatcheryCertificate), typeof(InventoryItem), typeof(InventoryLot), typeof(Pond),
        typeof(FryDelivery), typeof(ProductionCycle), typeof(PondBatch), typeof(BatchFailure), typeof(Harvest), typeof(Sale), typeof(ComplianceDocument),
    };

    public override void PreInitialize()
    {
        Clock.Provider = ClockProviders.Utc;

        Configuration.Auditing.IsEnabledForAnonymousUsers = true;
        Configuration.Auditing.SaveReturnValues = false;

        Configuration.EntityHistory.IsEnabled = true;
        Configuration.EntityHistory.Selectors.Add(new NamedTypeSelector(
            "Fpms.TrackedEntities",
            type => TrackedEntityTypes.Contains(type)));

        Configuration.Modules.Zero().EntityTypes.Tenant = typeof(Tenant);
        Configuration.Modules.Zero().EntityTypes.Role = typeof(Role);
        Configuration.Modules.Zero().EntityTypes.User = typeof(User);

        CapstoneLocalizationConfigurer.Configure(Configuration.Localization);

        Configuration.MultiTenancy.IsEnabled = CapstoneConsts.MultiTenancyEnabled;

        AppRoleConfig.Configure(Configuration.Modules.Zero().RoleManagement);
        Configuration.Authorization.Providers.Add<CapstoneAuthorizationProvider>();

        Configuration.Settings.Providers.Add<AppSettingProvider>();

        Configuration.Settings.SettingEncryptionConfiguration.DefaultPassPhrase = CapstoneConsts.DefaultPassPhrase;
        SimpleStringCipher.DefaultPassPhrase = CapstoneConsts.DefaultPassPhrase;
    }

    public override void Initialize()
    {
        IocManager.RegisterAssemblyByConvention(typeof(CapstoneCoreModule).GetAssembly());
    }

    public override void PostInitialize()
    {
        IocManager.Resolve<AppTimes>().StartupTime = Clock.Now;
    }
}
