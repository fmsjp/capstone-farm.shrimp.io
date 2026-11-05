using Abp.Application.Services.Dto;
using Abp.Auditing;
using Abp.Authorization;
using Abp.Authorization.Users;
using Abp.Configuration;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Abp.UI;
using Capstone.Authorization;
using Capstone.Authorization.Roles;
using Capstone.Authorization.Users;
using Capstone.Configuration;
using Capstone.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Capstone.SystemAdmin;

public class SystemFarmDto
{
    public int TenantId { get; set; }
    public string FarmCode { get; set; }
    public string Name { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreationTime { get; set; }
    public int UserCount { get; set; }
}

public class SetFarmActiveInput
{
    public int TenantId { get; set; }
    public bool IsActive { get; set; }
}

public class RegistrationSettingDto
{
    public bool IsEnabled { get; set; }
}

[AbpAuthorize(PermissionNames.System_Farms)]
public class SystemFarmAppService : CapstoneAppServiceBase
{
    private readonly IRepository<Tenant, int> _tenants;

    public SystemFarmAppService(IRepository<Tenant, int> tenants)
    {
        _tenants = tenants;
    }

    public virtual async Task<List<SystemFarmDto>> GetFarms()
    {
        var tenants = await _tenants.GetAll().AsNoTracking().OrderBy(t => t.TenancyName).ToListAsync();
        Dictionary<int, int> counts;
        using (CurrentUnitOfWork.DisableFilter(AbpDataFilters.MayHaveTenant))
        {
            counts = await UserRepository.GetAll().Where(u => u.TenantId != null).GroupBy(u => u.TenantId.Value)
                .Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        }

        return tenants.Select(t => new SystemFarmDto
        {
            TenantId = t.Id, FarmCode = t.TenancyName, Name = t.Name, IsActive = t.IsActive, CreationTime = t.CreationTime,
            UserCount = counts.TryGetValue(t.Id, out var n) ? n : 0,
        }).ToList();
    }

    public virtual async Task SetFarmActive(SetFarmActiveInput input)
    {
        var tenant = await TenantManager.GetByIdAsync(input.TenantId);
        tenant.IsActive = input.IsActive;
        await TenantManager.UpdateAsync(tenant);
    }

    public virtual async Task<RegistrationSettingDto> GetRegistrationSetting()
    {
        return new RegistrationSettingDto { IsEnabled = await SettingManager.GetSettingValueForApplicationAsync<bool>(AppSettingNames.RegistrationEnabled) };
    }

    public virtual async Task SetRegistrationSetting(RegistrationSettingDto input)
    {
        await SettingManager.ChangeSettingForApplicationAsync(AppSettingNames.RegistrationEnabled, input.IsEnabled ? "true" : "false");
    }
}

public enum IdentityVerificationMethod
{
    SecondFarmOwner = 1,
    SecurityQuestion = 2,
}

public class RecoveryUserDto
{
    public long UserId { get; set; }
    public string UserName { get; set; }
    public string FullName { get; set; }
    public string RoleName { get; set; }
    public bool IsActive { get; set; }
    public bool IsLockedOut { get; set; }
    public bool MustChangePassword { get; set; }
}

public class RecoveryActionInput
{
    public int TenantId { get; set; }
    public long UserId { get; set; }

    public bool IdentityVerified { get; set; }

    public IdentityVerificationMethod VerificationMethod { get; set; }

    [StringLength(300)]
    public string Note { get; set; }
}

public class TemporaryPasswordDto
{
    public string UserName { get; set; }

    [DisableAuditing]
    public string TemporaryPassword { get; set; }
}

[AbpAuthorize(PermissionNames.System_AccountRecovery)]
public class AccountRecoveryAppService : CapstoneAppServiceBase
{
    private readonly RoleManager _roleManager;

    public AccountRecoveryAppService(RoleManager roleManager)
    {
        _roleManager = roleManager;
    }

    public virtual async Task<List<RecoveryUserDto>> GetFarmUsers(int tenantId)
    {
        await TenantManager.GetByIdAsync(tenantId);
        using (CurrentUnitOfWork.SetTenantId(tenantId))
        {
            var users = await UserRepository.GetAll().AsNoTracking().Include(u => u.Roles).OrderBy(u => u.UserName).ToListAsync();
            var roles = await _roleManager.Roles.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.DisplayName);
            var now = DateTime.UtcNow;
            return users.Select(u => new RecoveryUserDto
            {
                UserId = u.Id, UserName = u.UserName, FullName = $"{u.Name} {u.Surname}".Trim(),
                RoleName = u.Roles.Select(r => roles.TryGetValue(r.RoleId, out var n) ? n : null).FirstOrDefault(n => n != null),
                IsActive = u.IsActive, IsLockedOut = u.LockoutEndDateUtc.HasValue && u.LockoutEndDateUtc > now, MustChangePassword = u.ShouldChangePasswordOnNextLogin,
            }).ToList();
        }
    }

    public virtual async Task Unlock(RecoveryActionInput input)
    {
        EnsureVerified(input);
        using (CurrentUnitOfWork.SetTenantId(input.TenantId))
        {
            var user = await UserManager.GetUserByIdAsync(input.UserId);
            user.LockoutEndDateUtc = null;
            user.AccessFailedCount = 0;
            CheckErrors(await UserManager.UpdateAsync(user));
            Logger.Info($"Account recovery: unlocked user {user.Id} in farm {input.TenantId} (verified by {input.VerificationMethod}).");
        }
    }

