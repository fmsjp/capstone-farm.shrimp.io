using Abp.Domain.Entities;
using Capstone.Domain;
using System;

namespace Capstone.Alerts;

public enum FarmNotificationType
{
    DocumentExpiring = 1,
    DocumentExpired = 2,
    WaterQualityAlert = 3,
    LowStock = 4,
    NegativeStock = 5,
    HighMortality = 6,
}

public class FarmNotification : FarmChildEntity
{
    public const int MaxMessageLength = 500;
    public const int MaxTypeLength = 50;

    public FarmNotificationType Type { get; set; }
    public string RelatedRecordType { get; set; }
    public Guid? RelatedRecordId { get; set; }
    public string Message { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public bool OwnersOnly { get; set; }
}

public class FarmNotificationRead : Entity<Guid>, IMustHaveTenant
{
    public int TenantId { get; set; }
    public Guid NotificationId { get; set; }
    public long UserId { get; set; }
    public DateTime ReadAtUtc { get; set; }
}
