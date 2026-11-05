using Capstone.Common;
using Capstone.Harvests;
using Capstone.Inventory;
using Capstone.Production;
using Capstone.Production.Dto;
using System;
using System.Collections.Generic;

namespace Capstone.Reports.Dto;

public class ReportHeaderDto
{
    public string FarmName { get; set; }
    public string FarmCode { get; set; }
    public string FarmAddress { get; set; }
    public string TimeZone { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
    public string GeneratedBy { get; set; }
    public string Scope { get; set; }
}

public class FeedByItemDto
{
    public string ItemName { get; set; }
    public string BaiRegistrationNo { get; set; }
    public decimal QuantityKg { get; set; }
    public decimal Cost { get; set; }
}

public class WeeklyWaterQualityDto
{
    public DateOnly WeekStart { get; set; }
    public string ParameterCode { get; set; }
    public string ParameterName { get; set; }
    public string UnitCode { get; set; }
    public int Readings { get; set; }
    public decimal Average { get; set; }
    public decimal Min { get; set; }
    public decimal Max { get; set; }
    public int OutOfRangeCount { get; set; }
}

public class GrowthRowDto
{
    public DateOnly Date { get; set; }
    public int? Doc { get; set; }
    public int SampleCount { get; set; }
    public decimal TotalSampleWeightG { get; set; }
    public decimal AbwG { get; set; }
    public decimal? GrowthRateGPerDay { get; set; }
}

public class HarvestRowDto
{
    public Guid HarvestId { get; set; }
    public Guid BatchId { get; set; }
    public string BatchCode { get; set; }
    public string PondCode { get; set; }
    public string CycleCode { get; set; }
    public DateOnly Date { get; set; }
    public HarvestType HarvestType { get; set; }
    public decimal TotalKg { get; set; }
    public int? Boxes { get; set; }
    public decimal? AbwG { get; set; }
    public decimal Pieces { get; set; }
    public string QualityChecks { get; set; }
    public List<GradeLineDto> Grades { get; set; } = new();
}

public class BatchFinanceDto
{
    public decimal Revenue { get; set; }
    public decimal InventoryUsageCost { get; set; }

    public decimal PlCost { get; set; }

    public decimal ProductionCost => InventoryUsageCost + PlCost;
    public decimal Margin => Revenue - ProductionCost;
    public string MarginLabel => "Margin over feed, chemicals and PL";
}

public class BatchPerformanceDto
{
    public Guid BatchId { get; set; }
    public string BatchCode { get; set; }
    public string PondCode { get; set; }
    public decimal PondAreaM2 { get; set; }
    public BatchStatus Status { get; set; }
    public DateOnly? StockingDate { get; set; }
    public long? PlCountStocked { get; set; }
    public int? PlAgeDays { get; set; }
    public string HatcheryLotNumber { get; set; }
    public string HatcheryCertificationStatusAtStocking { get; set; }
    public string FailureReason { get; set; }
    public bool IsFinal { get; set; }
    public IndicatorsDto Indicators { get; set; }
    public List<FeedByItemDto> Feed { get; set; } = new();
    public List<WeeklyWaterQualityDto> WaterQuality { get; set; } = new();
    public List<GrowthRowDto> Growth { get; set; } = new();
    public List<HarvestRowDto> Harvests { get; set; } = new();
    public BatchFinanceDto Finance { get; set; } = new();
}

public class CyclePerformanceDto
{
    public ReportHeaderDto Header { get; set; }
    public Guid CycleId { get; set; }
    public string CycleCode { get; set; }
    public CycleStatus Status { get; set; }
    public bool IsFinal { get; set; }
    public DateOnly PlannedStartDate { get; set; }
    public DateOnly PlannedEndDate { get; set; }
    public decimal? TargetProductionKg { get; set; }
    public string Objectives { get; set; }
    public string HatcheryName { get; set; }
    public string HatcheryShortCode { get; set; }
    public string HatcheryLotNumber { get; set; }
    public string FailureReason { get; set; }
    public List<BatchPerformanceDto> Batches { get; set; } = new();

