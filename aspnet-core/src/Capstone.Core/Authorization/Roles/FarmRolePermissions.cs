using System.Collections.Generic;

namespace Capstone.Authorization.Roles;

public static class FarmRolePermissions
{
    public static readonly IReadOnlyList<string> FarmOwner = new[]
    {
        PermissionNames.Dashboard, PermissionNames.Dashboard_Full,
        PermissionNames.FarmProfile, PermissionNames.FarmProfile_Edit,
        PermissionNames.FarmUsers, PermissionNames.ActivityLog, PermissionNames.Compliance,
        PermissionNames.Ponds, PermissionNames.Ponds_Manage, PermissionNames.Ponds_Prepare,
        PermissionNames.Hatcheries, PermissionNames.Hatcheries_Manage,
        PermissionNames.FryDeliveries, PermissionNames.FryDeliveries_Manage, PermissionNames.FryDeliveries_UpdateStatus,
        PermissionNames.Buyers,
        PermissionNames.Inventory, PermissionNames.Inventory_Manage,
        PermissionNames.Production, PermissionNames.Production_Manage, PermissionNames.Production_Stock,
        PermissionNames.Logs, PermissionNames.Logs_Create, PermissionNames.Logs_EditAny,
        PermissionNames.Harvests, PermissionNames.Harvests_Create, PermissionNames.Harvests_EditAny,
        PermissionNames.Sales, PermissionNames.Reports,
        PermissionNames.ReferenceData,
    };

    public static readonly IReadOnlyList<string> FarmTechnician = new[]
    {
        PermissionNames.Dashboard,
        PermissionNames.FarmProfile,
        PermissionNames.Ponds, PermissionNames.Ponds_Prepare,
        PermissionNames.Hatcheries,
        PermissionNames.FryDeliveries, PermissionNames.FryDeliveries_UpdateStatus,
        PermissionNames.Inventory,
        PermissionNames.Production, PermissionNames.Production_Stock,
        PermissionNames.Logs, PermissionNames.Logs_Create,
        PermissionNames.Harvests, PermissionNames.Harvests_Create,
        PermissionNames.ReferenceData,
    };

    public static readonly IReadOnlyList<string> SystemAdministrator = new[]
    {
        PermissionNames.System, PermissionNames.System_Farms, PermissionNames.System_AccountRecovery,
        PermissionNames.System_ReferenceData, PermissionNames.System_Logs, PermissionNames.System_Health,
    };

    public static IReadOnlyList<string> For(string roleName) => roleName switch
    {
        StaticRoleNames.Tenants.FarmOwner => FarmOwner,
        StaticRoleNames.Tenants.FarmTechnician => FarmTechnician,
        StaticRoleNames.Host.SystemAdministrator => SystemAdministrator,
        _ => System.Array.Empty<string>(),
    };
}
