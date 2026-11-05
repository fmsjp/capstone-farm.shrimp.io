using Capstone.Common;
using Capstone.Ponds;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Capstone.Production.Dto;

public class IndicatorsDto
{
    public decimal? StockingDensityPerM2 { get; set; }
    public int? Doc { get; set; }
    public decimal CumulativeFeedKg { get; set; }
    public decimal? CurrentAbwG { get; set; }
    public decimal? LatestSampleAbwG { get; set; }
    public long CumulativeMortality { get; set; }
    public decimal HarvestedPieces { get; set; }
    public decimal TotalHarvestKg { get; set; }
    public decimal? EstimatedPopulation { get; set; }
    public decimal? EstimatedBiomassKg { get; set; }
    public decimal? FcrToDate { get; set; }
    public decimal? FinalFcr { get; set; }
    public decimal? SurvivalRatePct { get; set; }
    public decimal? EstimatedSurvivalPct { get; set; }

    public string PopulationBasis { get; set; }

    public int? CultureDays { get; set; }
    public decimal? FinalAbwG { get; set; }

    public static IndicatorsDto From(BatchIndicators i) => new()
    {
        StockingDensityPerM2 = i.StockingDensityPerM2,
        Doc = i.Doc,
        CumulativeFeedKg = i.CumulativeFeedKg,
        CurrentAbwG = i.CurrentAbwG,
        LatestSampleAbwG = i.LatestAbwG,
        CumulativeMortality = i.CumulativeMortality,
        HarvestedPieces = i.HarvestedPieces,
        TotalHarvestKg = i.TotalHarvestKg,
        EstimatedPopulation = i.EstimatedPopulation,
        EstimatedBiomassKg = i.EstimatedBiomassKg,
        FcrToDate = i.FcrToDate,
        FinalFcr = i.FinalFcr,
        SurvivalRatePct = i.SurvivalRatePct,
        EstimatedSurvivalPct = i.EstimatedSurvivalPct,
        PopulationBasis = i.PopulationBasis,
        CultureDays = i.CultureDays,
        FinalAbwG = i.FinalAbwG,
    };
}

public class CycleListDto
{
    public Guid Id { get; set; }
    public string CycleCode { get; set; }
    public CycleStatus Status { get; set; }
    public Guid FryDeliveryId { get; set; }
    public string HatcheryName { get; set; }
    public string HatcheryShortCode { get; set; }
    public string HatcheryBatchNumber { get; set; }

    public DateOnly? StartDate { get; set; }

    public int BatchCount { get; set; }
    public int OpenBatchCount { get; set; }
    public long PlReceived { get; set; }
    public long PlStocked { get; set; }
    public long PlLeft { get; set; }

    public CycleOutcome? Outcome { get; set; }
}

public class ReopenEventDto
{
    public DateTime ReopenedAtUtc { get; set; }
    public string ReopenedBy { get; set; }
    public string Reason { get; set; }
}

public class ReopenInput
{
    public Guid Id { get; set; }

    [Required, StringLength(ReopenEvent.MaxReasonLength, MinimumLength = 3)]
    public string Reason { get; set; }

    public string RowVersion { get; set; }
}

public class CycleDetailDto : CycleListDto
{
    public CycleOutcome SuggestedOutcome { get; set; }

    public bool CanReopen { get; set; }
    public List<ReopenEventDto> Reopenings { get; set; } = new();
    public decimal? DeliveryCostPhp { get; set; }
    public long PlNotStocked { get; set; }
    public bool IsStockingFinished { get; set; }
    public DateOnly DeliveryDate { get; set; }
    public int? PlAgeDays { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public string ClosedBy { get; set; }
    public string RowVersion { get; set; }
    public List<BatchListDto> Batches { get; set; } = new();
    public decimal TotalHarvestKg { get; set; }
    public decimal TotalFeedKg { get; set; }

    public bool CanCreateBatch { get; set; }

    public bool CanClose { get; set; }
}

public class FailureInput
{
    public Guid Id { get; set; }

    [Required, StringLength(BatchFailure.MaxReasonLength)]
    public string Reason { get; set; }

    public DateOnly FailureDate { get; set; }

    [Range(0, 1000000000)]
    public decimal? EstimatedLossPhp { get; set; }

    public string RowVersion { get; set; }
}

public class CloseCycleInput
{
    public Guid Id { get; set; }

