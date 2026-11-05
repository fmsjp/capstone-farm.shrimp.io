using Capstone.ReferenceData;
using System.Linq;

namespace Capstone.EntityFrameworkCore.Seed.Host;

public class ParameterDefinitionCreator
{
    public const string Source = "PNS/BAFS 197:2025 Annex B";

    private readonly CapstoneDbContext _context;

    public ParameterDefinitionCreator(CapstoneDbContext context)
    {
        _context = context;
    }

    public void Create()
    {
        Add(ParameterCodes.DO, "Dissolved oxygen (DO)", "ppm", 0, 20, 4.0m, false, null, false, "2× daily (AM, PM)", 1, true);
        Add(ParameterCodes.Temperature, "Temperature", "°C", 0, 45, 25m, false, 30m, false, "2× daily (AM, PM)", 2, true);
        Add(ParameterCodes.PH, "pH", "pH", 0, 14, 7.5m, false, 9.0m, false, "2× daily (AM, PM)", 3, true);
        Add(ParameterCodes.Salinity, "Salinity", "ppt", 0, 60, 15m, false, 25m, false, "1× daily", 4, true);
        Add(ParameterCodes.Tan, "Total ammonia nitrogen (TAN)", "ppm", 0, 100, null, false, 1.0m, false, "Weekly", 5, false);
        Add(ParameterCodes.Nitrite, "Nitrite (NO₂⁻)", "ppm", 0, 100, null, false, 0.5m, false, "Weekly", 6, false);
        Add(ParameterCodes.Alkalinity, "Alkalinity", "ppm CaCO₃", 0, 500, 100m, false, 200m, false, "Weekly", 7, false);
        Add(ParameterCodes.Nitrate, "Nitrate (NO₃⁻)", "ppm", 0, 500, null, false, 1.2m, false, "As needed", 8, false);
        _context.SaveChanges();
    }

    private void Add(string code, string name, string unit, decimal physMin, decimal physMax,
        decimal? optMin, bool optMinExclusive, decimal? optMax, bool optMaxExclusive, string frequency, int order, bool requiredAtStocking)
    {
        if (_context.ParameterDefinitions.Any(p => p.Id == code))
        {
            return;
        }

        _context.ParameterDefinitions.Add(new ParameterDefinition
        {
            Id = code,
            Name = name,
            UnitCode = unit,
            PhysicalMin = physMin,
            PhysicalMax = physMax,
            OptimalMin = optMin,
            OptimalMinExclusive = optMinExclusive,
            OptimalMax = optMax,
            OptimalMaxExclusive = optMaxExclusive,
            GuidanceFrequency = frequency,
            SortOrder = order,
            RequiredAtStocking = requiredAtStocking,
            IsActive = true,
            Source = Source,
            LowRecommendation = DefaultRecommendations.ByParameter.TryGetValue(code, out var advice) ? advice.Low : null,
            HighRecommendation = DefaultRecommendations.ByParameter.TryGetValue(code, out var advice2) ? advice2.High : null,
        });
    }
}
