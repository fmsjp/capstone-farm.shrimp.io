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

namespace Capstone.Compliance;

public class DocumentPresetDto
{
    public Guid Id { get; set; }
    public Guid CategoryId { get; set; }

    [Required, StringLength(DocumentPreset.MaxNameLength)]
    public string Name { get; set; }

    [StringLength(DocumentPreset.MaxAgencyLength)]
    public string DefaultIssuingAgency { get; set; }

    public bool IsRequired { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public class DocumentCategoryDto
{
    public Guid Id { get; set; }

    [Required, StringLength(DocumentCategory.MaxNameLength)]
    public string Name { get; set; }

    public bool RequiresExpiryDate { get; set; }
    public bool IsActive { get; set; } = true;
    public int DocumentCount { get; set; }
    public List<DocumentPresetDto> Presets { get; set; } = new();
}

public class ComplianceDocumentDto
{
    public Guid Id { get; set; }
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; }
    public Guid? PresetId { get; set; }
    public string Title { get; set; }
    public string DocumentNumber { get; set; }
    public string IssuingAgency { get; set; }
    public DateOnly? IssueDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public bool IsPerpetual { get; set; }
    public int? DaysToExpiry { get; set; }
    public ExpiryStatus ExpiryStatus { get; set; }
    public string Notes { get; set; }
    public bool IsArchived { get; set; }
    public Guid? RenewedFromId { get; set; }
    public DateTime CreationTime { get; set; }
    public string RecordedBy { get; set; }
    public DateTime? LastModificationTime { get; set; }
    public string LastModifiedBy { get; set; }
}

public class GetDocumentsInput
{
    public Guid? CategoryId { get; set; }

    public ExpiryStatus? Status { get; set; }

    public bool IncludeArchived { get; set; }

    [StringLength(100)]
    public string Search { get; set; }
}

public class SaveDocumentInput
{
    public Guid Id { get; set; }

    public Guid CategoryId { get; set; }
    public Guid? PresetId { get; set; }

    [StringLength(ComplianceDocument.MaxTitleLength)]
    public string Title { get; set; }

    [Required, StringLength(ComplianceDocument.MaxNumberLength)]
    public string DocumentNumber { get; set; }

    [StringLength(ComplianceDocument.MaxAgencyLength)]
    public string IssuingAgency { get; set; }

    public DateOnly? IssueDate { get; set; }

    public bool IsPerpetual { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    [StringLength(ComplianceDocument.MaxNotesLength)]
    public string Notes { get; set; }
}

public class RenewDocumentInput : SaveDocumentInput
{
    public Guid RenewedFromId { get; set; }
}

public class ArchiveDocumentInput
{
    public Guid Id { get; set; }
    public bool IsArchived { get; set; }
}

public class ComplianceStatusDto
{
    public List<RequiredPermitStatusDto> RequiredPermits { get; set; } = new();
    public List<string> MissingPermits { get; set; } = new();
    public int ActiveCount { get; set; }
    public int ExpiringSoonCount { get; set; }
    public int ExpiredCount { get; set; }
    public int NoExpiryCount { get; set; }
}

public class RequiredPermitStatusDto
{
    public Guid PresetId { get; set; }
    public string Name { get; set; }
    public string CategoryName { get; set; }
    public bool IsSatisfied { get; set; }
    public DateOnly? ValidUntil { get; set; }
}

[AbpAuthorize(PermissionNames.Compliance)]
public class ComplianceAppService : CapstoneAppServiceBase
{
    private readonly IRepository<DocumentCategory, Guid> _categories;
    private readonly IRepository<DocumentPreset, Guid> _presets;
    private readonly IRepository<ComplianceDocument, Guid> _documents;
    private readonly ComplianceChecker _checker;

    public ComplianceAppService(IRepository<DocumentCategory, Guid> categories, IRepository<DocumentPreset, Guid> presets,
        IRepository<ComplianceDocument, Guid> documents, ComplianceChecker checker)
    {
        _categories = categories;
        _presets = presets;
        _documents = documents;
        _checker = checker;
    }

