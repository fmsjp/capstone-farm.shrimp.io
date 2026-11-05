using Capstone.Domain;
using Capstone.Resources;
using System;
using System.ComponentModel.DataAnnotations;

namespace Capstone.Production;

public class ProductionCycle : FarmEntity
{
    public const int MaxCodeLength = 50;

    public string CycleCode { get; set; }

    public CycleStatus Status { get; set; } = CycleStatus.Active;

    public Guid FryDeliveryId { get; set; }
    public FryDelivery FryDelivery { get; set; }

    public CycleOutcome? Outcome { get; set; }

    public DateTime? ClosedAtUtc { get; set; }
    public long? ClosedByUserId { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; }

    public bool IsClosed => Status == CycleStatus.Closed;
}

public class ReopenEvent : FarmEntity
{
    public const int MaxReasonLength = 500;

    public ReopenedEntityType EntityType { get; set; }
    public Guid EntityId { get; set; }
    public DateTime ReopenedAtUtc { get; set; }
    public long ReopenedByUserId { get; set; }
    public string Reason { get; set; }
}
