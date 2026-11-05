using Abp.Application.Services;
using Abp.Domain.Repositories;
using Abp.IdentityFramework;
using Abp.Runtime.Session;
using Capstone.Authorization;
using Capstone.Authorization.Users;
using Capstone.Farms;
using Capstone.MultiTenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Capstone;

public abstract class CapstoneAppServiceBase : ApplicationService
{
    public TenantManager TenantManager { get; set; }

    public UserManager UserManager { get; set; }

    public FarmContext Farm { get; set; }

    public IRepository<User, long> UserRepository { get; set; }

    protected CapstoneAppServiceBase()
    {
        LocalizationSourceName = CapstoneConsts.LocalizationSourceName;
    }

    protected virtual async Task<User> GetCurrentUserAsync()
    {
        var user = await UserManager.FindByIdAsync(AbpSession.GetUserId().ToString());
        if (user == null)
        {
            throw new Exception("There is no current user!");
        }

        return user;
    }

    protected virtual Task<Tenant> GetCurrentTenantAsync()
    {
        return TenantManager.GetByIdAsync(AbpSession.GetTenantId());
    }

    protected virtual void CheckErrors(IdentityResult identityResult)
    {
        identityResult.CheckErrors(LocalizationManager);
    }

    protected async Task<Dictionary<long, string>> GetUserNamesAsync(IEnumerable<long?> userIds)
    {
        var ids = userIds.Where(i => i.HasValue).Select(i => i.Value).Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<long, string>();
        }

        var users = await UserRepository.GetAll().AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.Name, u.Surname, u.UserName })
            .ToListAsync();
        return users.ToDictionary(u => u.Id, u => $"{u.Name} {u.Surname}".Trim());
    }

    protected static string NameOf(Dictionary<long, string> names, long? id) =>
        id.HasValue && names.TryGetValue(id.Value, out var name) ? name : null;

    protected async Task<bool> IsGrantedAsync(string permission) => await PermissionChecker.IsGrantedAsync(permission);

    protected Task<bool> CanEditAnyLogAsync() => IsGrantedAsync(PermissionNames.Logs_EditAny);
}
