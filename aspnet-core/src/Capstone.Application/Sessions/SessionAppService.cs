using Abp.Auditing;
using Capstone.Authorization.Roles;
using Capstone.Sessions.Dto;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Capstone.Sessions;

public class SessionAppService : CapstoneAppServiceBase, ISessionAppService
{
    [DisableAuditing]
    public virtual async Task<GetCurrentLoginInformationsOutput> GetCurrentLoginInformations()
    {
        var output = new GetCurrentLoginInformationsOutput
        {
            Application = new ApplicationInfoDto
            {
                Version = AppVersionHelper.Version,
                ReleaseDate = AppVersionHelper.ReleaseDate,
                Features = new Dictionary<string, bool>()
            }
        };

        if (AbpSession.TenantId.HasValue)
        {
            output.Tenant = ObjectMapper.Map<TenantLoginInfoDto>(await GetCurrentTenantAsync());
            var profile = await Farm.GetProfileAsync();
            output.Tenant.FarmName = string.IsNullOrWhiteSpace(profile.FarmName) ? output.Tenant.Name : profile.FarmName;
            output.Tenant.TimeZone = profile.TimeZone;
            output.Tenant.DateOrder = (int)profile.DateOrder;
            output.Tenant.Use24HourTime = profile.Use24HourTime;
        }

        if (AbpSession.UserId.HasValue)
        {
            var user = await GetCurrentUserAsync();
            output.User = ObjectMapper.Map<UserLoginInfoDto>(user);
            var roles = await UserManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault(r => r == StaticRoleNames.Tenants.FarmOwner)
                       ?? roles.FirstOrDefault(r => r == StaticRoleNames.Tenants.FarmTechnician)
                       ?? roles.FirstOrDefault(r => r == StaticRoleNames.Host.SystemAdministrator)
                       ?? roles.FirstOrDefault();
            output.User.RoleName = role;
            output.User.RoleDisplayName = role switch
            {
                StaticRoleNames.Tenants.FarmOwner => "Farm Owner",
                StaticRoleNames.Tenants.FarmTechnician => "Farm Technician",
                StaticRoleNames.Host.SystemAdministrator => "System Administrator",
                _ => role,
            };
        }

        return output;
    }
}
