using Abp.Authorization;
using Abp.Runtime.Session;
using Capstone.Configuration.Dto;
using System.Threading.Tasks;

namespace Capstone.Configuration;

[AbpAuthorize]
public class ConfigurationAppService : CapstoneAppServiceBase, IConfigurationAppService
{
    public async Task ChangeUiTheme(ChangeUiThemeInput input)
    {
        await SettingManager.ChangeSettingForUserAsync(AbpSession.ToUserIdentifier(), AppSettingNames.UiTheme, input.Theme);
    }
}
