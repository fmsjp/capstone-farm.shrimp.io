using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.UI;
using Capstone.Authorization;
using Capstone.Farms;
using Capstone.Ponds;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Capstone.ReferenceData;

public class StandardParameterDto
{
    public string Code { get; set; }
    public string Name { get; set; }
    public string Unit { get; set; }
    public decimal PhysicalMin { get; set; }
    public decimal PhysicalMax { get; set; }
    public decimal? StandardMin { get; set; }
    public decimal? StandardMax { get; set; }
    public string StandardRange { get; set; }
    public string Source { get; set; }
    public decimal? FarmMin { get; set; }
    public decimal? FarmMax { get; set; }

    public bool Differs { get; set; }
}

public class StandardDensityBandDto
{
    public FarmingSystem FarmingSystem { get; set; }
    public PondConstructionType ConstructionType { get; set; }
    public decimal? StandardMin { get; set; }
    public decimal? StandardMax { get; set; }
    public string Source { get; set; }
    public decimal? FarmMin { get; set; }
    public decimal? FarmMax { get; set; }
    public bool Differs { get; set; }
}

public class StandardThresholdDto
{
    public string Code { get; set; }
    public string Name { get; set; }
    public string Unit { get; set; }
    public decimal Default { get; set; }
    public decimal Min { get; set; }
    public decimal Max { get; set; }
    public string Source { get; set; }
    public decimal? FarmValue { get; set; }
    public bool Differs { get; set; }
}

public class FarmStandardsDto
{
    public List<StandardParameterDto> Parameters { get; set; } = new();
    public List<StandardDensityBandDto> DensityBands { get; set; } = new();
    public List<StandardThresholdDto> Thresholds { get; set; } = new();
    public DateTime? ReviewedAtUtc { get; set; }
    public bool CanEdit { get; set; }
}

public class SetParameterRangeInput
{
    public string Code { get; set; }
    public decimal? OptimalMin { get; set; }
    public decimal? OptimalMax { get; set; }
}

public class SetDensityBandInput
{
    public FarmingSystem FarmingSystem { get; set; }
    public PondConstructionType ConstructionType { get; set; }
    public decimal? MinPlM2 { get; set; }
    public decimal? MaxPlM2 { get; set; }
}

public class DensityBandKeyInput
{
    public FarmingSystem FarmingSystem { get; set; }
    public PondConstructionType ConstructionType { get; set; }
}

public class SetThresholdInput
{
    public string Code { get; set; }
    public decimal Value { get; set; }
}

public class CodeInput
{
    public string Code { get; set; }
}

[AbpAuthorize(PermissionNames.FarmProfile)]
public class FarmStandardsAppService : CapstoneAppServiceBase
{
    private readonly WaterQualityEvaluator _waterQuality;
    private readonly IRepository<FarmParameterRange, Guid> _ranges;
    private readonly IRepository<DensityBand, int> _bands;
    private readonly IRepository<FarmDensityBand, Guid> _farmBands;
    private readonly IRepository<FarmThreshold, Guid> _thresholds;
    private readonly IRepository<FarmProfile, int> _profiles;

    public FarmStandardsAppService(WaterQualityEvaluator waterQuality, IRepository<FarmParameterRange, Guid> ranges, IRepository<DensityBand, int> bands,
        IRepository<FarmDensityBand, Guid> farmBands, IRepository<FarmThreshold, Guid> thresholds, IRepository<FarmProfile, int> profiles)
    {
        _waterQuality = waterQuality;
        _ranges = ranges;
        _bands = bands;
        _farmBands = farmBands;
        _thresholds = thresholds;
        _profiles = profiles;
    }

