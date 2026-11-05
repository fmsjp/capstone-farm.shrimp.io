using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.UI;
using Capstone.Authorization;
using Capstone.Common;
using Capstone.Domain;
using Capstone.Ponds;
using Capstone.Validation;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Capstone.Farms;

public class FarmProfileDto
{
    public string FarmCode { get; set; }

    [Required, StringLength(FarmProfile.MaxFarmNameLength)]
    public string FarmName { get; set; }

    [Required, StringLength(FarmProfile.MaxAddressLineLength)]
    public string AddressLine1 { get; set; }

    [StringLength(FarmProfile.MaxAddressLineLength)]
    public string AddressLine2 { get; set; }

    [Required, StringLength(FarmProfile.MaxCityLength)]
    public string CityMunicipality { get; set; }

    [Required, StringLength(FarmProfile.MaxProvinceLength)]
    public string Province { get; set; }

    [StringLength(FarmProfile.MaxPostalCodeLength)]
    public string PostalCode { get; set; }

    [Required, StringLength(FarmProfile.MaxCountryLength)]
    public string Country { get; set; }

    [Range(typeof(decimal), "0.01", "100000", ErrorMessage = "Total area must be between 0.01 and 100,000 hectares.")]
    public decimal? TotalAreaHectares { get; set; }

    [Range(-90, 90)]
    public decimal? Latitude { get; set; }

    [Range(-180, 180)]
    public decimal? Longitude { get; set; }

    [StringLength(100)]
    public string ContactName { get; set; }

    [StringLength(50)]
    public string ContactPhone { get; set; }

    [OptionalEmailAddress, StringLength(256)]
    public string ContactEmail { get; set; }

    [Required, StringLength(64)]
    public string TimeZone { get; set; }

    [Range(1, 365)]
    public int DocumentExpiryAlertDays { get; set; }

    public FarmingSystem FarmingSystem { get; set; } = FarmingSystem.SemiIntensive;

    public FarmDateOrder DateOrder { get; set; } = FarmDateOrder.MonthFirst;

    public bool Use24HourTime { get; set; }

    public decimal PondAreaHectares { get; set; }

    public string FullAddress { get; set; }
}

[AbpAuthorize(PermissionNames.FarmProfile)]
public class FarmProfileAppService : CapstoneAppServiceBase
{
    private readonly IRepository<FarmProfile, int> _profiles;
    private readonly IRepository<Pond, Guid> _ponds;

    public FarmProfileAppService(IRepository<FarmProfile, int> profiles, IRepository<Pond, Guid> ponds)
    {
        _profiles = profiles;
        _ponds = ponds;
    }

    public virtual async Task<FarmProfileDto> Get()
    {
        var p = await Farm.GetProfileAsync();
        var tenant = await GetCurrentTenantAsync();
        return new FarmProfileDto
        {
            FarmCode = tenant.TenancyName,
            FarmName = string.IsNullOrWhiteSpace(p.FarmName) ? tenant.Name : p.FarmName,
            AddressLine1 = p.AddressLine1,
            AddressLine2 = p.AddressLine2,
            CityMunicipality = p.CityMunicipality,
            Province = p.Province,
            PostalCode = p.PostalCode,
            Country = string.IsNullOrWhiteSpace(p.Country) ? FarmProfile.DefaultCountry : p.Country,
            TotalAreaHectares = p.TotalAreaHectares,
            Latitude = p.Latitude,
            Longitude = p.Longitude,
            ContactName = p.ContactName,
            ContactPhone = p.ContactPhone,
            ContactEmail = p.ContactEmail,
            TimeZone = p.TimeZone,
            DocumentExpiryAlertDays = p.DocumentExpiryAlertDays,
            FarmingSystem = p.FarmingSystem,
            DateOrder = p.DateOrder,
            Use24HourTime = p.Use24HourTime,
            PondAreaHectares = await GetPondAreaHectaresAsync(),
            FullAddress = p.FormatAddress(),
        };
    }

    [AbpAuthorize(PermissionNames.FarmProfile_Edit)]
    public virtual async Task<List<WarningDto>> Update(FarmProfileDto input)
    {
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(input.TimeZone);
        }
        catch (Exception)
        {
            if (input.TimeZone != FarmClock.DefaultTimeZone)
            {
                throw new UserFriendlyException($"Unknown time zone '{input.TimeZone}'. Use an IANA name such as Asia/Manila.");
            }
        }

        FarmAddress.Validate(input.PostalCode, input.Country);

        var tenantId = Farm.TenantId;
        var profile = await _profiles.FirstOrDefaultAsync(x => x.Id == tenantId);
        if (profile == null)
        {
            profile = new FarmProfile { Id = tenantId, TenantId = tenantId };
            await _profiles.InsertAsync(profile);
        }

        profile.FarmName = input.FarmName.Trim();
        profile.AddressLine1 = FarmAddress.Clean(input.AddressLine1);
        profile.AddressLine2 = FarmAddress.Clean(input.AddressLine2);
        profile.CityMunicipality = FarmAddress.Clean(input.CityMunicipality);
        profile.Province = FarmAddress.Clean(input.Province);
        profile.PostalCode = FarmAddress.Clean(input.PostalCode);
        profile.Country = FarmAddress.Clean(input.Country) ?? FarmProfile.DefaultCountry;
        profile.TotalAreaHectares = input.TotalAreaHectares;
        profile.Latitude = input.Latitude;
        profile.Longitude = input.Longitude;
        profile.ContactName = input.ContactName;
        profile.ContactPhone = input.ContactPhone;
        profile.ContactEmail = input.ContactEmail;
        profile.TimeZone = input.TimeZone;
        profile.DocumentExpiryAlertDays = input.DocumentExpiryAlertDays;
        profile.FarmingSystem = Enum.IsDefined(input.FarmingSystem) ? input.FarmingSystem : FarmingSystem.SemiIntensive;
        profile.DateOrder = Enum.IsDefined(input.DateOrder) ? input.DateOrder : FarmDateOrder.MonthFirst;
        profile.Use24HourTime = input.Use24HourTime;

        var tenant = await GetCurrentTenantAsync();
        tenant.Name = profile.FarmName.Length > 128 ? profile.FarmName[..128] : profile.FarmName;
        await TenantManager.UpdateAsync(tenant);

        var warnings = new List<WarningDto>();
        var pondArea = await GetPondAreaHectaresAsync();
        if (profile.TotalAreaHectares.HasValue && pondArea > profile.TotalAreaHectares.Value)
        {
            warnings.Add(new WarningDto
            {
                Kind = "FarmArea",
                Message = $"The ponds add up to {pondArea:0.##} ha, more than the farm's total area of {profile.TotalAreaHectares.Value:0.##} ha. Check the total area or the pond areas.",
            });
        }

        return warnings;
    }

    private async Task<decimal> GetPondAreaHectaresAsync()
    {
        var m2 = await _ponds.GetAll().Where(p => !p.IsArchived).SumAsync(p => (decimal?)p.AreaM2) ?? 0m;
        return Math.Round(m2 / 10000m, 4);
    }
}
