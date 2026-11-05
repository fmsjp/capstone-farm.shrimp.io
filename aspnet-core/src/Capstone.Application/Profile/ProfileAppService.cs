using Abp.Auditing;
using Abp.Authorization;
using Abp.Authorization.Users;
using Abp.UI;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Capstone.Profile;

public class ChangeMyPasswordInput
{
    [Required]
    [DisableAuditing]
    public string CurrentPassword { get; set; }

    [Required]
    [StringLength(AbpUserBase.MaxPlainPasswordLength)]
    [DisableAuditing]
    public string NewPassword { get; set; }
}

public class MyProfileDto
{
    [Required]
    [StringLength(AbpUserBase.MaxNameLength)]
    public string Name { get; set; }

    [Required]
    [StringLength(AbpUserBase.MaxSurnameLength)]
    public string Surname { get; set; }

    [Required]
    [EmailAddress]
    [StringLength(AbpUserBase.MaxEmailAddressLength)]
    public string EmailAddress { get; set; }

    public string UserName { get; set; }
}

[AbpAuthorize]
public class ProfileAppService : CapstoneAppServiceBase
{
    public virtual async Task ChangeMyPassword(ChangeMyPasswordInput input)
    {
        var user = await GetCurrentUserAsync();
        await UserManager.InitializeOptionsAsync(AbpSession.TenantId);
        if (!await UserManager.CheckPasswordAsync(user, input.CurrentPassword))
        {
            throw new UserFriendlyException("The current password is incorrect.");
        }

        if (input.CurrentPassword == input.NewPassword)
        {
            throw new UserFriendlyException("Choose a new password that is different from the current one.");
        }

        CheckErrors(await UserManager.ChangePasswordAsync(user, input.NewPassword));
        user.ShouldChangePasswordOnNextLogin = false;
        CheckErrors(await UserManager.UpdateAsync(user));
    }

    public virtual async Task<MyProfileDto> GetMyProfile()
    {
        var user = await GetCurrentUserAsync();
        return new MyProfileDto { Name = user.Name, Surname = user.Surname, EmailAddress = user.EmailAddress, UserName = user.UserName };
    }

    public virtual async Task UpdateMyProfile(MyProfileDto input)
    {
        var user = await GetCurrentUserAsync();
        user.Name = input.Name.Trim();
        user.Surname = input.Surname.Trim();
        user.EmailAddress = input.EmailAddress.Trim();
        user.SetNormalizedNames();
        CheckErrors(await UserManager.UpdateAsync(user));
    }
}