    public virtual async Task<FarmStandardsDto> Get()
    {
        var dto = new FarmStandardsDto { CanEdit = await IsGrantedAsync(PermissionNames.FarmProfile_Edit) };
        var ranges = await _ranges.GetAll().AsNoTracking().ToListAsync();
        foreach (var p in (await _waterQuality.GetStandardParametersAsync()).Where(p => p.IsActive))
        {
            var own = ranges.FirstOrDefault(r => r.ParameterCode == p.Id);
            dto.Parameters.Add(new StandardParameterDto
            {
                Code = p.Id, Name = p.Name, Unit = p.UnitCode, PhysicalMin = p.PhysicalMin, PhysicalMax = p.PhysicalMax,
                StandardMin = p.OptimalMin, StandardMax = p.OptimalMax, StandardRange = p.DescribeOptimalRange(), Source = p.Source,
                FarmMin = own?.OptimalMin, FarmMax = own?.OptimalMax, Differs = own != null,
            });
        }

        var farmBands = await _farmBands.GetAll().AsNoTracking().ToListAsync();
        foreach (var b in await _bands.GetAll().AsNoTracking().OrderBy(b => b.FarmingSystem).ThenBy(b => b.ConstructionType).ToListAsync())
        {
            var own = farmBands.FirstOrDefault(f => f.FarmingSystem == b.FarmingSystem && f.ConstructionType == b.ConstructionType);
            dto.DensityBands.Add(new StandardDensityBandDto
            {
                FarmingSystem = b.FarmingSystem, ConstructionType = b.ConstructionType, StandardMin = b.MinPlM2, StandardMax = b.MaxPlM2, Source = b.Source,
                FarmMin = own?.MinPlM2, FarmMax = own?.MaxPlM2, Differs = own != null,
            });
        }

        var thresholds = await _thresholds.GetAll().AsNoTracking().ToListAsync();
        foreach (var t in FarmThresholds.All)
        {
            var own = thresholds.FirstOrDefault(x => x.Code == t.Code);
            dto.Thresholds.Add(new StandardThresholdDto
            {
                Code = t.Code, Name = t.Name, Unit = t.Unit, Default = t.Default, Min = t.Min, Max = t.Max, Source = t.Source,
                FarmValue = own?.Value, Differs = own != null,
            });
        }

        var profile = await Farm.GetProfileAsync();
        dto.ReviewedAtUtc = profile.StandardsReviewedAtUtc;
        return dto;
    }

    [AbpAuthorize(PermissionNames.FarmProfile_Edit)]
    public virtual async Task SetParameterRange(SetParameterRangeInput input)
    {
        var p = (await _waterQuality.GetStandardParametersAsync()).FirstOrDefault(x => x.Id == input.Code) ?? throw new UserFriendlyException("Unknown parameter.");
        if (!input.OptimalMin.HasValue && !input.OptimalMax.HasValue)
        {
            throw new UserFriendlyException("Enter a minimum, a maximum, or both. Use \"Reset to standard\" to go back to the default.");
        }

        foreach (var v in new[] { input.OptimalMin, input.OptimalMax }.Where(v => v.HasValue))
        {
            if (!p.IsPhysicallyPossible(v!.Value))
            {
                throw new UserFriendlyException($"{p.Name} values must be between {p.PhysicalMin:0.###} and {p.PhysicalMax:0.###} {p.UnitCode}.");
            }
        }

        if (input.OptimalMin > input.OptimalMax)
        {
            throw new UserFriendlyException("The minimum cannot be above the maximum.");
        }

        var own = await _ranges.FirstOrDefaultAsync(r => r.ParameterCode == p.Id);
        if (own == null)
        {
            await _ranges.InsertAsync(new FarmParameterRange { ParameterCode = p.Id, OptimalMin = input.OptimalMin, OptimalMax = input.OptimalMax });
        }
        else
        {
            own.OptimalMin = input.OptimalMin;
            own.OptimalMax = input.OptimalMax;
        }
    }

    [AbpAuthorize(PermissionNames.FarmProfile_Edit)]
    public virtual async Task ResetParameterRange(CodeInput input)
    {
        var own = await _ranges.FirstOrDefaultAsync(r => r.ParameterCode == input.Code);
        if (own != null)
        {
            await _ranges.HardDeleteAsync(own);
        }
    }

