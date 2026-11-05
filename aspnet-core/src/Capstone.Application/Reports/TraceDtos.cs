using System;
using System.Collections.Generic;

namespace Capstone.Reports.Dto;

public class BatchTraceDto
{
    public string FarmName { get; set; }
    public string BfarRegistrationNo { get; set; }
    public DateTime GeneratedAtUtc { get; set; }

    public Guid BatchId { get; set; }
    public string BatchCode { get; set; }
    public string CycleCode { get; set; }
    public string PondCode { get; set; }
    public int BatchStatus { get; set; }

    public TraceFrySourceDto FrySource { get; set; }
    public TraceStockingDto Stocking { get; set; }
    public List<TraceFeedLotDto> FeedLots { get; set; } = new();
    public int FeedingSessionsFed { get; set; }
    public int FeedingSessionsNotFed { get; set; }
    public List<TraceTreatmentDto> Treatments { get; set; } = new();
    public List<TraceHarvestDto> Harvests { get; set; } = new();
    public List<TraceSaleDto> Sales { get; set; } = new();
    public List<TraceDisposalDto> Disposals { get; set; } = new();

    public decimal UnaccountedKg { get; set; }
    public decimal HarvestedKg { get; set; }
    public decimal SoldKg { get; set; }
    public decimal DisposedKg { get; set; }
}

public class TraceFrySourceDto
{
    public Guid DeliveryId { get; set; }

    public string HatcheryName { get; set; }
    public string HatcheryShortCode { get; set; }
    public string BfarAccreditationNo { get; set; }
    public int AccreditationStatus { get; set; }
    public string HatcheryBatchNumber { get; set; }
    public DateOnly DeliveryDate { get; set; }
    public int? PlAgeDays { get; set; }
    public long PlReceived { get; set; }
    public string PlHealthCertificateNo { get; set; }
    public string TransportPermitNo { get; set; }
    public string SfscNo { get; set; }
}

public class TraceStockingDto
{
    public DateOnly? StockingDate { get; set; }
    public long? PlStocked { get; set; }
    public long? DeadAtStocking { get; set; }
    public decimal PondAreaM2 { get; set; }
    public int? FarmingSystem { get; set; }
    public int? ConstructionType { get; set; }
    public int? DensityFlag { get; set; }
    public decimal? DensityPerM2 { get; set; }
}

public class TraceFeedLotDto
{
    public string Product { get; set; }
    public string FeedType { get; set; }
    public string LotNumber { get; set; }
    public string Supplier { get; set; }
    public DateOnly? FirstFedOn { get; set; }
    public DateOnly? LastFedOn { get; set; }
    public decimal Kg { get; set; }

    public bool NeedsReconciliation { get; set; }
}

public class TraceTreatmentDto
{
    public DateOnly Date { get; set; }
    public DateOnly? EndDate { get; set; }
    public string Product { get; set; }
    public string ActiveIngredient { get; set; }
    public string Category { get; set; }
    public string LotNumber { get; set; }
    public string Supplier { get; set; }
    public string Quantity { get; set; }
    public string Reason { get; set; }
    public decimal? Dosage { get; set; }
    public string DosageUnit { get; set; }
    public string Method { get; set; }
    public int? WithdrawalDays { get; set; }
    public DateOnly? EarliestSafeHarvestDate { get; set; }
    public string PrescriptionRef { get; set; }
    public bool NeedsReconciliation { get; set; }
}

public class TraceHarvestDto
{
    public Guid HarvestId { get; set; }
    public DateTime HarvestedAtUtc { get; set; }
    public int HarvestType { get; set; }
    public decimal TotalKg { get; set; }
    public long? DeadAtHarvest { get; set; }
    public long? FinalCount { get; set; }
    public List<TraceGradeDto> Grades { get; set; } = new();
}

public class TraceGradeDto
{
    public Guid GradeLineId { get; set; }
    public string SizeLabel { get; set; }
    public decimal Kg { get; set; }
}

public class TraceSaleDto
{
    public Guid SaleId { get; set; }
    public string DispatchReceiptNo { get; set; }
    public DateOnly SaleDate { get; set; }
    public string BuyerName { get; set; }
    public string SizeLabel { get; set; }
    public decimal Kg { get; set; }
    public decimal? BuyerAcceptedKg { get; set; }
    public int PaymentStatus { get; set; }
    public string Transporter { get; set; }
    public string DriverName { get; set; }
    public string PlateNo { get; set; }
    public DateTime? LoadedAtUtc { get; set; }
    public string Destination { get; set; }
    public string LocalTransportPermitNo { get; set; }
    public string TemperatureNote { get; set; }
}

public class TraceDisposalDto
{
    public int Reason { get; set; }
    public decimal Kg { get; set; }
    public string Note { get; set; }
}
