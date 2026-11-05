using Capstone.Domain;
using System;
using System.ComponentModel.DataAnnotations;

namespace Capstone.Resources;

public enum FryDeliveryStatus { Ordered = 1, InTransit = 2, Received = 3, Rejected = 4 }

public class FryDelivery : FarmEntity
{
    public const int MaxBatchNumberLength = 100;
    public const int MaxNotesLength = 500;
    public const long MaxPl = 10_000_000;
    public const int MaxPermitLength = 100;

    public Guid HatcheryId { get; set; }
    public HatcherySource Hatchery { get; set; }

    public string HatcheryBatchNumber { get; set; }

    public DateOnly DeliveryDate { get; set; }

    public int? PlAgeDays { get; set; }

    public long PlReceived { get; set; }
    public decimal? CostPhp { get; set; }
    public string Notes { get; set; }

    public string PlHealthCertificateNo { get; set; }

    public string TransportPermitNo { get; set; }

    public string SfscNo { get; set; }

    public DateTime? StockingFinishedAtUtc { get; set; }

    public long? StockingFinishedByUserId { get; set; }

    public bool IsStockingFinished => StockingFinishedAtUtc.HasValue;

    [Timestamp]
    public byte[] RowVersion { get; set; }
}

public class FryDeliveryStatusChange : FarmEntity
{
    public const int MaxNoteLength = 500;

    public Guid DeliveryId { get; set; }
    public FryDeliveryStatus Status { get; set; }
    public DateTime ChangedAtUtc { get; set; }
    public string Note { get; set; }
}

public enum ShortfallReason { DeadOnArrival = 1, Discarded = 2, Returned = 3, CountDifference = 4 }

public class FryDeliveryShortfall : FarmEntity
{
    public const int MaxNoteLength = 500;

    public Guid DeliveryId { get; set; }
    public ShortfallReason Reason { get; set; }
    public long Count { get; set; }
    public string Note { get; set; }
}