    [AbpAuthorize(PermissionNames.FarmProfile_Edit)]
    public virtual async Task SetDensityBand(SetDensityBandInput input)
    {
        if (!Enum.IsDefined(input.FarmingSystem) || !Enum.IsDefined(input.ConstructionType))
        {
            throw new UserFriendlyException("Choose the farming system and construction type.");
        }

        if (!input.MinPlM2.HasValue && !input.MaxPlM2.HasValue)
        {
            throw new UserFriendlyException("Enter a minimum, a maximum, or both.");
        }

        if (input.MinPlM2 is <= 0 or > 1000 || input.MaxPlM2 is <= 0 or > 1000)
        {
            throw new UserFriendlyException("Densities must be between 0 and 1,000 PL/m².");
        }

        if (input.MinPlM2 > input.MaxPlM2)
        {
            throw new UserFriendlyException("The minimum cannot be above the maximum.");
        }

        var own = await _farmBands.FirstOrDefaultAsync(b => b.FarmingSystem == input.FarmingSystem && b.ConstructionType == input.ConstructionType);
        if (own == null)
        {
            await _farmBands.InsertAsync(new FarmDensityBand { FarmingSystem = input.FarmingSystem, ConstructionType = input.ConstructionType, MinPlM2 = input.MinPlM2, MaxPlM2 = input.MaxPlM2 });
        }
        else
        {
            own.MinPlM2 = input.MinPlM2;
            own.MaxPlM2 = input.MaxPlM2;
        }
    }

    [AbpAuthorize(PermissionNames.FarmProfile_Edit)]
    public virtual async Task ResetDensityBand(DensityBandKeyInput input)
    {
        var own = await _farmBands.FirstOrDefaultAsync(b => b.FarmingSystem == input.FarmingSystem && b.ConstructionType == input.ConstructionType);
        if (own != null)
        {
            await _farmBands.HardDeleteAsync(own);
        }
    }

    [AbpAuthorize(PermissionNames.FarmProfile_Edit)]
    public virtual async Task SetThreshold(SetThresholdInput input)
    {
        var t = FarmThresholds.Find(input.Code) ?? throw new UserFriendlyException("Unknown threshold.");
        if (input.Value < t.Min || input.Value > t.Max)
        {
            throw new UserFriendlyException($"{t.Name} must be between {t.Min:0.##} and {t.Max:0.##} {t.Unit}.");
        }

        var own = await _thresholds.FirstOrDefaultAsync(x => x.Code == t.Code);
        var other = t.Code switch
        {
            FarmThresholds.PlAgeMin => (Code: FarmThresholds.PlAgeMax, IsMin: true),
            FarmThresholds.PlAgeMax => (Code: FarmThresholds.PlAgeMin, IsMin: false),
            _ => (Code: (string)null, IsMin: false),
        };
        if (other.Code != null)
        {
            var otherValue = (await _thresholds.FirstOrDefaultAsync(x => x.Code == other.Code))?.Value ?? FarmThresholds.Find(other.Code).Default;
            if (other.IsMin ? input.Value > otherValue : input.Value < otherValue)
            {
                throw new UserFriendlyException("The youngest PL age cannot be above the oldest.");
            }
        }

        if (own == null)
        {
            await _thresholds.InsertAsync(new FarmThreshold { Code = t.Code, Value = input.Value });
        }
        else
        {
            own.Value = input.Value;
        }
    }

    [AbpAuthorize(PermissionNames.FarmProfile_Edit)]
    public virtual async Task ResetThreshold(CodeInput input)
    {
        var own = await _thresholds.FirstOrDefaultAsync(x => x.Code == input.Code);
        if (own != null)
        {
            await _thresholds.HardDeleteAsync(own);
        }
    }

    [AbpAuthorize(PermissionNames.FarmProfile_Edit)]
    public virtual async Task MarkReviewed()
    {
        var profile = await _profiles.FirstOrDefaultAsync(p => p.TenantId == Farm.TenantId) ?? throw new UserFriendlyException("Farm profile not found.");
        profile.StandardsReviewedAtUtc = DateTime.UtcNow;
    }
}
