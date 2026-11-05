using Abp.MultiTenancy;
using Abp.Zero.Configuration;

namespace Capstone.Authorization.Roles;

public static class AppRoleConfig
{
    public static void Configure(IRoleManagementConfig roleManagementConfig)
    {
        roleManagementConfig.StaticRoles.Add(
            new StaticRoleDefinition(StaticRoleNames.Host.SystemAdministrator, MultiTenancySides.Host, grantAllPermissionsByDefault: false));

        roleManagementConfig.StaticRoles.Add(
            new StaticRoleDefinition(StaticRoleNames.Tenants.FarmOwner, MultiTenancySides.Tenant, grantAllPermissionsByDefault: false));
        roleManagementConfig.StaticRoles.Add(
            new StaticRoleDefinition(StaticRoleNames.Tenants.FarmTechnician, MultiTenancySides.Tenant, grantAllPermissionsByDefault: false));
    }
}
