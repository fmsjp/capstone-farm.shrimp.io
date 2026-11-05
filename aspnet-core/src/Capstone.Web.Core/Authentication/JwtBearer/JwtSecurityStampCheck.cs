using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Abp.Domain.Uow;
using Abp.Runtime.Security;
using Capstone.Authorization.Users;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Capstone.Authentication.JwtBearer
{
    public static class JwtSecurityStampCheck
    {
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(1);
        private const string RejectedMessage = "Your session is no longer valid. Please sign in again.";

        public static async Task ValidateAsync(TokenValidatedContext context)
        {
            var principal = context.Principal;
            var userIdText = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!long.TryParse(userIdText, out var userId))
            {
                context.Fail(RejectedMessage);
                return;
            }

            var services = context.HttpContext.RequestServices;
            var stampClaimType = services.GetRequiredService<IOptions<IdentityOptions>>().Value.ClaimsIdentity.SecurityStampClaimType;
            var tokenStamp = principal.FindFirst(stampClaimType)?.Value;
            int? tenantId = int.TryParse(principal.FindFirst(AbpClaimTypes.TenantId)?.Value, out var parsedTenantId)
                ? parsedTenantId
                : null;

            var cache = services.GetRequiredService<IMemoryCache>();
            var cacheKey = $"jwt-stamp:{tenantId}:{userId}:{tokenStamp}";
            if (!cache.TryGetValue(cacheKey, out bool isCurrent))
            {
                isCurrent = await IsCurrentAsync(services, tenantId, userId, tokenStamp);
                cache.Set(cacheKey, isCurrent, CacheDuration);
            }

            if (!isCurrent)
            {
                context.Fail(RejectedMessage);
            }
        }

        private static async Task<bool> IsCurrentAsync(IServiceProvider services, int? tenantId, long userId, string tokenStamp)
        {
            var unitOfWorkManager = services.GetRequiredService<IUnitOfWorkManager>();
            using (var uow = unitOfWorkManager.Begin())
            {
                using (unitOfWorkManager.Current.SetTenantId(tenantId))
                {
                    var userManager = services.GetRequiredService<UserManager>();
                    var user = await userManager.FindByIdAsync(userId.ToString());
                    var isCurrent = user != null
                        && user.IsActive
                        && (string.IsNullOrEmpty(tokenStamp) || string.Equals(user.SecurityStamp, tokenStamp, StringComparison.Ordinal));
                    await uow.CompleteAsync();
                    return isCurrent;
                }
            }
        }
    }
}
