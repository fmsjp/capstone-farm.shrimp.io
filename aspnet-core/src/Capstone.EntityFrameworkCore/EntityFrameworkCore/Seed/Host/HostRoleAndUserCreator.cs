using Abp.Authorization;
using Abp.Authorization.Roles;
using Abp.Authorization.Users;
using Capstone.Authorization.Roles;
using Capstone.Authorization.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Linq;

namespace Capstone.EntityFrameworkCore.Seed.Host;

public class HostRoleAndUserCreator
{
    private readonly CapstoneDbContext _context;

    public HostRoleAndUserCreator(CapstoneDbContext context)
    {
        _context = context;
    }

    public void Create()
    {
        var role = _context.Roles.IgnoreQueryFilters()
            .FirstOrDefault(r => r.TenantId == null && r.Name == StaticRoleNames.Host.SystemAdministrator);
        if (role == null)
        {
            role = _context.Roles.Add(new Role(null, StaticRoleNames.Host.SystemAdministrator, "System Administrator")
            {
                IsStatic = true,
                IsDefault = false,
            }).Entity;
            _context.SaveChanges();
        }

        var wanted = FarmRolePermissions.SystemAdministrator.ToList();
        var existing = _context.Permissions.IgnoreQueryFilters()
            .OfType<RolePermissionSetting>()
            .Where(p => p.TenantId == null && p.RoleId == role.Id)
            .ToList();
        _context.Permissions.RemoveRange(existing.Where(p => !wanted.Contains(p.Name)));
        foreach (var name in wanted.Where(n => existing.All(p => p.Name != n)))
        {
            _context.Permissions.Add(new RolePermissionSetting { TenantId = null, Name = name, IsGranted = true, RoleId = role.Id });
        }

        _context.SaveChanges();

        var legacy = _context.Roles.IgnoreQueryFilters().FirstOrDefault(r => r.TenantId == null && r.Name == StaticRoleNames.Host.Admin);
        if (legacy != null)
        {
            var legacyGrants = _context.Permissions.IgnoreQueryFilters().OfType<RolePermissionSetting>()
                .Where(p => p.TenantId == null && p.RoleId == legacy.Id).ToList();
            _context.Permissions.RemoveRange(legacyGrants);
            var legacyLinks = _context.UserRoles.IgnoreQueryFilters().Where(ur => ur.TenantId == null && ur.RoleId == legacy.Id).ToList();
            _context.UserRoles.RemoveRange(legacyLinks);
            _context.SaveChanges();
        }

        var user = _context.Users.IgnoreQueryFilters().FirstOrDefault(u => u.TenantId == null && u.UserName == AbpUserBase.AdminUserName);
        if (user == null)
        {
            var configured = SeedOptions.SystemAdminInitialPassword;
            user = new User
            {
                TenantId = null,
                UserName = AbpUserBase.AdminUserName,
                Name = "System",
                Surname = "Administrator",
                EmailAddress = "sysadmin@fpms.local",
                IsEmailConfirmed = true,
                IsActive = true,
                IsLockoutEnabled = true,
                ShouldChangePasswordOnNextLogin = !string.IsNullOrWhiteSpace(configured),
            };
            var password = string.IsNullOrWhiteSpace(configured) ? SeedOptions.DevelopmentPassword : configured;
            user.Password = new PasswordHasher<User>(new OptionsWrapper<PasswordHasherOptions>(new PasswordHasherOptions())).HashPassword(user, password);
            user.SetNormalizedNames();
            user = _context.Users.Add(user).Entity;
            _context.SaveChanges();
        }
        else if (user.Name == "admin")
        {
            user.Name = "System";
            user.Surname = "Administrator";
            _context.SaveChanges();
        }

        if (!_context.UserRoles.IgnoreQueryFilters().Any(ur => ur.TenantId == null && ur.UserId == user.Id && ur.RoleId == role.Id))
        {
            _context.UserRoles.Add(new UserRole(null, user.Id, role.Id));
            _context.SaveChanges();
        }
    }
}
