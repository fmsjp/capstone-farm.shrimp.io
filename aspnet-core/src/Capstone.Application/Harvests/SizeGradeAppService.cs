using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.UI;
using Capstone.Authorization;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Capstone.Harvests;

public class SizeGradeDto
{
    public Guid Id { get; set; }

    [Required, StringLength(SizeGrade.MaxLabelLength)]
    public string Label { get; set; }

    [Range(1, SizeGrade.MaxCount)]
    public int? MinCountPerKg { get; set; }

    [Range(1, SizeGrade.MaxCount)]
    public int? MaxCountPerKg { get; set; }

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

[AbpAuthorize(PermissionNames.Harvests)]
public class SizeGradeAppService : CapstoneAppServiceBase
{
    private readonly IRepository<SizeGrade, Guid> _grades;

    public SizeGradeAppService(IRepository<SizeGrade, Guid> grades)
    {
        _grades = grades;
    }

    public virtual async Task<List<SizeGradeDto>> GetAll()
    {
        var list = await _grades.GetAll().AsNoTracking()
            .OrderByDescending(g => g.IsActive).ThenBy(g => g.SortOrder).ThenBy(g => g.MinCountPerKg).ThenBy(g => g.Label).ToListAsync();
        return list.Select(g => new SizeGradeDto
        {
            Id = g.Id, Label = g.Label, MinCountPerKg = g.MinCountPerKg, MaxCountPerKg = g.MaxCountPerKg, SortOrder = g.SortOrder, IsActive = g.IsActive,
        }).ToList();
    }

    [AbpAuthorize(PermissionNames.Harvests_EditAny)]
    public virtual async Task<Guid> Save(SizeGradeDto input)
    {
        var label = input.Label.Trim();
        if (input.MinCountPerKg.HasValue && input.MaxCountPerKg.HasValue && input.MaxCountPerKg < input.MinCountPerKg)
        {
            throw new UserFriendlyException("The maximum count per kg cannot be below the minimum.");
        }

        if (await _grades.GetAll().AnyAsync(g => g.Label == label && g.Id != input.Id))
        {
            throw new UserFriendlyException($"The size grade '{label}' already exists.");
        }

        var isNew = input.Id == Guid.Empty;
        var grade = isNew ? new SizeGrade() : await _grades.GetAsync(input.Id);
        grade.Label = label;
        grade.MinCountPerKg = input.MinCountPerKg;
        grade.MaxCountPerKg = input.MaxCountPerKg;
        grade.SortOrder = input.SortOrder > 0 || !isNew ? input.SortOrder : (await _grades.GetAll().MaxAsync(g => (int?)g.SortOrder) ?? 0) + 1;
        grade.IsActive = input.IsActive;
        return isNew ? await _grades.InsertAndGetIdAsync(grade) : grade.Id;
    }
}
