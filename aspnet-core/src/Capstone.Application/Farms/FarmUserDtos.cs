using Abp.Auditing;
using Abp.Authorization.Users;
using System;
using System.ComponentModel.DataAnnotations;

namespace Capstone.Farms.Dto;

public class FarmUserDto
{
    public long Id { get; set; }
    public string UserName { get; set; }
    public string Name { get; set; }
    public string Surname { get; set; }
    public string EmailAddress { get; set; }
    public string RoleName { get; set; }
    public bool IsActive { get; set; }
    public bool IsLockedOut { get; set; }
    public bool MustChangePassword { get; set; }
    public DateTime CreationTime { get; set; }
    public bool IsCurrentUser { get; set; }
}

public class CreateFarmUserInput
{
    [Required, StringLength(AbpUserBase.MaxNameLength)]
    public string Name { get; set; }

    [Required, StringLength(AbpUserBase.MaxSurnameLength)]
    public string Surname { get; set; }

    [Required, StringLength(AbpUserBase.MaxUserNameLength)]
    public string UserName { get; set; }

    [Required, EmailAddress, StringLength(AbpUserBase.MaxEmailAddressLength)]
    public string EmailAddress { get; set; }

    [Required]
    public string RoleName { get; set; }

    [Required, StringLength(AbpUserBase.MaxPlainPasswordLength), DisableAuditing]
    public string TemporaryPassword { get; set; }
}

public class UpdateFarmUserInput
{
    public long Id { get; set; }

    [Required, StringLength(AbpUserBase.MaxNameLength)]
    public string Name { get; set; }

    [Required, StringLength(AbpUserBase.MaxSurnameLength)]
    public string Surname { get; set; }

    [Required, EmailAddress, StringLength(AbpUserBase.MaxEmailAddressLength)]
    public string EmailAddress { get; set; }

    [Required]
    public string RoleName { get; set; }
}

public class SetUserActiveInput
{
    public long Id { get; set; }
    public bool IsActive { get; set; }
}

public class ResetFarmUserPasswordInput
{
    public long Id { get; set; }

    [Required, StringLength(AbpUserBase.MaxPlainPasswordLength), DisableAuditing]
    public string TemporaryPassword { get; set; }
}

public class GetSuggestedUserNameInput
{
    [StringLength(AbpUserBase.MaxNameLength)]
    public string Name { get; set; }

    [StringLength(AbpUserBase.MaxSurnameLength)]
    public string Surname { get; set; }
}

public class SuggestedUserNameDto
{
    public string UserName { get; set; }
}
