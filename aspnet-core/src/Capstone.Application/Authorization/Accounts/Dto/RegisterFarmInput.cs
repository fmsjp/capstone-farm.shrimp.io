using Abp.Auditing;
using Abp.Authorization.Users;
using Capstone.Farms;
using Capstone.Validation;
using System.ComponentModel.DataAnnotations;

namespace Capstone.Authorization.Accounts.Dto;

public class RegisterFarmInput
{
    [Required]
    [StringLength(64, MinimumLength = 2)]
    [RegularExpression("^[a-zA-Z][a-zA-Z0-9_-]{1,63}$", ErrorMessage = "Farm code must start with a letter and use letters, digits, '-' or '_'.")]
    public string FarmCode { get; set; }

    [Required]
    [StringLength(FarmProfile.MaxFarmNameLength)]
    public string FarmName { get; set; }

    [Required]
    [StringLength(FarmProfile.MaxAddressLineLength)]
    public string AddressLine1 { get; set; }

    [StringLength(FarmProfile.MaxAddressLineLength)]
    public string AddressLine2 { get; set; }

    [Required]
    [StringLength(FarmProfile.MaxCityLength)]
    public string CityMunicipality { get; set; }

    [Required]
    [StringLength(FarmProfile.MaxProvinceLength)]
    public string Province { get; set; }

    [StringLength(FarmProfile.MaxPostalCodeLength)]
    public string PostalCode { get; set; }

    [Required]
    [StringLength(FarmProfile.MaxCountryLength)]
    public string Country { get; set; } = FarmProfile.DefaultCountry;

    [Range(typeof(decimal), "0.01", "100000", ErrorMessage = "Total area must be between 0.01 and 100,000 hectares.")]
    public decimal? TotalAreaHectares { get; set; }

    [StringLength(100)]
    public string ContactName { get; set; }

    [StringLength(50)]
    public string ContactPhone { get; set; }

    [OptionalEmailAddress]
    [StringLength(256)]
    public string ContactEmail { get; set; }

    [Required]
    [StringLength(AbpUserBase.MaxNameLength)]
    public string OwnerName { get; set; }

    [Required]
    [StringLength(AbpUserBase.MaxSurnameLength)]
    public string OwnerSurname { get; set; }

    [Required]
    [StringLength(AbpUserBase.MaxUserNameLength)]
    public string OwnerUserName { get; set; }

    [Required]
    [EmailAddress]
    [StringLength(AbpUserBase.MaxEmailAddressLength)]
    public string OwnerEmailAddress { get; set; }

    [Required]
    [StringLength(AbpUserBase.MaxPlainPasswordLength)]
    [DisableAuditing]
    public string OwnerPassword { get; set; }
}

public class RegisterFarmOutput
{
    public string FarmCode { get; set; }
    public int TenantId { get; set; }
    public bool CanLogin { get; set; }
}

public class RegistrationStatusOutput
{
    public bool IsEnabled { get; set; }
}

public class GetRegistrationSuggestionsInput
{
    [StringLength(FarmProfile.MaxFarmNameLength)]
    public string FarmName { get; set; }

    [StringLength(AbpUserBase.MaxNameLength)]
    public string OwnerName { get; set; }

    [StringLength(AbpUserBase.MaxSurnameLength)]
    public string OwnerSurname { get; set; }
}

public class RegistrationSuggestionsOutput
{
    public string FarmCode { get; set; }

    public string OwnerUserName { get; set; }
}
