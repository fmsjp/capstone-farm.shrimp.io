using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;
using Abp.UI;
using Capstone.Domain;
using Capstone.Ponds;
using System.Linq;
using System.Text.RegularExpressions;

namespace Capstone.Farms;

public enum FarmDateOrder { MonthFirst = 1, DayFirst = 2 }

public class FarmProfile : AuditedEntity<int>, IMustHaveTenant
{
    public const int MaxFarmNameLength = 150;
    public const int MaxAddressLineLength = 200;
    public const int MaxCityLength = 100;
    public const int MaxProvinceLength = 100;
    public const int MaxPostalCodeLength = 10;
    public const int MaxCountryLength = 100;
    public const string DefaultCountry = "Philippines";

    public int TenantId { get; set; }

    public string FarmName { get; set; }

    public string AddressLine1 { get; set; }

    public string AddressLine2 { get; set; }

    public string CityMunicipality { get; set; }
    public string Province { get; set; }
    public string PostalCode { get; set; }
    public string Country { get; set; } = DefaultCountry;

    public decimal? TotalAreaHectares { get; set; }

    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string ContactName { get; set; }
    public string ContactPhone { get; set; }
    public string ContactEmail { get; set; }
    public string TimeZone { get; set; } = FarmClock.DefaultTimeZone;
    public int DocumentExpiryAlertDays { get; set; } = 30;

    public int StockingWindowDays { get; set; } = ReferenceData.FarmThresholds.DefaultStockingWindowDays;

    public System.DateTime? StandardsReviewedAtUtc { get; set; }

    public FarmingSystem FarmingSystem { get; set; } = FarmingSystem.SemiIntensive;

    public FarmDateOrder DateOrder { get; set; } = FarmDateOrder.MonthFirst;

    public bool Use24HourTime { get; set; }

    public string FormatAddress() => FarmAddress.Format(AddressLine1, AddressLine2, CityMunicipality, Province, PostalCode, Country);
}

public static class FarmAddress
{
    private static readonly Regex PhilippinePostalCode = new("^[0-9]{4}$");

    public static string Format(string line1, string line2, string city, string province, string postalCode, string country)
    {
        var provincePart = string.Join(" ", new[] { province, postalCode }.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()));
        return string.Join(", ", new[] { line1, line2, city, provincePart, country }.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()));
    }

    public static void Validate(string postalCode, string country)
    {
        if (string.IsNullOrWhiteSpace(postalCode))
        {
            return;
        }

        var isPhilippines = string.IsNullOrWhiteSpace(country) || country.Trim().Equals(FarmProfile.DefaultCountry, System.StringComparison.OrdinalIgnoreCase);
        if (isPhilippines && !PhilippinePostalCode.IsMatch(postalCode.Trim()))
        {
            throw new UserFriendlyException("A Philippine postal code has 4 digits (for example 9502).");
        }
    }

    public static string Clean(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
