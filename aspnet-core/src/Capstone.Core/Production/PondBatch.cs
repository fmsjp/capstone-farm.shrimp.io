using Capstone.Domain;
using Capstone.Ponds;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Capstone.Production;

public class PondBatch : FarmEntity
{
    public const int MaxCodeLength = 50;
    public const int MaxLetterLength = 2;

    public Guid CycleId { get; set; }
    public ProductionCycle Cycle { get; set; }
    public Guid PondId { get; set; }
    public Pond Pond { get; set; }

    public Guid? PreparationRoundId { get; set; }

    public string Letter { get; set; }

    public string BatchCode { get; set; }

    public BatchStatus Status { get; set; } = BatchStatus.Stocked;

    public DateTime? StockedAtUtc { get; set; }

    public DateOnly? StockingDate { get; set; }

    public long? PlCountStocked { get; set; }

    public decimal PondAreaM2AtStocking { get; set; }

    public TransportMode TransportMode { get; set; } = TransportMode.Other;
    public DateTime? TimeProcessedUtc { get; set; }
    public int? AcclimationMinutes { get; set; }
    public decimal? InitialSurvivalEstimatePct { get; set; }
    public string HatcheryCertificationStatusAtStocking { get; set; }

    public long? DeadAtStocking { get; set; }

    public FarmingSystem? FarmingSystemAtStocking { get; set; }

    public PondConstructionType? ConstructionTypeAtStocking { get; set; }

    public DensityFlag? DensityFlag { get; set; }

    public decimal? FinalAbwG { get; set; }
    public decimal? FinalFcr { get; set; }
    public decimal? SurvivalRatePct { get; set; }
    public decimal? TotalHarvestKg { get; set; }
    public decimal? TotalFeedKg { get; set; }
    public int? CultureDays { get; set; }

    public DateTime? ClosedAtUtc { get; set; }
    public long? ClosedByUserId { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; }

    [NotMapped]
    public decimal? StockingDensityPerM2 => IndicatorCalculator.StockingDensity(PlCountStocked, PondAreaM2AtStocking);

    public bool IsOpen => Status != BatchStatus.Closed;

    public void EnsureNotClosed()
    {
        if (Status == BatchStatus.Closed)
        {
            throw FpmsErrors.Rule(FpmsErrors.ReadOnlyClosed);
        }
    }

    public bool AllowsNewLog(LogType type) =>
        type is not (LogType.PondPreparation or LogType.Feeding or LogType.PostProduction)
        && Status is BatchStatus.Stocked or BatchStatus.Harvested or BatchStatus.Failed;

    public bool AllowsFeeding => Status is BatchStatus.Stocked or BatchStatus.Harvested or BatchStatus.Failed;
}

public class BatchFailure : FarmEntity
{
    public const int MaxReasonLength = 1000;

    public Guid BatchId { get; set; }
    public DateOnly FailedOn { get; set; }
    public string Reason { get; set; }
    public decimal? EstimatedLossPhp { get; set; }
}
