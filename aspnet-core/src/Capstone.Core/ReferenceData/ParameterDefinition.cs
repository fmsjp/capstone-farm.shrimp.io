using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;

namespace Capstone.ReferenceData;

public class ParameterDefinition : Entity<string>, IHasModificationTime
{
    public const int MaxCodeLength = 20;
    public const int MaxNameLength = 100;
    public const int MaxUnitLength = 30;
    public const int MaxRecommendationLength = 500;
    public const int MaxSourceLength = 200;

    public string Name { get; set; }
    public string UnitCode { get; set; }
    public decimal PhysicalMin { get; set; }
    public decimal PhysicalMax { get; set; }
    public decimal? OptimalMin { get; set; }
    public bool OptimalMinExclusive { get; set; }
    public decimal? OptimalMax { get; set; }
    public bool OptimalMaxExclusive { get; set; }
    public string GuidanceFrequency { get; set; }

    public string LowRecommendation { get; set; }

    public string HighRecommendation { get; set; }
    public string Source { get; set; }

    public int SortOrder { get; set; }
    public bool RequiredAtStocking { get; set; }
    public bool IsActive { get; set; } = true;
    public System.DateTime? LastModificationTime { get; set; }

    public bool IsPhysicallyPossible(decimal value) => value >= PhysicalMin && value <= PhysicalMax;

    public bool IsOutOfOptimalRange(decimal value)
    {
        if (OptimalMin.HasValue && (OptimalMinExclusive ? value <= OptimalMin.Value : value < OptimalMin.Value))
        {
            return true;
        }

        if (OptimalMax.HasValue && (OptimalMaxExclusive ? value >= OptimalMax.Value : value > OptimalMax.Value))
        {
            return true;
        }

        return false;
    }

    public Production.ReadingSide? SideOf(decimal value)
    {
        if (OptimalMin.HasValue && (OptimalMinExclusive ? value <= OptimalMin.Value : value < OptimalMin.Value))
        {
            return Production.ReadingSide.Low;
        }

        if (OptimalMax.HasValue && (OptimalMaxExclusive ? value >= OptimalMax.Value : value > OptimalMax.Value))
        {
            return Production.ReadingSide.High;
        }

        return null;
    }

    public string RecommendationFor(decimal value)
    {
        if (OptimalMin.HasValue && (OptimalMinExclusive ? value <= OptimalMin.Value : value < OptimalMin.Value))
        {
            return string.IsNullOrWhiteSpace(LowRecommendation) ? null : LowRecommendation;
        }

        if (OptimalMax.HasValue && (OptimalMaxExclusive ? value >= OptimalMax.Value : value > OptimalMax.Value))
        {
            return string.IsNullOrWhiteSpace(HighRecommendation) ? null : HighRecommendation;
        }

        return null;
    }

    public string DescribeOptimalRange()
    {
        if (!OptimalMin.HasValue && !OptimalMax.HasValue)
        {
            return "no range (recorded only)";
        }

        if (OptimalMin.HasValue && OptimalMax.HasValue)
        {
            return $"{OptimalMin:0.###}–{OptimalMax:0.###} {UnitCode}";
        }

        return OptimalMin.HasValue
            ? $"{(OptimalMinExclusive ? ">" : "≥")} {OptimalMin:0.###} {UnitCode}"
            : $"{(OptimalMaxExclusive ? "<" : "≤")} {OptimalMax:0.###} {UnitCode}";
    }
}

public static class ParameterCodes
{
    public const string DO = "DO";
    public const string Temperature = "TEMP";
    public const string PH = "PH";
    public const string Salinity = "SAL";
    public const string Tan = "TAN";
    public const string Nitrite = "NO2";
    public const string Alkalinity = "ALK";
    public const string Nitrate = "NO3";

    public static readonly string[] RequiredAtStocking = { Temperature, Salinity, PH, DO };
}