    public virtual async Task<List<DocumentCategoryDto>> GetCategories()
    {
        var counts = await _documents.GetAll().Where(d => !d.IsArchived).GroupBy(d => d.CategoryId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        var list = await _categories.GetAll().AsNoTracking().OrderBy(c => c.Name).ToListAsync();
        var presets = await _presets.GetAll().AsNoTracking().OrderBy(p => p.SortOrder).ThenBy(p => p.Name).ToListAsync();
        return list.Select(c => new DocumentCategoryDto
        {
            Id = c.Id, Name = c.Name, RequiresExpiryDate = c.RequiresExpiryDate, IsActive = c.IsActive,
            DocumentCount = counts.TryGetValue(c.Id, out var n) ? n : 0,
            Presets = presets.Where(p => p.CategoryId == c.Id).Select(p => new DocumentPresetDto
            {
                Id = p.Id, CategoryId = p.CategoryId, Name = p.Name, DefaultIssuingAgency = p.DefaultIssuingAgency,
                IsRequired = p.IsRequired, IsActive = p.IsActive, SortOrder = p.SortOrder,
            }).ToList(),
        }).ToList();
    }

    public virtual async Task<DocumentCategoryDto> SaveCategory(DocumentCategoryDto input)
    {
        var name = input.Name.Trim();
        if (await _categories.GetAll().AnyAsync(c => c.Name == name && c.Id != input.Id))
        {
            throw new UserFriendlyException($"A category named '{name}' already exists.");
        }

        DocumentCategory category;
        if (input.Id == Guid.Empty)
        {
            category = new DocumentCategory();
            await _categories.InsertAsync(category);
        }
        else
        {
            category = await _categories.GetAsync(input.Id);
        }

        category.Name = name;
        category.RequiresExpiryDate = input.RequiresExpiryDate;
        category.IsActive = input.IsActive;
        await CurrentUnitOfWork.SaveChangesAsync();
        input.Id = category.Id;
        return input;
    }

    public virtual async Task<DocumentPresetDto> SavePreset(DocumentPresetDto input)
    {
        var name = input.Name.Trim();
        _ = await _categories.FirstOrDefaultAsync(input.CategoryId) ?? throw new UserFriendlyException("Category not found.");
        if (await _presets.GetAll().AnyAsync(p => p.CategoryId == input.CategoryId && p.Name == name && p.Id != input.Id))
        {
            throw new UserFriendlyException($"'{name}' is already a preset in this category.");
        }

        DocumentPreset preset;
        if (input.Id == Guid.Empty)
        {
            var max = await _presets.GetAll().Where(p => p.CategoryId == input.CategoryId).MaxAsync(p => (int?)p.SortOrder) ?? 0;
            preset = new DocumentPreset { CategoryId = input.CategoryId, SortOrder = max + 1 };
            await _presets.InsertAsync(preset);
        }
        else
        {
            preset = await _presets.GetAsync(input.Id);
            if (preset.CategoryId != input.CategoryId)
            {
                throw new UserFriendlyException("A preset cannot be moved to another category.");
            }
        }

        preset.Name = name;
        preset.DefaultIssuingAgency = string.IsNullOrWhiteSpace(input.DefaultIssuingAgency) ? null : input.DefaultIssuingAgency.Trim();
        preset.IsRequired = input.IsRequired;
        preset.IsActive = input.IsActive;
        await CurrentUnitOfWork.SaveChangesAsync();
        input.Id = preset.Id;
        input.SortOrder = preset.SortOrder;
        return input;
    }

    public virtual async Task<List<ComplianceDocumentDto>> GetDocuments(GetDocumentsInput input)
    {
        var profile = await Farm.GetProfileAsync();
        var today = await Farm.GetTodayAsync();
        var search = input.Search?.Trim();
        var docs = await _documents.GetAll().AsNoTracking().Include(d => d.Category)
            .Where(d => input.IncludeArchived || !d.IsArchived)
            .Where(d => !input.CategoryId.HasValue || d.CategoryId == input.CategoryId)
            .Where(d => string.IsNullOrEmpty(search) || d.Title.Contains(search) || d.DocumentNumber.Contains(search) || (d.IssuingAgency != null && d.IssuingAgency.Contains(search)))
            .ToListAsync();

        var names = await GetUserNamesAsync(docs.SelectMany(d => new[] { d.CreatorUserId, d.LastModifierUserId }));
        return docs
            .Select(d => Map(d, today, profile.DocumentExpiryAlertDays, names))
            .Where(d => !input.Status.HasValue || d.ExpiryStatus == input.Status.Value)
            .OrderBy(d => d.IsArchived)
            .ThenBy(d => d.ExpiryStatus switch { ExpiryStatus.Expired => 0, ExpiryStatus.ExpiringSoon => 1, ExpiryStatus.Active => 2, _ => 3 })
            .ThenBy(d => d.ExpiryDate)
            .ThenBy(d => d.CategoryName).ThenBy(d => d.Title)
            .ToList();
    }

    public virtual async Task<ComplianceDocumentDto> GetDocument(Guid id)
    {
        var profile = await Farm.GetProfileAsync();
        var d = await _documents.GetAll().AsNoTracking().Include(x => x.Category).FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new UserFriendlyException("Compliance record not found.");
        var names = await GetUserNamesAsync(new[] { d.CreatorUserId, d.LastModifierUserId });
        return Map(d, await Farm.GetTodayAsync(), profile.DocumentExpiryAlertDays, names);
    }

    public virtual async Task<Guid> CreateDocument(SaveDocumentInput input)
    {
        var d = new ComplianceDocument();
        await ApplyAsync(d, input, exceptId: null);
        return await _documents.InsertAndGetIdAsync(d);
    }

    public virtual async Task UpdateDocument(SaveDocumentInput input)
    {
        var d = await _documents.FirstOrDefaultAsync(input.Id) ?? throw new UserFriendlyException("Compliance record not found.");
        if (d.IsArchived)
        {
            throw new UserFriendlyException("Restore the record before editing it.");
        }

        await ApplyAsync(d, input, exceptId: d.Id);
    }

    public virtual async Task<Guid> RenewDocument(RenewDocumentInput input)
    {
        var old = await _documents.FirstOrDefaultAsync(input.RenewedFromId) ?? throw new UserFriendlyException("Compliance record not found.");
        if (old.IsArchived)
        {
            throw new UserFriendlyException("This record is archived; it may already have been renewed.");
        }

        var renewed = new ComplianceDocument { RenewedFromId = old.Id };
        await ApplyAsync(renewed, input, exceptId: old.Id);

        old.IsArchived = true;
        await CurrentUnitOfWork.SaveChangesAsync();
        return await _documents.InsertAndGetIdAsync(renewed);
    }

    public virtual async Task SetArchived(ArchiveDocumentInput input)
    {
        var d = await _documents.GetAsync(input.Id);
        if (d.IsArchived == input.IsArchived)
        {
            return;
        }

        if (!input.IsArchived)
        {
            await EnsureUniqueNumberAsync(d.CategoryId, d.DocumentNumber, d.Id);
        }

        d.IsArchived = input.IsArchived;
    }

    public virtual async Task<ComplianceStatusDto> GetStatus()
    {
        var profile = await Farm.GetProfileAsync();
        var today = await Farm.GetTodayAsync();
        var required = await _presets.GetAll().AsNoTracking().Include(p => p.Category)
            .Where(p => p.IsActive && p.IsRequired && p.Category.IsActive)
            .OrderBy(p => p.Category.Name).ThenBy(p => p.SortOrder).ToListAsync();
        var docs = await _documents.GetAll().AsNoTracking().Where(d => !d.IsArchived).ToListAsync();

        var result = new ComplianceStatusDto();
        foreach (var p in required)
        {
            var current = docs.Where(d => d.PresetId == p.Id && d.IsCurrent(today)).ToList();
            result.RequiredPermits.Add(new RequiredPermitStatusDto
            {
                PresetId = p.Id,
                Name = p.Name,
                CategoryName = p.Category.Name,
                IsSatisfied = current.Count > 0,
                ValidUntil = current.Count == 0 || current.Any(d => !d.ExpiryDate.HasValue) ? null : current.Max(d => d.ExpiryDate),
            });
        }

        result.MissingPermits = await _checker.GetMissingRequiredPermitsAsync(today);
        foreach (var d in docs)
        {
            switch (d.GetExpiryStatus(today, profile.DocumentExpiryAlertDays))
            {
                case ExpiryStatus.Active: result.ActiveCount++; break;
                case ExpiryStatus.ExpiringSoon: result.ExpiringSoonCount++; break;
                case ExpiryStatus.Expired: result.ExpiredCount++; break;
                default: result.NoExpiryCount++; break;
            }
        }

        return result;
    }

    private async Task ApplyAsync(ComplianceDocument d, SaveDocumentInput input, Guid? exceptId)
    {
        var category = await _categories.FirstOrDefaultAsync(input.CategoryId) ?? throw new UserFriendlyException("Choose a category.");
        if (!category.IsActive && d.CategoryId != category.Id)
        {
            throw new UserFriendlyException($"The category '{category.Name}' is inactive.");
        }

        DocumentPreset preset = null;
        if (input.PresetId.HasValue)
        {
            preset = await _presets.FirstOrDefaultAsync(input.PresetId.Value);
            if (preset == null || preset.CategoryId != category.Id)
            {
                throw new UserFriendlyException("The document title does not belong to the chosen category.");
            }

            if (!preset.IsActive && d.PresetId != preset.Id)
            {
                throw new UserFriendlyException($"The preset '{preset.Name}' is inactive.");
            }
        }

        var title = string.IsNullOrWhiteSpace(input.Title) ? preset?.Name : input.Title.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new UserFriendlyException("Enter a document title or choose a preset.");
        }

        var number = input.DocumentNumber?.Trim();
        if (string.IsNullOrWhiteSpace(number))
        {
            throw new UserFriendlyException("The document / reference number is required.");
        }

        var today = await Farm.GetTodayAsync();
        if (input.IssueDate.HasValue && input.IssueDate.Value > today)
        {
            throw new UserFriendlyException("The issue date cannot be in the future.");
        }

        DateOnly? expiry;
        if (input.IsPerpetual)
        {
            if (category.RequiresExpiryDate)
            {
                throw new UserFriendlyException($"Records in '{category.Name}' must have an expiry date; they cannot be perpetual.");
            }

            expiry = null;
        }
        else
        {
            expiry = input.ExpiryDate ?? throw new UserFriendlyException("Enter an expiry date, or tick Perpetual / No expiry date.");
            if (input.IssueDate.HasValue && expiry.Value < input.IssueDate.Value)
            {
                throw new UserFriendlyException("The expiry date cannot be before the issue date.");
            }
        }

        await EnsureUniqueNumberAsync(category.Id, number, exceptId);

        d.CategoryId = category.Id;
        d.PresetId = preset?.Id;
        d.Title = title;
        d.DocumentNumber = number;
        d.IssuingAgency = string.IsNullOrWhiteSpace(input.IssuingAgency) ? null : input.IssuingAgency.Trim();
        d.IssueDate = input.IssueDate;
        d.ExpiryDate = expiry;
        d.Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim();
    }

