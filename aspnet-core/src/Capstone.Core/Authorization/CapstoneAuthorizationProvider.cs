using Abp.Authorization;
using Abp.Localization;
using Abp.MultiTenancy;

namespace Capstone.Authorization;

public class CapstoneAuthorizationProvider : AuthorizationProvider
{
    public override void SetPermissions(IPermissionDefinitionContext context)
    {
        context.CreatePermission(PermissionNames.Pages_Users, L("Users"), multiTenancySides: MultiTenancySides.Host);
        context.CreatePermission(PermissionNames.Pages_Users_Activation, L("UsersActivation"), multiTenancySides: MultiTenancySides.Host);
        context.CreatePermission(PermissionNames.Pages_Roles, L("Roles"), multiTenancySides: MultiTenancySides.Host);
        context.CreatePermission(PermissionNames.Pages_Tenants, L("Tenants"), multiTenancySides: MultiTenancySides.Host);

        const MultiTenancySides farm = MultiTenancySides.Tenant;

        var dashboard = context.CreatePermission(PermissionNames.Dashboard, F("Dashboard"), multiTenancySides: farm);
        dashboard.CreateChildPermission(PermissionNames.Dashboard_Full, F("Full dashboard"), multiTenancySides: farm);

        var profile = context.CreatePermission(PermissionNames.FarmProfile, F("Farm profile"), multiTenancySides: farm);
        profile.CreateChildPermission(PermissionNames.FarmProfile_Edit, F("Edit farm profile"), multiTenancySides: farm);
        context.CreatePermission(PermissionNames.FarmUsers, F("Farm user accounts"), multiTenancySides: farm);
        context.CreatePermission(PermissionNames.ActivityLog, F("Activity log"), multiTenancySides: farm);
        context.CreatePermission(PermissionNames.Compliance, F("Permits and licenses"), multiTenancySides: farm);

        var ponds = context.CreatePermission(PermissionNames.Ponds, F("Ponds"), multiTenancySides: farm);
        ponds.CreateChildPermission(PermissionNames.Ponds_Manage, F("Manage ponds"), multiTenancySides: farm);
        ponds.CreateChildPermission(PermissionNames.Ponds_Prepare, F("Record pond preparation"), multiTenancySides: farm);

        var hatcheries = context.CreatePermission(PermissionNames.Hatcheries, F("Hatchery sources"), multiTenancySides: farm);
        hatcheries.CreateChildPermission(PermissionNames.Hatcheries_Manage, F("Manage hatchery sources"), multiTenancySides: farm);

        var fry = context.CreatePermission(PermissionNames.FryDeliveries, F("Fry deliveries"), multiTenancySides: farm);
        fry.CreateChildPermission(PermissionNames.FryDeliveries_Manage, F("Record and edit fry deliveries"), multiTenancySides: farm);
        fry.CreateChildPermission(PermissionNames.FryDeliveries_UpdateStatus, F("Update fry delivery status"), multiTenancySides: farm);

        context.CreatePermission(PermissionNames.Buyers, F("Buyers"), multiTenancySides: farm);

        var inventory = context.CreatePermission(PermissionNames.Inventory, F("Inventory"), multiTenancySides: farm);
        inventory.CreateChildPermission(PermissionNames.Inventory_Manage, F("Manage inventory"), multiTenancySides: farm);

        var production = context.CreatePermission(PermissionNames.Production, F("Production cycles and batches"), multiTenancySides: farm);
        production.CreateChildPermission(PermissionNames.Production_Manage, F("Manage cycles and batches"), multiTenancySides: farm);
        production.CreateChildPermission(PermissionNames.Production_Stock, F("Create batches (stocking)"), multiTenancySides: farm);

        var logs = context.CreatePermission(PermissionNames.Logs, F("Logbooks"), multiTenancySides: farm);
        logs.CreateChildPermission(PermissionNames.Logs_Create, F("Record logbook entries"), multiTenancySides: farm);
        logs.CreateChildPermission(PermissionNames.Logs_EditAny, F("Edit any user's logbook entries"), multiTenancySides: farm);

        var harvests = context.CreatePermission(PermissionNames.Harvests, F("Harvests"), multiTenancySides: farm);
        harvests.CreateChildPermission(PermissionNames.Harvests_Create, F("Record harvests"), multiTenancySides: farm);
        harvests.CreateChildPermission(PermissionNames.Harvests_EditAny, F("Edit any user's harvests"), multiTenancySides: farm);

        context.CreatePermission(PermissionNames.Sales, F("Sales"), multiTenancySides: farm);
        context.CreatePermission(PermissionNames.Reports, F("Reports"), multiTenancySides: farm);
        context.CreatePermission(PermissionNames.ReferenceData, F("Read reference data"), multiTenancySides: farm);

        const MultiTenancySides host = MultiTenancySides.Host;
        var system = context.CreatePermission(PermissionNames.System, F("System administration"), multiTenancySides: host);
        system.CreateChildPermission(PermissionNames.System_Farms, F("Farm accounts and registration"), multiTenancySides: host);
        system.CreateChildPermission(PermissionNames.System_AccountRecovery, F("Account recovery"), multiTenancySides: host);
        system.CreateChildPermission(PermissionNames.System_ReferenceData, F("Reference data"), multiTenancySides: host);
        system.CreateChildPermission(PermissionNames.System_Logs, F("Security and error logs"), multiTenancySides: host);
        system.CreateChildPermission(PermissionNames.System_Health, F("System health and backups"), multiTenancySides: host);
    }

    private static ILocalizableString L(string name) => new LocalizableString(name, CapstoneConsts.LocalizationSourceName);

    private static ILocalizableString F(string text) => new FixedLocalizableString(text);
}
