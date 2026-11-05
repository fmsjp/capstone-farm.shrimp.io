using Capstone.Domain;
using System;

namespace Capstone.Production;

public class FeedingSession : FarmEntity
{
    public const int MaxNotesLength = 500;
    public const int MaxVoidReasonLength = 500;
    public const int MaxFedAtLength = 5;
    public const decimal MaxKg = 1000m;

    public Guid BatchId { get; set; }
    public PondBatch Batch { get; set; }
    public DateOnly FeedDate { get; set; }
    public FeedingSessionTime Session { get; set; }
    public FeedingStatus Status { get; set; } = FeedingStatus.Fed;
    public string Notes { get; set; }

    public string FedAtTime { get; set; }

    public Guid ClientRequestId { get; set; }
    public string VoidReason { get; set; }
}