    private async Task EnsureUniqueNumberAsync(Guid categoryId, string number, Guid? exceptId)
    {
        if (await _documents.GetAll().AnyAsync(x => x.CategoryId == categoryId && !x.IsArchived && x.DocumentNumber == number && x.Id != exceptId))
        {
            throw new UserFriendlyException($"Reference number '{number}' is already recorded in this category. Edit or renew that record instead.");
        }
    }

    internal static ComplianceDocumentDto Map(ComplianceDocument d, DateOnly today, int alertDays, Dictionary<long, string> names) => new()
    {
        Id = d.Id,
        CategoryId = d.CategoryId,
        CategoryName = d.Category?.Name,
        PresetId = d.PresetId,
        Title = d.Title,
        DocumentNumber = d.DocumentNumber,
        IssuingAgency = d.IssuingAgency,
        IssueDate = d.IssueDate,
        ExpiryDate = d.ExpiryDate,
        IsPerpetual = d.IsPerpetual,
        DaysToExpiry = d.ExpiryDate.HasValue ? d.ExpiryDate.Value.DayNumber - today.DayNumber : null,
        ExpiryStatus = d.GetExpiryStatus(today, alertDays),
        Notes = d.Notes,
        IsArchived = d.IsArchived,
        RenewedFromId = d.RenewedFromId,
        CreationTime = d.CreationTime,
        RecordedBy = NameOf(names, d.CreatorUserId),
        LastModificationTime = d.LastModificationTime,
        LastModifiedBy = NameOf(names, d.LastModifierUserId),
    };
}
