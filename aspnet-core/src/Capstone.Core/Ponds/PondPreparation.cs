using Capstone.Domain;
using System;

namespace Capstone.Ponds;

public class PondPreparationRound : FarmEntity
{
    public Guid PondId { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? PreparedAtUtc { get; set; }
    public long? PreparedByUserId { get; set; }
    public DateTime? UsedAtUtc { get; set; }

    public const int StaleAfterDays = 30;

    public bool IsOpen => UsedAtUtc == null;
    public bool IsPrepared => PreparedAtUtc != null;
}

public class PondPreparationEntry : FarmEntity
{
    public const int MaxDescriptionLength = 1000;

    public Guid RoundId { get; set; }
    public DateTime ObservedAtUtc { get; set; }
    public string Description { get; set; }

    public Guid ClientRequestId { get; set; } = Guid.NewGuid();
}
