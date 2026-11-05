using Abp.Authorization.Roles;
using Capstone.Authorization.Roles;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace Capstone.EntityFrameworkCore.Seed.Farms;

public class FarmRoleSynchronizer
{
    private readonly CapstoneDbContext _context;

    public FarmRoleSynchronizer(CapstoneDbContext context)
    {
        _context = context;
    }

    public void SyncAllFarms()
    {
        var tenantIds = _context.Tenants.IgnoreQueryFilters().Where(t => !t.IsDeleted).Select(t => t.Id).ToList();
        foreach (var tenantId in tenantIds)
        {
            SyncFarm(tenantId);
        }
    }

    public void SyncFarm(int tenantId)
    {
        foreach (var roleName in new[] { StaticRoleNames.Tenants.FarmOwner, StaticRoleNames.Tenants.FarmTechnician })
        {
            var role = EnsureRole(tenantId, roleName);
            var wanted = FarmRolePermissions.For(roleName).ToList();
            var existing = _context.Permissions.IgnoreQueryFilters().OfType<RolePermissionSetting>()
                .Where(p => p.TenantId == tenantId && p.RoleId == role.Id)
                .ToList();
            _context.Permissions.RemoveRange(existing.Where(p => !wanted.Contains(p.Name)));
            foreach (var name in wanted.Where(n => existing.All(p => p.Name != n)))
            {
                _context.Permissions.Add(new RolePermissionSetting { TenantId = tenantId, RoleId = role.Id, Name = name, IsGranted = true });
            }
        }

        _context.SaveChanges();
    }

    public Role EnsureRole(int tenantId, string roleName)
    {
        var role = _context.Roles.IgnoreQueryFilters().FirstOrDefault(r => r.TenantId == tenantId && r.Name == roleName);
        if (role == null)
        {
            var display = roleName == StaticRoleNames.Tenants.FarmOwner ? "Farm Owner" : "Farm Technician";
            role = _context.Roles.Add(new Role(tenantId, roleName, display) { IsStatic = true }).Entity;
            _context.SaveChanges();
        }

        return role;
    }
}
