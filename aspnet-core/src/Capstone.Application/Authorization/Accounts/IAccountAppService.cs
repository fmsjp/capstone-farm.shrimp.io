using Abp.Application.Services;
using Capstone.Authorization.Accounts.Dto;
using System.Threading.Tasks;

namespace Capstone.Authorization.Accounts;

public interface IAccountAppService : IApplicationService
{
    Task<IsTenantAvailableOutput> IsTenantAvailable(IsTenantAvailableInput input);

    Task<RegistrationStatusOutput> GetRegistrationStatus();

    Task<RegistrationSuggestionsOutput> GetRegistrationSuggestions(GetRegistrationSuggestionsInput input);

    Task<RegisterFarmOutput> RegisterFarm(RegisterFarmInput input);
}
