using Abp.Application.Services;
using Capstone.Sessions.Dto;
using System.Threading.Tasks;

namespace Capstone.Sessions;

public interface ISessionAppService : IApplicationService
{
    Task<GetCurrentLoginInformationsOutput> GetCurrentLoginInformations();
}