    public CycleOutcome? Outcome { get; set; }

    public string RowVersion { get; set; }
}

public class CloseBatchInput
{
    public Guid Id { get; set; }

    public bool ConfirmReadOnly { get; set; }

    public string RowVersion { get; set; }
}

public class StatusChangeInput
{
    public Guid Id { get; set; }
    public string RowVersion { get; set; }
}

public class GetBatchPageInput : Capstone.Common.PagedInput
{
    public Guid? CycleId { get; set; }
    public Guid? PondId { get; set; }
    public BatchStatus? Status { get; set; }
    public bool OpenOnly { get; set; }
}

public class GetBatchStatusCountsInput
{
    [System.ComponentModel.DataAnnotations.StringLength(60)]
    public string Search { get; set; }

    public bool OpenOnly { get; set; }
}

public class StatusCountDto
{
    public int Status { get; set; }
    public int Count { get; set; }
}

public class BatchListDto
{
    public Guid Id { get; set; }
    public string BatchCode { get; set; }
    public string Letter { get; set; }
    public Guid CycleId { get; set; }
    public string CycleCode { get; set; }
    public Guid PondId { get; set; }
    public string PondCode { get; set; }
    public BatchStatus Status { get; set; }
    public DateOnly? StockingDate { get; set; }
    public long? PlCountStocked { get; set; }
    public int? Doc { get; set; }
    public decimal? CurrentAbwG { get; set; }
    public decimal? FcrToDate { get; set; }
    public decimal? EstimatedSurvivalPct { get; set; }
}

public class BatchDetailDto : BatchListDto
{
    public decimal PondAreaM2AtStocking { get; set; }
    public DateTime? StockedAtUtc { get; set; }
    public TransportMode TransportMode { get; set; }
    public DateTime? TimeProcessedUtc { get; set; }
    public int? AcclimationMinutes { get; set; }
    public decimal? InitialSurvivalEstimatePct { get; set; }
    public Guid FryDeliveryId { get; set; }
    public string HatcheryName { get; set; }
    public string HatcheryBatchNumber { get; set; }
    public int? PlAgeDays { get; set; }
    public string HatcheryCertificationStatusAtStocking { get; set; }
    public decimal? StockingDensityPerM2 { get; set; }
    public long? DeadAtStocking { get; set; }
    public string PlHealthCertificateNo { get; set; }
    public string SfscNo { get; set; }

    public DateOnly? EarliestSafeHarvestDate { get; set; }

    public bool CanReopen { get; set; }

    public string ReopenBlockedReason { get; set; }

    public List<ReopenEventDto> Reopenings { get; set; } = new();

    public FarmingSystem? FarmingSystemAtStocking { get; set; }
    public PondConstructionType? ConstructionTypeAtStocking { get; set; }
    public DensityFlag? DensityFlag { get; set; }
    public string FailureReason { get; set; }
    public DateOnly? FailureDate { get; set; }
    public decimal? EstimatedLossPhp { get; set; }
    public decimal? FinalAbwG { get; set; }
    public decimal? FinalFcr { get; set; }
    public decimal? SurvivalRatePct { get; set; }
    public decimal? TotalHarvestKg { get; set; }
    public decimal? TotalFeedKg { get; set; }
    public int? CultureDays { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public string ClosedBy { get; set; }
    public string RowVersion { get; set; }
    public IndicatorsDto Indicators { get; set; }
    public Dictionary<string, int> LogCounts { get; set; } = new();
    public bool HasFinalHarvest { get; set; }
    public bool HasRecords { get; set; }
    public List<string> AllowedLogTypes { get; set; } = new();
    public bool CanDeclareFailure { get; set; }
    public bool CanClose { get; set; }
    public bool CanAddHarvest { get; set; }
    public bool IsReadOnly { get; set; }
}

public class GetBatchesInput
{
    public string Search { get; set; }
    public Guid? CycleId { get; set; }
    public Guid? PondId { get; set; }
    public BatchStatus? Status { get; set; }
    public bool OpenOnly { get; set; }
}

public class ReadingInput
{
    [Required]
    public string ParameterCode { get; set; }

    public decimal Value { get; set; }
}

public class GetStockingOptionsInput
{
    public Guid? CycleId { get; set; }
}

public class StockingOptionsDto
{
    public List<StockingDeliveryDto> Deliveries { get; set; } = new();

