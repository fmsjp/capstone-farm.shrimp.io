using Capstone.Domain;
using System;
using System.Collections.Generic;

namespace Capstone.Production;

public class ProductionLog : FarmEntity
{
    public const int MaxNotesLength = 2000;
    public const int MaxVoidReasonLength = 500;

    public Guid BatchId { get; set; }
    public PondBatch Batch { get; set; }
    public Guid PondId { get; set; }
    public LogType LogType { get; set; }
    public DateTime ObservedAtUtc { get; set; }
    public string Notes { get; set; }
    public Guid ClientRequestId { get; set; }

    public string VoidReason { get; set; }

    public PondPreparationDetail PondPreparation { get; set; }
    public GrowthSamplingDetail GrowthSampling { get; set; }
    public ChemicalApplicationDetail ChemicalApplication { get; set; }
    public MortalityDetail Mortality { get; set; }
    public PostProductionDetail PostProduction { get; set; }
    public List<WaterQualityMeasurement> Measurements { get; set; } = new();
}

public abstract class LogDetail : FarmChildEntity
{
}

public class PondPreparationDetail : LogDetail
{
    public PondPreparationActivity ActivityType { get; set; }
    public DateOnly? DrainingDate { get; set; }
    public int? DryingDays { get; set; }
    public DateOnly? FillingDate { get; set; }
    public decimal? SeawaterPct { get; set; }
    public decimal? FreshwaterPct { get; set; }
}

public class GrowthSamplingDetail : LogDetail
{
    public int SampleCount { get; set; }
    public decimal TotalSampleWeightG { get; set; }
    public decimal AbwG { get; set; }

    public decimal? EstimatedSurvivalPct { get; set; }
}

public class ChemicalApplicationDetail : LogDetail
{
    public const int MaxReasonLength = 500;
    public const int MaxShortLength = 50;
    public const int MaxMethodLength = 100;
    public const int MaxPrescriptionLength = 100;
    public const int MaxWithdrawalDays = 365;

    public string Reason { get; set; }

    public decimal? Dosage { get; set; }
    public string DosageUnit { get; set; }
    public string Method { get; set; }

    public DateOnly? EndDate { get; set; }

    public int? WithdrawalDays { get; set; }

    public string PrescriptionRef { get; set; }

    public static DateOnly? SafeHarvestDate(DateOnly logDate, DateOnly? endDate, int? withdrawalDays) =>
        withdrawalDays is > 0 ? (endDate ?? logDate).AddDays(withdrawalDays.Value) : null;
}

public class MortalityDetail : LogDetail
{
    public const int MaxTextLength = 500;

    public const int MaxNameLength = 100;

    public int Count { get; set; }
    public MortalityCause Cause { get; set; } = MortalityCause.Others;

    public string DiseaseName { get; set; }

    public string OtherCause { get; set; }

    public string Symptoms { get; set; }
    public string ResponseAction { get; set; }

    public bool IsHighMortality { get; set; }
}

public class PostProductionDetail : LogDetail
{
    public PostProductionActivity ActivityType { get; set; }
}

public class WaterQualityMeasurement : FarmChildEntity
{
    public const int MaxCodeLength = 20;

    public Guid LogId { get; set; }
    public string ParameterCode { get; set; }
    public decimal Value { get; set; }
    public string UnitCode { get; set; }
    public bool IsOutOfRange { get; set; }

    public ReadingSide? Side { get; set; }
}
