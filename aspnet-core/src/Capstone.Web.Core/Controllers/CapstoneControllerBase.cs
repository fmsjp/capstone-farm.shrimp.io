using Abp.AspNetCore.Mvc.Controllers;
using Abp.IdentityFramework;
using Microsoft.AspNetCore.Identity;

namespace Capstone.Controllers
{
    public abstract class CapstoneControllerBase : AbpController
    {
        protected CapstoneControllerBase()
        {
            LocalizationSourceName = CapstoneConsts.LocalizationSourceName;
        }

        protected void CheckErrors(IdentityResult identityResult)
        {
            identityResult.CheckErrors(LocalizationManager);
        }
    }
}
