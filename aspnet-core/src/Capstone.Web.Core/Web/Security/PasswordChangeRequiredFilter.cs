using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Capstone.Web.Security;

public class PasswordChangeRequiredFilter : IAsyncActionFilter
{
    public const string ClaimType = "fpms_pwd_change_required";

    private static readonly string[] AllowedActions =
    {
        "Profile.ChangeMyPassword",
        "Session.GetCurrentLoginInformations",
        "AbpUserConfiguration.GetAll",
        "TokenAuth.Authenticate",
    };

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        if (user?.Identity?.IsAuthenticated == true && user.HasClaim(c => c.Type == ClaimType && c.Value == "true"))
        {
            var descriptor = context.ActionDescriptor as ControllerActionDescriptor;
            var name = descriptor == null ? string.Empty : $"{descriptor.ControllerName}.{descriptor.ActionName}";
            if (!AllowedActions.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase)))
            {
                context.Result = new ObjectResult(new
                {
                    success = false,
                    error = new { message = "You must change your temporary password before continuing." },
                    unAuthorizedRequest = true,
                    __abp = true,
                })
                { StatusCode = 403 };
                return;
            }
        }

        await next();
    }
}
