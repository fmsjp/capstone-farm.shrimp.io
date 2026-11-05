using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.UI;
using Capstone.Authorization;
using Capstone.Logbooks;
using Capstone.Logbooks.Dto;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Capstone.ReferenceData;

[AbpAuthorize(PermissionNames.ReferenceData)]
public class ReferenceDataAppService : CapstoneAppServiceBase
{
    private readonly WaterQualityEvaluator _waterQuality;

    public ReferenceDataAppService(WaterQualityEvaluator waterQuality)
    {
        _waterQuality = waterQuality;
    }

    public virtual async Task<List<ParameterDto>> GetParameters()
    {
        var list = (await _waterQuality.GetParametersAsync()).Values.OrderBy(p => p.SortOrder);
        return list.Select(LogAppService.MapParameter).ToList();
    }
}

public class UpdateParameterInput
{
    [Required]
    public string Code { get; set; }

    [Required, StringLength(ParameterDefinition.MaxNameLength)]
    public string Name { get; set; }

    [Required, StringLength(ParameterDefinition.MaxUnitLength)]
    public string UnitCode { get; set; }

    public decimal PhysicalMin { get; set; }
    public decimal PhysicalMax { get; set; }
    public decimal? OptimalMin { get; set; }
    public bool OptimalMinExclusive { get; set; }
    public decimal? OptimalMax { get; set; }
    public bool OptimalMaxExclusive { get; set; }

    [StringLength(100)]
    public string GuidanceFrequency { get; set; }

    [StringLength(ParameterDefinition.MaxRecommendationLength)]
    public string LowRecommendation { get; set; }

    [StringLength(ParameterDefinition.MaxRecommendationLength)]
    public string HighRecommendation { get; set; }

    public bool IsActive { get; set; }
}

[AbpAuthorize(PermissionNames.System_ReferenceData)]
public class SystemReferenceDataAppService : CapstoneAppServiceBase
{
    private readonly IRepository<ParameterDefinition, string> _parameters;

    public SystemReferenceDataAppService(IRepository<ParameterDefinition, string> parameters)
    {
        _parameters = parameters;
    }

    public virtual async Task<List<ParameterDto>> GetParameters()
    {
        var list = await _parameters.GetAll().AsNoTracking().OrderBy(p => p.SortOrder).ToListAsync();
        return list.Select(LogAppService.MapParameter).ToList();
    }

    public virtual async Task UpdateParameter(UpdateParameterInput input)
    {
        if (input.PhysicalMin >= input.PhysicalMax)
        {
            throw new UserFriendlyException("The physical minimum must be below the physical maximum.");
        }

        if ((input.OptimalMin.HasValue && (input.OptimalMin < input.PhysicalMin || input.OptimalMin > input.PhysicalMax))
            || (input.OptimalMax.HasValue && (input.OptimalMax < input.PhysicalMin || input.OptimalMax > input.PhysicalMax))
            || (input.OptimalMin.HasValue && input.OptimalMax.HasValue && input.OptimalMin > input.OptimalMax))
        {
            throw new UserFriendlyException("The optimal range must lie within the physical limits, with minimum not above maximum.");
        }

        var p = await _parameters.GetAsync(input.Code);
        p.Name = input.Name.Trim();
        p.UnitCode = input.UnitCode.Trim();
        p.PhysicalMin = input.PhysicalMin;
        p.PhysicalMax = input.PhysicalMax;
        p.OptimalMin = input.OptimalMin;
        p.OptimalMinExclusive = input.OptimalMinExclusive;
        p.OptimalMax = input.OptimalMax;
        p.OptimalMaxExclusive = input.OptimalMaxExclusive;
        p.GuidanceFrequency = input.GuidanceFrequency;
        p.LowRecommendation = string.IsNullOrWhiteSpace(input.LowRecommendation) ? null : input.LowRecommendation.Trim();
        p.HighRecommendation = string.IsNullOrWhiteSpace(input.HighRecommendation) ? null : input.HighRecommendation.Trim();
        p.IsActive = input.IsActive;
        p.LastModificationTime = DateTime.UtcNow;
    }
}
