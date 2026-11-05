using Abp.Configuration;
using System.Collections.Generic;

namespace Capstone.Configuration;

public class AppSettingProvider : SettingProvider
{
    public override IEnumerable<SettingDefinition> GetSettingDefinitions(SettingDefinitionProviderContext context)
    {
        return new[]
        {
            new SettingDefinition(AppSettingNames.UiTheme, "blue", scopes: SettingScopes.Application | SettingScopes.Tenant | SettingScopes.User, clientVisibilityProvider: new VisibleSettingClientVisibilityProvider()),
            new SettingDefinition(AppSettingNames.RegistrationEnabled, "true", scopes: SettingScopes.Application, clientVisibilityProvider: new VisibleSettingClientVisibilityProvider()),
        };
    }
}
