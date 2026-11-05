using Abp.Dependency;
using Abp.Domain.Repositories;
using Capstone.Domain;
using Capstone.Ponds;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Capstone.ReferenceData;

public class FarmParameterRange : FarmEntity
{
    public string ParameterCode { get; set; }
    public decimal? OptimalMin { get; set; }
    public decimal? OptimalMax { get; set; }
}

public class FarmDensityBand : FarmEntity
{
    public FarmingSystem FarmingSystem { get; set; }
    public PondConstructionType ConstructionType { get; set; }
    public decimal? MinPlM2 { get; set; }
    public decimal? MaxPlM2 { get; set; }
}

public class FarmThreshold : FarmEntity
{
    public const int MaxCodeLength = 40;

    public string Code { get; set; }
    public decimal Value { get; set; }
}

public record ThresholdDefinition(string Code, string Name, string Unit, decimal Default, decimal Min, decimal Max, string Source);

public static class FarmThresholds
{
    public const string PlAgeMin = "PlAgeMin";
    public const string PlAgeMax = "PlAgeMax";
    public const string FeedSpikeMultiplier = "FeedSpikeMultiplier";
    public const string DailyMortalityPct = "DailyMortalityPct";
    public const string AbwDropPct = "AbwDropPct";
    public const string BuyerWeightDiffPct = "BuyerWeightDiffPct";

    public const int DefaultStockingWindowDays = 1;
    public const int MaxStockingWindowDays = 3;

    private const string TeamDefault = "Capstone team default † (to confirm with the partner farm)";

    public static readonly IReadOnlyList<ThresholdDefinition> All = new[]
    {
        new ThresholdDefinition(PlAgeMin, "Youngest PL age for stocking", "days", 10, 1, 60, TeamDefault),
        new ThresholdDefinition(PlAgeMax, "Oldest PL age for stocking", "days", 20, 1, 60, TeamDefault),
        new ThresholdDefinition(FeedSpikeMultiplier, "Feeding session above the 7-day average by", "×", 3, 1.5m, 10, TeamDefault),
        new ThresholdDefinition(DailyMortalityPct, "Daily mortality above (share of estimated population)", "%", 1, 0.1m, 50, TeamDefault),
        new ThresholdDefinition(AbwDropPct, "ABW drop from the previous sample above", "%", 20, 1, 90, TeamDefault),
        new ThresholdDefinition(BuyerWeightDiffPct, "Buyer's accepted kg differs from farm kg by more than", "%", 10, 1, 50, TeamDefault),
    };

    public static ThresholdDefinition Find(string code) => All.FirstOrDefault(t => t.Code == code);
}

public class FarmStandardsProvider : ITransientDependency
{
    private readonly IRepository<FarmThreshold, Guid> _thresholds;

    public FarmStandardsProvider(IRepository<FarmThreshold, Guid> thresholds)
    {
        _thresholds = thresholds;
    }

    public async Task<decimal> GetAsync(string code)
    {
        var definition = FarmThresholds.Find(code) ?? throw new ArgumentException($"Unknown threshold {code}.");
        var own = await _thresholds.GetAll().AsNoTracking().Where(t => t.Code == code).Select(t => (decimal?)t.Value).FirstOrDefaultAsync();
        return own ?? definition.Default;
    }
}