    public List<StockingPondDto> Ponds { get; set; } = new();

    public List<StockingPondDto> BlockedPonds { get; set; } = new();

    public Guid? CycleId { get; set; }
    public string CycleCode { get; set; }
}

public class StockingDeliveryDto
{
    public Guid Id { get; set; }
    public string HatcheryName { get; set; }
    public string HatcheryShortCode { get; set; }
    public string HatcheryBatchNumber { get; set; }
    public DateOnly DeliveryDate { get; set; }
    public int? PlAgeDays { get; set; }
    public long PlReceived { get; set; }
    public long PlLeft { get; set; }
    public string PlHealthCertificateNo { get; set; }

    public int DaysSinceReceived { get; set; }
}

public class StockingPondDto
{
    public Guid Id { get; set; }
    public string PondCode { get; set; }
    public string Name { get; set; }
    public decimal AreaM2 { get; set; }
    public decimal AverageDepthM { get; set; }
    public Ponds.FarmingSystem FarmingSystem { get; set; }
    public Ponds.PondConstructionType ConstructionType { get; set; }

    public decimal? BandMinPlM2 { get; set; }
    public decimal? BandMaxPlM2 { get; set; }
    public string BandSource { get; set; }
    public DateTime? PreparedAtUtc { get; set; }
    public string BlockedReason { get; set; }
}

public class BatchStockingInput
{
    public Guid PondId { get; set; }

    [Range(1, 10000000, ErrorMessage = "The stocking count must be greater than zero.")]
    public long PlCount { get; set; }

    public DateTime StockedAt { get; set; }

    public TransportMode TransportMode { get; set; } = TransportMode.Truck;

    public DateTime? TimeProcessed { get; set; }

    [Range(0, 600)]
    public int? AcclimationMinutes { get; set; }

    [Range(0, 100)]
    public decimal? InitialSurvivalEstimatePct { get; set; }

    [Range(0, 10000000)]
    public long? DeadAtStocking { get; set; }

    public List<ReadingInput> Readings { get; set; } = new();
}

public class CreateBatchesInput
{
    public Guid? FryDeliveryId { get; set; }

    public Guid? CycleId { get; set; }

    [Required]
    public List<BatchStockingInput> Batches { get; set; } = new();

    public Guid? ClientRequestId { get; set; }
}

public class CreatedBatchDto
{
    public Guid Id { get; set; }
    public string BatchCode { get; set; }
    public string PondCode { get; set; }
    public decimal? StockingDensityPerM2 { get; set; }
    public DensityFlag? DensityFlag { get; set; }
}

public class CreateBatchesResultDto
{
    public Guid CycleId { get; set; }
    public string CycleCode { get; set; }
    public bool CycleCreated { get; set; }
    public bool WasDuplicate { get; set; }
    public List<CreatedBatchDto> Batches { get; set; } = new();
    public long PlLeft { get; set; }
    public List<WarningDto> Warnings { get; set; } = new();
}

public class CloseCheckDto
{
    public Guid BatchId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int BlankFeedingSlots { get; set; }
    public int NotFedSlots { get; set; }
    public int BlankWaterDays { get; set; }

    public List<BlankDayDto> BlankDays { get; set; } = new();
    public int BlankDaysTotal { get; set; }

    public decimal HarvestedKg { get; set; }
    public decimal SoldKg { get; set; }
    public decimal NotSoldKg { get; set; }
    public decimal UnaccountedKg { get; set; }

    public List<FlaggedLotDto> FlaggedLots { get; set; } = new();
}

public class FlaggedLotDto
{
    public string LotNumber { get; set; }
    public string Product { get; set; }
}

public class BlankDayDto
{
    public DateOnly Date { get; set; }
    public string Missing { get; set; }
}

public class BatchLogCalendarDayDto
{
    public DateOnly Date { get; set; }
    public DailyCellStatus Am { get; set; }
    public DailyCellStatus Pm { get; set; }
    public DailyCellStatus Water { get; set; }
    public bool HasMortality { get; set; }
    public bool HasGrowthSampling { get; set; }
    public bool HasChemical { get; set; }
}

public class BatchLogCalendarDto
{
    public Guid BatchId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public List<BatchLogCalendarDayDto> Days { get; set; } = new();
}
