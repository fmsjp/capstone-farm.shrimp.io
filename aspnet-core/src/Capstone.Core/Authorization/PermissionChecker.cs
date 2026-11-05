using Abp.Authorization;
using Capstone.Authorization.Roles;
using Capstone.Authorization.Users;

namespace Capstone.Authorization;

public class PermissionChecker : PermissionChecker<Role, User>
{
    public PermissionChecker(UserManager userManager)
        : base(userManager)
    {
    }
}
