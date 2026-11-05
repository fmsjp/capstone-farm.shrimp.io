using Capstone.Common;
using Capstone.Production;
using Capstone.Production.Dto;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Capstone.Logbooks.Dto;

public class UsageLineInput
{
    public Guid LotId { get; set; }

    [Range(0, 100000000)]
    public decimal Quantity { get; set; }
}

public class PondPreparationInput
{
    public PondPreparationActivity ActivityType { get; set; }
    public DateOnly? DrainingDate { get; set; }
    public int? DryingDays { get; set; }
    public DateOnly? FillingDate { get; set; }
    public decimal? SeawaterPct { get; set; }
    public decimal? FreshwaterPct { get; set; }
    public List<UsageLineInput> Usages { get; set; } = new();
}

public class GrowthInput
{
    [Range(1, 100000, ErrorMessage = "Sample size must be greater than zero.")]
    public int SampleCount { get; set; }

    [Range(0.01, 10000000, ErrorMessage = "Total sample weight must be greater than zero.")]
    public decimal TotalSampleWeightG { get; set; }

    [Range(0, 100, ErrorMessage = "Estimated survival must be between 0 and 100%.")]
    public decimal? EstimatedSurvivalPct { get; set; }
}

public class ChemicalApplicationInput
{
    [StringLength(ChemicalApplicationDetail.MaxReasonLength)]
    public string Reason { get; set; }

    [Range(0, 100000000)]
    public decimal? Dosage { get; set; }

    [StringLength(ChemicalApplicationDetail.MaxShortLength)]
    public string DosageUnit { get; set; }

    [StringLength(ChemicalApplicationDetail.MaxMethodLength)]
    public string Method { get; set; }

    public DateOnly? EndDate { get; set; }

    [Range(0, ChemicalApplicationDetail.MaxWithdrawalDays)]
    public int? WithdrawalDays { get; set; }

    [StringLength(ChemicalApplicationDetail.MaxPrescriptionLength)]
    public string PrescriptionRef { get; set; }

    public List<UsageLineInput> Usages { get; set; } = new();
}

public class MortalityInput
{
    [Range(0, int.MaxValue)]
    public int Count { get; set; }

    public MortalityCause Cause { get; set; } = MortalityCause.Others;

    [StringLength(MortalityDetail.MaxNameLength)]
    public string DiseaseName { get; set; }

    [StringLength(MortalityDetail.MaxTextLength)]
    public string OtherCause { get; set; }

    [StringLength(MortalityDetail.MaxTextLength)]
    public string Symptoms { get; set; }

    [StringLength(MortalityDetail.MaxTextLength)]
    public string ResponseAction { get; set; }
}

public class PostProductionInput
{
    public PostProductionActivity ActivityType { get; set; }
}

public class SaveLogInput
{
    public Guid? Id { get; set; }

    public Guid BatchId { get; set; }
    public LogType LogType { get; set; }

    public DateTime ObservedAt { get; set; }

    [StringLength(ProductionLog.MaxNotesLength)]
    public string Notes { get; set; }

    public Guid ClientRequestId { get; set; }

    public List<ReadingInput> Readings { get; set; }
    public GrowthInput Growth { get; set; }
    public ChemicalApplicationInput ChemicalApplication { get; set; }
    public MortalityInput Mortality { get; set; }
}

public class VoidInput
{
    public Guid Id { get; set; }

    [Required, StringLength(ProductionLog.MaxVoidReasonLength)]
    public string Reason { get; set; }
}

public class MeasurementDto
{
    public string ParameterCode { get; set; }
    public string ParameterName { get; set; }
    public decimal Value { get; set; }
    public string UnitCode { get; set; }
    public bool IsOutOfRange { get; set; }
    public ReadingSide? Side { get; set; }
    public string OptimalRange { get; set; }
    public string Recommendation { get; set; }
}

public class UsageDto
{
    public Guid LotId { get; set; }
    public string Label { get; set; }
    public decimal Quantity { get; set; }
    public string Unit { get; set; }
}

public class LogDto
{
    public Guid Id { get; set; }
    public Guid BatchId { get; set; }
    public string BatchCode { get; set; }
    public string PondCode { get; set; }
    public LogType LogType { get; set; }
    public DateTime ObservedAtUtc { get; set; }
    public string Notes { get; set; }
    public Guid ClientRequestId { get; set; }
    public long? RecorderUserId { get; set; }
    public string RecordedBy { get; set; }
    public DateTime CreationTime { get; set; }
    public DateTime? LastModificationTime { get; set; }
    public bool CanEdit { get; set; }
    public bool HasOutOfRange { get; set; }

    public bool CanVoid { get; set; }

    public bool IsVoided { get; set; }

    public string VoidReason { get; set; }
    public string VoidedBy { get; set; }
    public DateTime? VoidedAtUtc { get; set; }

    public bool IsBatchClosed { get; set; }

    public PondPreparationInput PondPreparation { get; set; }
    public List<MeasurementDto> Measurements { get; set; } = new();
    public GrowthInput Growth { get; set; }
    public decimal? AbwG { get; set; }
    public decimal? EstimatedSurvivalPct { get; set; }
    public bool IsHighMortality { get; set; }
    public decimal? GrowthRateGPerDay { get; set; }
    public ChemicalApplicationInput ChemicalApplication { get; set; }

    public DateOnly? SafeHarvestDate { get; set; }

    public MortalityInput Mortality { get; set; }
    public PostProductionInput PostProduction { get; set; }
    public List<UsageDto> Usages { get; set; } = new();
}

public class SaveLogResultDto
{
    public LogDto Log { get; set; }

    public bool WasDuplicate { get; set; }

    public List<WarningDto> Warnings { get; set; } = new();
    public IndicatorsDto Indicators { get; set; }
}

public class GetLogsInput : Capstone.Common.PagedInput
{
    public Guid? BatchId { get; set; }
    public LogType? LogType { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public bool OutOfRangeOnly { get; set; }

    public bool IncludeVoided { get; set; }
}

public class ParameterDto
{
    public string Code { get; set; }
    public string Name { get; set; }
    public string UnitCode { get; set; }
    public decimal PhysicalMin { get; set; }
    public decimal PhysicalMax { get; set; }
    public decimal? OptimalMin { get; set; }
    public bool OptimalMinExclusive { get; set; }
    public decimal? OptimalMax { get; set; }
    public bool OptimalMaxExclusive { get; set; }
    public string OptimalRange { get; set; }
    public string GuidanceFrequency { get; set; }
    public string LowRecommendation { get; set; }
    public string HighRecommendation { get; set; }
    public bool RequiredAtStocking { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}