    public virtual async Task<TemporaryPasswordDto> IssueTemporaryPassword(RecoveryActionInput input)
    {
        EnsureVerified(input);
        using (CurrentUnitOfWork.SetTenantId(input.TenantId))
        {
            var user = await UserManager.GetUserByIdAsync(input.UserId);
            await UserManager.InitializeOptionsAsync(input.TenantId);
            var password = GenerateTemporaryPassword();
            CheckErrors(await UserManager.ChangePasswordAsync(user, password));
            user.ShouldChangePasswordOnNextLogin = true;
            user.LockoutEndDateUtc = null;
            user.AccessFailedCount = 0;
            user.IsActive = true;
            CheckErrors(await UserManager.UpdateAsync(user));
            Logger.Info($"Account recovery: temporary password issued to user {user.Id} in farm {input.TenantId} (verified by {input.VerificationMethod}).");
            return new TemporaryPasswordDto { UserName = user.UserName, TemporaryPassword = password };
        }
    }

    private static void EnsureVerified(RecoveryActionInput input)
    {
        if (!input.IdentityVerified || !Enum.IsDefined(input.VerificationMethod))
        {
            throw new UserFriendlyException("Confirm that the person's identity was verified through a second Farm Owner or the pre-agreed security question.");
        }
    }

    public static string GenerateTemporaryPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnpqrstuvwxyz";
        const string digits = "23456789";
        var all = upper + lower + digits;
        var chars = new List<char>
        {
            upper[RandomNumberGenerator.GetInt32(upper.Length)],
            lower[RandomNumberGenerator.GetInt32(lower.Length)],
            digits[RandomNumberGenerator.GetInt32(digits.Length)],
        };
        while (chars.Count < 12)
        {
            chars.Add(all[RandomNumberGenerator.GetInt32(all.Length)]);
        }

        return new string(chars.OrderBy(_ => RandomNumberGenerator.GetInt32(1000)).ToArray());
    }
}

public class SecurityLogInput : Capstone.Common.PagedInput
{
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int? TenantId { get; set; }
    public bool FailuresOnly { get; set; }
}

public class LoginAttemptDto
{
    public DateTime CreationTime { get; set; }
    public string FarmCode { get; set; }
    public string UserNameOrEmail { get; set; }
    public string Result { get; set; }
    public string ClientIpAddress { get; set; }
    public string BrowserInfo { get; set; }
}

public class SystemEventDto
{
    public DateTime ExecutionTime { get; set; }
    public int? TenantId { get; set; }
    public long? UserId { get; set; }
    public string Service { get; set; }
    public string Method { get; set; }
    public int DurationMs { get; set; }
    public bool Failed { get; set; }
    public string ErrorType { get; set; }
}

[AbpAuthorize(PermissionNames.System_Logs)]
public class SecurityLogAppService : CapstoneAppServiceBase
{
    private readonly IRepository<UserLoginAttempt, long> _attempts;
    private readonly IRepository<AuditLog, long> _auditLogs;

    public SecurityLogAppService(IRepository<UserLoginAttempt, long> attempts, IRepository<AuditLog, long> auditLogs)
    {
        _attempts = attempts;
        _auditLogs = auditLogs;
    }

    public virtual async Task<PagedResultDto<LoginAttemptDto>> GetLoginAttempts(SecurityLogInput input)
    {
        using (CurrentUnitOfWork.DisableFilter(AbpDataFilters.MayHaveTenant))
        {
            var query = _attempts.GetAll().AsNoTracking()
                .Where(a => !input.TenantId.HasValue || a.TenantId == input.TenantId)
                .Where(a => !input.FromUtc.HasValue || a.CreationTime >= input.FromUtc)
                .Where(a => !input.ToUtc.HasValue || a.CreationTime < input.ToUtc)
                .Where(a => !input.FailuresOnly || a.Result != AbpLoginResultType.Success);
            var total = await query.CountAsync();
            var list = await query.OrderByDescending(a => a.CreationTime).Skip(input.SkipCount).Take(input.MaxResultCount).ToListAsync();
            return new PagedResultDto<LoginAttemptDto>(total, list.Select(a => new LoginAttemptDto
            {
                CreationTime = a.CreationTime, FarmCode = a.TenancyName, UserNameOrEmail = a.UserNameOrEmailAddress, Result = a.Result.ToString(),
                ClientIpAddress = a.ClientIpAddress, BrowserInfo = a.BrowserInfo,
            }).ToList());
        }
    }

    public virtual async Task<PagedResultDto<SystemEventDto>> GetSystemEvents(SecurityLogInput input)
    {
        using (CurrentUnitOfWork.DisableFilter(AbpDataFilters.MayHaveTenant))
        {
            var query = _auditLogs.GetAll().AsNoTracking()
                .Where(a => !input.TenantId.HasValue || a.TenantId == input.TenantId)
                .Where(a => !input.FromUtc.HasValue || a.ExecutionTime >= input.FromUtc)
                .Where(a => !input.ToUtc.HasValue || a.ExecutionTime < input.ToUtc)
                .Where(a => !input.FailuresOnly || a.Exception != null);
            var total = await query.CountAsync();
            var list = await query.OrderByDescending(a => a.ExecutionTime).Skip(input.SkipCount).Take(input.MaxResultCount)
                .Select(a => new { a.ExecutionTime, a.TenantId, a.UserId, a.ServiceName, a.MethodName, a.ExecutionDuration, a.Exception })
                .ToListAsync();
            return new PagedResultDto<SystemEventDto>(total, list.Select(a => new SystemEventDto
            {
                ExecutionTime = a.ExecutionTime, TenantId = a.TenantId, UserId = a.UserId,
                Service = a.ServiceName?[(a.ServiceName.LastIndexOf('.') + 1)..], Method = a.MethodName, DurationMs = a.ExecutionDuration,
                Failed = a.Exception != null, ErrorType = a.Exception == null ? null : a.Exception.Split(':', 2)[0].Trim(),
            }).ToList());
        }
    }
}
