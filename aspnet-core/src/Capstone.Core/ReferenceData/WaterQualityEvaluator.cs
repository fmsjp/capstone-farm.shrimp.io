using Abp.Domain.Repositories;
using Abp.Domain.Services;
using Abp.UI;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Capstone.ReferenceData;

public class EvaluatedReading
{
    public ParameterDefinition Parameter { get; set; }
    public decimal Value { get; set; }
    public bool IsOutOfRange { get; set; }
}

public class WaterQualityEvaluator : DomainService
{
    private readonly IRepository<ParameterDefinition, string> _parameters;
    private readonly IRepository<FarmParameterRange, System.Guid> _farmRanges;

    public WaterQualityEvaluator(IRepository<ParameterDefinition, string> parameters, IRepository<FarmParameterRange, System.Guid> farmRanges)
    {
        _parameters = parameters;
        _farmRanges = farmRanges;
    }

    public async Task<Dictionary<string, ParameterDefinition>> GetParametersAsync()
    {
        var list = await _parameters.GetAll().AsNoTracking().ToListAsync();
        var overrides = await _farmRanges.GetAll().AsNoTracking().ToListAsync();
        foreach (var o in overrides)
        {
            var p = list.FirstOrDefault(x => x.Id == o.ParameterCode);
            if (p == null)
            {
                continue;
            }

            p.OptimalMin = o.OptimalMin;
            p.OptimalMinExclusive = false;
            p.OptimalMax = o.OptimalMax;
            p.OptimalMaxExclusive = false;
            p.Source = "Farm Standards";
        }

        return list.ToDictionary(p => p.Id);
    }

    public async Task<List<ParameterDefinition>> GetStandardParametersAsync() =>
        await _parameters.GetAll().AsNoTracking().OrderBy(p => p.SortOrder).ToListAsync();

    public async Task<List<EvaluatedReading>> EvaluateAsync(IEnumerable<(string Code, decimal Value)> readings, bool requireStockingSet)
    {
        var parameters = await GetParametersAsync();
        var result = new List<EvaluatedReading>();
        var errors = new List<string>();

        foreach (var (code, value) in readings)
        {
            if (string.IsNullOrWhiteSpace(code) || !parameters.TryGetValue(code, out var parameter) || !parameter.IsActive)
            {
                errors.Add($"Unknown water-quality parameter '{code}'.");
                continue;
            }

            if (!parameter.IsPhysicallyPossible(value))
            {
                errors.Add($"{parameter.Name} {value:0.###} {parameter.UnitCode} is outside the possible range " +
                           $"{parameter.PhysicalMin:0.###}–{parameter.PhysicalMax:0.###}. Check the reading.");
                continue;
            }

            if (result.Any(r => r.Parameter.Id == parameter.Id))
            {
                errors.Add($"{parameter.Name} is entered more than once.");
                continue;
            }

            result.Add(new EvaluatedReading { Parameter = parameter, Value = value, IsOutOfRange = parameter.IsOutOfOptimalRange(value) });
        }

        if (requireStockingSet)
        {
            foreach (var code in ParameterCodes.RequiredAtStocking)
            {
                if (result.All(r => r.Parameter.Id != code))
                {
                    var name = parameters.TryGetValue(code, out var p) ? p.Name : code;
                    errors.Add($"Baseline {name} is required.");
                }
            }
        }
        else if (result.Count == 0 && errors.Count == 0)
        {
            errors.Add("Enter at least one water-quality reading.");
        }

        if (errors.Count > 0)
        {
            throw new UserFriendlyException("Some water-quality readings are invalid.", string.Join("\n", errors));
        }

        return result;
    }
}
