using Capstone.Domain;
using System;

namespace Capstone.Compliance;

public enum ExpiryStatus
{
    NoExpiry = 0,
    Active = 1,
    ExpiringSoon = 2,
    Expired = 3,
}

public class DocumentCategory : FarmEntity
{
    public const int MaxNameLength = 100;

    public string Name { get; set; }

    public bool RequiresExpiryDate { get; set; }

    public bool IsActive { get; set; } = true;
}

public class DocumentPreset : FarmEntity
{
    public const int MaxNameLength = 200;
    public const int MaxAgencyLength = 200;

    public Guid CategoryId { get; set; }
    public DocumentCategory Category { get; set; }
    public string Name { get; set; }

    public string DefaultIssuingAgency { get; set; }

    public bool IsRequired { get; set; }

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public class ComplianceDocument : FarmEntity
{
    public const int MaxTitleLength = 200;
    public const int MaxNumberLength = 100;
    public const int MaxAgencyLength = 200;
    public const int MaxNotesLength = 500;

    public Guid CategoryId { get; set; }
    public DocumentCategory Category { get; set; }

    public Guid? PresetId { get; set; }
    public DocumentPreset Preset { get; set; }

    public string Title { get; set; }
    public string DocumentNumber { get; set; }
    public string IssuingAgency { get; set; }
    public DateOnly? IssueDate { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    public string Notes { get; set; }

    public bool IsArchived { get; set; }

    public Guid? RenewedFromId { get; set; }

    public bool IsPerpetual => !ExpiryDate.HasValue;

    public ExpiryStatus GetExpiryStatus(DateOnly today, int alertDays)
    {
        if (!ExpiryDate.HasValue)
        {
            return ExpiryStatus.NoExpiry;
        }

        if (ExpiryDate.Value < today)
        {
            return ExpiryStatus.Expired;
        }

        return ExpiryDate.Value <= today.AddDays(alertDays) ? ExpiryStatus.ExpiringSoon : ExpiryStatus.Active;
    }

    public bool IsCurrent(DateOnly today) => !IsArchived && (!ExpiryDate.HasValue || ExpiryDate.Value >= today);
}
