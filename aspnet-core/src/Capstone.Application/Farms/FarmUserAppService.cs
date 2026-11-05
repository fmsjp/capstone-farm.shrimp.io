using Abp.Authorization;
using Abp.Runtime.Session;
using Abp.UI;
using Capstone.Authorization;
using Capstone.Authorization.Roles;
using Capstone.Authorization.Users;
using Capstone.Farms.Dto;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Capstone.Farms;

[AbpAuthorize(PermissionNames.FarmUsers)]
public class FarmUserAppService : CapstoneAppServiceBase
{
    private readonly FarmProvisioningManager _provisioning;
    private readonly RoleManager _roleManager;
    private readonly AccountNameGenerator _names;

    public FarmUserAppService(FarmProvisioningManager provisioning, RoleManager roleManager, AccountNameGenerator names)
    {
        _provisioning = provisioning;
        _roleManager = roleManager;
        _names = names;
    }

    public virtual async Task<List<FarmUserDto>> GetAll()
    {
        var users = await UserRepository.GetAll().AsNoTracking().Include(u => u.Roles).OrderBy(u => u.Name).ThenBy(u => u.Surname).ToListAsync();
        var roles = await _roleManager.Roles.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.Name);
        var now = DateTime.UtcNow;
        return users.Select(u => new FarmUserDto
        {
            Id = u.Id,
            UserName = u.UserName,
            Name = u.Name,
            Surname = u.Surname,
            EmailAddress = u.EmailAddress,
            RoleName = u.Roles.Select(r => roles.TryGetValue(r.RoleId, out var n) ? n : null).FirstOrDefault(n => n != null),
            IsActive = u.IsActive,
            IsLockedOut = u.LockoutEndDateUtc.HasValue && u.LockoutEndDateUtc.Value > now,
            MustChangePassword = u.ShouldChangePasswordOnNextLogin,
            CreationTime = u.CreationTime,
            IsCurrentUser = u.Id == AbpSession.UserId,
        }).ToList();
    }

    public virtual async Task<SuggestedUserNameDto> GetSuggestedUserName(GetSuggestedUserNameInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name) && string.IsNullOrWhiteSpace(input.Surname))
        {
            return new SuggestedUserNameDto();
        }

        return new SuggestedUserNameDto { UserName = await _names.SuggestUserNameAsync(AbpSession.GetTenantId(), input.Name, input.Surname) };
    }

    public virtual async Task<FarmUserDto> Create(CreateFarmUserInput input)
    {
        FarmProvisioningManager.EnsureFarmRole(input.RoleName);
        var user = await _provisioning.CreateFarmUserAsync(AbpSession.GetTenantId(), input.Name, input.Surname, input.UserName,
            input.EmailAddress, input.TemporaryPassword, input.RoleName, mustChangePassword: true);
        return (await GetAll()).First(u => u.Id == user.Id);
    }

    public virtual async Task Update(UpdateFarmUserInput input)
    {
        FarmProvisioningManager.EnsureFarmRole(input.RoleName);
        var user = await UserManager.GetUserByIdAsync(input.Id);
        var currentRoles = await UserManager.GetRolesAsync(user);
        if (user.Id == AbpSession.UserId && input.RoleName != StaticRoleNames.Tenants.FarmOwner)
        {
            throw new UserFriendlyException("You cannot remove your own Farm Owner role.");
        }

        if (currentRoles.Contains(StaticRoleNames.Tenants.FarmOwner) && input.RoleName != StaticRoleNames.Tenants.FarmOwner)
        {
            await EnsureAnotherActiveOwnerAsync(user.Id);
        }

        user.Name = input.Name.Trim();
        user.Surname = input.Surname.Trim();
        user.EmailAddress = input.EmailAddress.Trim();
        user.SetNormalizedNames();
        CheckErrors(await UserManager.UpdateAsync(user));
        CheckErrors(await UserManager.SetRolesAsync(user, new[] { input.RoleName }));
    }

    public virtual async Task SetActive(SetUserActiveInput input)
    {
        var user = await UserManager.GetUserByIdAsync(input.Id);
        if (!input.IsActive)
        {
            if (user.Id == AbpSession.UserId)
            {
                throw new UserFriendlyException("You cannot deactivate your own account.");
            }

            if ((await UserManager.GetRolesAsync(user)).Contains(StaticRoleNames.Tenants.FarmOwner))
            {
                await EnsureAnotherActiveOwnerAsync(user.Id);
            }
        }

        user.IsActive = input.IsActive;
        CheckErrors(await UserManager.UpdateAsync(user));
    }

    public virtual async Task ResetPassword(ResetFarmUserPasswordInput input)
    {
        var user = await UserManager.GetUserByIdAsync(input.Id);
        await UserManager.InitializeOptionsAsync(AbpSession.TenantId);
        CheckErrors(await UserManager.ChangePasswordAsync(user, input.TemporaryPassword));
        user.ShouldChangePasswordOnNextLogin = true;
        user.LockoutEndDateUtc = null;
        user.AccessFailedCount = 0;
        CheckErrors(await UserManager.UpdateAsync(user));
    }

    private async Task EnsureAnotherActiveOwnerAsync(long exceptUserId)
    {
        var ownerRole = await _roleManager.Roles.FirstAsync(r => r.Name == StaticRoleNames.Tenants.FarmOwner);
        var others = await UserRepository.GetAll()
            .Where(u => u.Id != exceptUserId && u.IsActive && u.Roles.Any(r => r.RoleId == ownerRole.Id))
            .AnyAsync();
        if (!others)
        {
            throw new UserFriendlyException("A farm must keep at least one active Farm Owner.");
        }
    }
}
