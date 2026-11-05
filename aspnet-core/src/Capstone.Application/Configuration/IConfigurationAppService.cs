using Capstone.Configuration.Dto;
using System.Threading.Tasks;

namespace Capstone.Configuration;

public interface IConfigurationAppService
{
    Task ChangeUiTheme(ChangeUiThemeInput input);
}
