using Abp.Configuration;
using Abp.UI;
using Capstone.Authorization.Accounts.Dto;
using Capstone.Configuration;
using Capstone.Farms;
using System.Threading.Tasks;

namespace Capstone.Authorization.Accounts;

public class AccountAppService : CapstoneAppServiceBase, IAccountAppService
{
    private readonly FarmProvisioningManager _provisioning;
    private readonly AccountNameGenerator _names;

    public AccountAppService(FarmProvisioningManager provisioning, AccountNameGenerator names)
    {
        _provisioning = provisioning;
        _names = names;
    }

    public virtual async Task<IsTenantAvailableOutput> IsTenantAvailable(IsTenantAvailableInput input)
    {
        var tenant = await TenantManager.FindByTenancyNameAsync(input.TenancyName);
        if (tenant == null)
        {
            return new IsTenantAvailableOutput(TenantAvailabilityState.NotFound);
        }

        if (!tenant.IsActive)
        {
            return new IsTenantAvailableOutput(TenantAvailabilityState.InActive);
        }

        return new IsTenantAvailableOutput(TenantAvailabilityState.Available, tenant.Id);
    }

    public virtual async Task<RegistrationStatusOutput> GetRegistrationStatus()
    {
        return new RegistrationStatusOutput
        {
            IsEnabled = await SettingManager.GetSettingValueForApplicationAsync<bool>(AppSettingNames.RegistrationEnabled),
        };
    }

    public virtual async Task<RegistrationSuggestionsOutput> GetRegistrationSuggestions(GetRegistrationSuggestionsInput input)
    {
        if (!(await GetRegistrationStatus()).IsEnabled)
        {
            throw new UserFriendlyException("Farm registration is currently disabled. Contact the System Administrator.");
        }

        var hasOwnerName = !string.IsNullOrWhiteSpace(input.OwnerName) || !string.IsNullOrWhiteSpace(input.OwnerSurname);
        return new RegistrationSuggestionsOutput
        {
            FarmCode = string.IsNullOrWhiteSpace(input.FarmName) ? null : await _names.SuggestFarmCodeAsync(input.FarmName),
            OwnerUserName = hasOwnerName ? await _names.SuggestUserNameAsync(null, input.OwnerName, input.OwnerSurname) : null,
        };
    }

    public virtual async Task<RegisterFarmOutput> RegisterFarm(RegisterFarmInput input)
    {
        if (AbpSession.UserId.HasValue)
        {
            throw new UserFriendlyException("Sign out before registering a new farm account.");
        }

        if (!(await GetRegistrationStatus()).IsEnabled)
        {
            throw new UserFriendlyException("Farm registration is currently disabled. Contact the System Administrator.");
        }

        using var hostScope = CurrentUnitOfWork.SetTenantId(null);
        var (tenant, _) = await _provisioning.CreateFarmAsync(new FarmRegistration
        {
            FarmCode = input.FarmCode,
            FarmName = input.FarmName,
            AddressLine1 = input.AddressLine1,
            AddressLine2 = input.AddressLine2,
            CityMunicipality = input.CityMunicipality,
            Province = input.Province,
            PostalCode = input.PostalCode,
            Country = input.Country,
            TotalAreaHectares = input.TotalAreaHectares,
            ContactName = input.ContactName,
            ContactPhone = input.ContactPhone,
            ContactEmail = input.ContactEmail,

            OwnerName = input.OwnerName,
            OwnerSurname = input.OwnerSurname,
            OwnerUserName = input.OwnerUserName,
            OwnerEmailAddress = input.OwnerEmailAddress,
            OwnerPassword = input.OwnerPassword,
        });

        return new RegisterFarmOutput { FarmCode = tenant.TenancyName, TenantId = tenant.Id, CanLogin = true };
    }
}