    public long TotalPlStocked { get; set; }
    public decimal TotalFeedKg { get; set; }
    public decimal TotalHarvestKg { get; set; }
    public decimal TotalHarvestedPieces { get; set; }
    public decimal? CycleFcr { get; set; }
    public decimal? CycleSurvivalPct { get; set; }
    public decimal? TargetAchievementPct { get; set; }
    public BatchFinanceDto Finance { get; set; } = new();
}

public class HarvestSummaryInput : DateRangeInput
{
    public Guid? CycleId { get; set; }
    public Guid? BatchId { get; set; }
}

public class SizeDistributionDto
{
    public string SizeLabel { get; set; }
    public int? MinCountPerKg { get; set; }
    public decimal Kg { get; set; }
    public decimal Pct { get; set; }
}

public class BuyerAllocationDto
{
    public string BuyerName { get; set; }
    public decimal Kg { get; set; }
    public decimal Amount { get; set; }
    public decimal Pct { get; set; }
}

public class BatchHarvestTotalsDto
{
    public Guid BatchId { get; set; }
    public string BatchCode { get; set; }
    public string PondCode { get; set; }
    public string CycleCode { get; set; }
    public decimal TotalKg { get; set; }
    public decimal Pieces { get; set; }
    public decimal? WeightedAbwG { get; set; }
    public decimal? SurvivalRatePct { get; set; }
}

public class HarvestSummaryDto
{
    public ReportHeaderDto Header { get; set; }
    public List<HarvestRowDto> Harvests { get; set; } = new();
    public List<BatchHarvestTotalsDto> Batches { get; set; } = new();
    public List<SizeDistributionDto> SizeDistribution { get; set; } = new();
    public List<BuyerAllocationDto> BuyerAllocation { get; set; } = new();
    public decimal TotalKg { get; set; }
    public decimal TotalPieces { get; set; }
    public decimal? WeightedAbwG { get; set; }
    public decimal SoldKg { get; set; }
    public decimal SalesAmount { get; set; }
}

public class FailureReportInput
{
    public Guid? BatchId { get; set; }
    public Guid? CycleId { get; set; }
}

public class FailureBatchDto
{
    public string BatchCode { get; set; }
    public string PondCode { get; set; }
    public decimal PondAreaM2 { get; set; }
    public BatchStatus Status { get; set; }
    public DateOnly? StockingDate { get; set; }
    public long? PlCountStocked { get; set; }
    public string HatcheryLotNumber { get; set; }
    public string HatcheryCertificationStatusAtStocking { get; set; }
    public string FailureReason { get; set; }
    public DateOnly? FailureDate { get; set; }
    public decimal? EstimatedLossPhp { get; set; }
    public int? DocAtFailure { get; set; }
    public IndicatorsDto Indicators { get; set; }
    public List<string> MortalityEvents { get; set; } = new();
    public List<string> Treatments { get; set; } = new();
    public List<string> RecentWaterQuality { get; set; } = new();
    public decimal InventoryUsageCost { get; set; }
}

public class FailureReportDto
{
    public ReportHeaderDto Header { get; set; }
    public string CycleCode { get; set; }
    public string HatcheryName { get; set; }
    public string HatcheryShortCode { get; set; }
    public string HatcheryAccreditation { get; set; }
    public string CycleFailureReason { get; set; }
    public DateOnly? CycleFailureDate { get; set; }
    public decimal? CycleEstimatedLossPhp { get; set; }
    public List<FailureBatchDto> Batches { get; set; } = new();
    public string Notice { get; set; } = "Prepared from FPMS records to support an insurance or compensation claim. Not an official BFAR or insurer form.";
}

public class WaterQualityExceptionInput : DateRangeInput
{
    public Guid? BatchId { get; set; }
}

public class WaterQualityExceptionDto
{
    public DateTime ObservedAtUtc { get; set; }
    public string BatchCode { get; set; }
    public string PondCode { get; set; }
    public string ParameterName { get; set; }
    public decimal Value { get; set; }
    public string UnitCode { get; set; }
    public string OptimalRange { get; set; }
    public string RecordedBy { get; set; }
}

public class WaterQualityExceptionReportDto
{
    public ReportHeaderDto Header { get; set; }

    public int ExceptionCount { get; set; }

    public int ReadingsChecked { get; set; }

    public decimal? ExceptionRatePct { get; set; }
    public int PondsAffected { get; set; }
    public int BatchesAffected { get; set; }
    public string MostFrequentParameter { get; set; }
    public List<WaterQualityExceptionDto> Items { get; set; } = new();
}

public class InventoryMovementInput : DateRangeInput
{
    public Guid? ItemId { get; set; }
}

public class InventoryItemSummaryDto
{
    public string ItemName { get; set; }
    public InventoryCategory Category { get; set; }
    public string Unit { get; set; }
    public decimal Opening { get; set; }
    public decimal Receipts { get; set; }
    public decimal Usage { get; set; }
    public decimal Adjustments { get; set; }
    public decimal Closing { get; set; }
    public decimal UsageCost { get; set; }
}

public class InventoryMovementRowDto
{
    public DateTime MovementDateUtc { get; set; }
    public string ItemName { get; set; }
    public string LotNumber { get; set; }
    public string Unit { get; set; }
    public StockMovementType Type { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitCost { get; set; }
    public string BatchCode { get; set; }
    public string Reason { get; set; }
}

public class InventoryMovementReportDto
{
    public ReportHeaderDto Header { get; set; }

    public decimal FeedUsedKg { get; set; }

    public decimal TotalUsageCost { get; set; }
    public List<InventoryItemSummaryDto> Items { get; set; } = new();
    public List<InventoryMovementRowDto> Movements { get; set; } = new();
}

public class SalesByBuyerInput : DateRangeInput
{
    public Guid? BuyerId { get; set; }
}

public class SalesByBuyerRowDto
{
    public Guid BuyerId { get; set; }
    public string BuyerName { get; set; }
    public int SaleCount { get; set; }
    public decimal Kg { get; set; }
    public decimal Amount { get; set; }
    public decimal Paid { get; set; }
    public decimal Balance { get; set; }

    public decimal? AveragePricePerKg { get; set; }

    public int OverdueCount { get; set; }
}

public class SalesByBuyerSaleDto
{
    public Guid SaleId { get; set; }
    public string DispatchReceiptNo { get; set; }
    public DateOnly SaleDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public string BuyerName { get; set; }
    public decimal Kg { get; set; }
    public decimal Amount { get; set; }
    public decimal Paid { get; set; }
    public decimal Balance { get; set; }
    public Capstone.Sales.PaymentStatus PaymentStatus { get; set; }
    public bool IsOverdue { get; set; }
}

public class SalesByBuyerDto
{
    public ReportHeaderDto Header { get; set; }
    public int SaleCount { get; set; }
    public decimal TotalKg { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalBalance { get; set; }
    public decimal? AveragePricePerKg { get; set; }
    public int OverdueCount { get; set; }
    public decimal OverdueBalance { get; set; }
    public List<SalesByBuyerRowDto> Buyers { get; set; } = new();
    public List<SalesByBuyerSaleDto> Sales { get; set; } = new();
}
