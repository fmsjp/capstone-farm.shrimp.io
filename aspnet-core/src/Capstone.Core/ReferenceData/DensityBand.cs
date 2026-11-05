using Abp.Dependency;
using Abp.Domain.Entities;
using Abp.Domain.Repositories;
using Capstone.Ponds;
using Capstone.Production;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace Capstone.ReferenceData;

public class DensityBand : Entity<int>
{
    public const int MaxSourceLength = 200;

    public FarmingSystem FarmingSystem { get; set; }
    public PondConstructionType ConstructionType { get; set; }
    public decimal? MinPlM2 { get; set; }
    public decimal? MaxPlM2 { get; set; }
    public string Source { get; set; }

    public DensityFlag Judge(decimal density) =>
        MinPlM2.HasValue && density < MinPlM2.Value ? DensityFlag.Below
        : MaxPlM2.HasValue && density > MaxPlM2.Value ? DensityFlag.Above
        : DensityFlag.Within;

    public string Describe() => (MinPlM2, MaxPlM2) switch
    {
        ({ } min, { } max) => $"{min:0.##}–{max:0.##} PL/m²",
        (null, { } max) => $"up to {max:0.##} PL/m²",
        ({ } min, null) => $"above {min:0.##} PL/m²",
        _ => "any density",
    };
}

public class DensityJudgement
{
    public DensityFlag? Flag { get; set; }
    public DensityBand Band { get; set; }

    public bool IsFarmValue { get; set; }

    public string Warning { get; set; }
}

public class DensityEvaluator : ITransientDependency
{
    private readonly IRepository<DensityBand, int> _bands;
    private readonly IRepository<FarmDensityBand, System.Guid> _farmBands;

    public DensityEvaluator(IRepository<DensityBand, int> bands, IRepository<FarmDensityBand, System.Guid> farmBands)
    {
        _bands = bands;
        _farmBands = farmBands;
    }

    public async Task<(DensityBand Band, bool IsFarmValue)> EffectiveBandAsync(FarmingSystem system, PondConstructionType construction)
    {
        var standard = await _bands.GetAll().AsNoTracking().FirstOrDefaultAsync(b => b.FarmingSystem == system && b.ConstructionType == construction);
        var own = await _farmBands.GetAll().AsNoTracking().FirstOrDefaultAsync(b => b.FarmingSystem == system && b.ConstructionType == construction);
        if (own == null)
        {
            return (standard, false);
        }

        return (new DensityBand { FarmingSystem = system, ConstructionType = construction, MinPlM2 = own.MinPlM2, MaxPlM2 = own.MaxPlM2, Source = "Farm Standards" }, true);
    }

    public async Task<DensityJudgement> JudgeAsync(FarmingSystem system, PondConstructionType construction, decimal? density)
    {
        var result = new DensityJudgement();
        if (!density.HasValue)
        {
            return result;
        }

        var (band, isFarmValue) = await EffectiveBandAsync(system, construction);
        if (band == null)
        {
            return result;
        }

        result.Band = band;
        result.IsFarmValue = isFarmValue;
        result.Flag = band.Judge(density.Value);
        if (result.Flag != DensityFlag.Within)
        {
            result.Warning = $"{density.Value:0.#} PL/m² is {(result.Flag == DensityFlag.Below ? "below" : "above")} the {(isFarmValue ? "farm's" : "recommended")} {band.Describe()} for "
                             + $"{Label(system)} culture in {Label(construction)} ponds ({(isFarmValue ? "Farm Standards" : band.Source)}). You can still save.";
        }

        return result;
    }

    public static string Label(FarmingSystem s) => s switch
    {
        FarmingSystem.SemiIntensive => "semi-intensive",
        _ => s.ToString().ToLowerInvariant(),
    };

    public static string Label(PondConstructionType c) => c switch
    {
        PondConstructionType.HdpeLined => "HDPE-lined",
        _ => c.ToString().ToLowerInvariant(),
    };
}
