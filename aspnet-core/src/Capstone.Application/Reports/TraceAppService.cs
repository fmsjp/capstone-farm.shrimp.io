using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.UI;
using Capstone.Authorization;
using Capstone.Compliance;
using Capstone.Domain;
using Capstone.Harvests;
using Capstone.Inventory;
using Capstone.Production;
using Capstone.Reports.Dto;
using Capstone.Sales;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Capstone.Reports;

[AbpAuthorize(PermissionNames.Reports)]
public class TraceAppService : CapstoneAppServiceBase
{
    private readonly IRepository<PondBatch, Guid> _batches;
    private readonly IRepository<FeedingSession, Guid> _sessions;
    private readonly IRepository<StockMovement, Guid> _movements;
    private readonly IRepository<ProductionLog, Guid> _logs;
    private readonly IRepository<Harvest, Guid> _harvests;
    private readonly IRepository<SaleLine, Guid> _saleLines;
    private readonly IRepository<Sale, Guid> _sales;
    private readonly IRepository<ComplianceDocument, Guid> _documents;

    public TraceAppService(
        IRepository<PondBatch, Guid> batches,
        IRepository<FeedingSession, Guid> sessions,
        IRepository<StockMovement, Guid> movements,
        IRepository<ProductionLog, Guid> logs,
        IRepository<Harvest, Guid> harvests,
        IRepository<SaleLine, Guid> saleLines,
        IRepository<Sale, Guid> sales,
        IRepository<ComplianceDocument, Guid> documents)
    {
        _batches = batches;
        _sessions = sessions;
        _movements = movements;
        _logs = logs;
        _harvests = harvests;
        _saleLines = saleLines;
        _sales = sales;
        _documents = documents;
    }

    public virtual async Task<BatchTraceDto> GetBatchTrace(Guid batchId)
    {
        var batch = await _batches.GetAll().AsNoTracking()
            .Include(b => b.Pond)
            .Include(b => b.Cycle).ThenInclude(c => c.FryDelivery).ThenInclude(d => d.Hatchery)
            .FirstOrDefaultAsync(b => b.Id == batchId) ?? throw new UserFriendlyException("Batch not found.");
        var zone = await Farm.GetTimeZoneAsync();
        var today = FarmClock.Today(zone);
        var profile = await Farm.GetProfileAsync();

        var docs = await _documents.GetAll().AsNoTracking().Include(d => d.Preset)
            .Where(d => !d.IsArchived && d.Preset != null && d.Preset.Name == SaleAppService.BfarPreset).ToListAsync();
        var bfar = docs.Where(d => d.IsCurrent(today)).OrderByDescending(d => d.IssueDate).Select(d => d.DocumentNumber)
            .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));

        var dto = new BatchTraceDto
        {
            FarmName = profile.FarmName, BfarRegistrationNo = bfar, GeneratedAtUtc = DateTime.UtcNow,
            BatchId = batch.Id, BatchCode = batch.BatchCode, CycleCode = batch.Cycle?.CycleCode, PondCode = batch.Pond?.PondCode, BatchStatus = (int)batch.Status,
            Stocking = new TraceStockingDto
            {
                StockingDate = batch.StockingDate, PlStocked = batch.PlCountStocked, DeadAtStocking = batch.DeadAtStocking, PondAreaM2 = batch.PondAreaM2AtStocking,
                FarmingSystem = (int?)batch.FarmingSystemAtStocking, ConstructionType = (int?)batch.ConstructionTypeAtStocking, DensityFlag = (int?)batch.DensityFlag,
                DensityPerM2 = batch.StockingDensityPerM2,
            },
        };

        var delivery = batch.Cycle?.FryDelivery;
        if (delivery != null)
        {
            dto.FrySource = new TraceFrySourceDto
            {
                DeliveryId = delivery.Id, HatcheryName = delivery.Hatchery?.Name, HatcheryShortCode = delivery.Hatchery?.ShortCode,
                BfarAccreditationNo = delivery.Hatchery?.BfarAccreditationNo, AccreditationStatus = (int)(delivery.Hatchery?.AccreditationStatus ?? Resources.AccreditationStatus.Unknown),
                HatcheryBatchNumber = delivery.HatcheryBatchNumber, DeliveryDate = delivery.DeliveryDate, PlAgeDays = delivery.PlAgeDays, PlReceived = delivery.PlReceived,
                PlHealthCertificateNo = delivery.PlHealthCertificateNo, TransportPermitNo = delivery.TransportPermitNo, SfscNo = delivery.SfscNo,
            };
        }

        await AddFeedAsync(dto, batchId);
        await AddTreatmentsAsync(dto, batchId, zone);
        await AddHarvestsAndSalesAsync(dto, batchId);
        return dto;
    }

    private async Task AddFeedAsync(BatchTraceDto dto, Guid batchId)
    {
        var sessions = await _sessions.GetAll().AsNoTracking().Where(s => s.BatchId == batchId)
            .Select(s => new { s.Id, s.FeedDate, s.Status }).ToListAsync();
        dto.FeedingSessionsFed = sessions.Count(s => s.Status == FeedingStatus.Fed);
        dto.FeedingSessionsNotFed = sessions.Count(s => s.Status == FeedingStatus.NotFed);
        var dateOf = sessions.ToDictionary(s => s.Id, s => s.FeedDate);
        var ids = dateOf.Keys.ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var moves = await _movements.GetAll().AsNoTracking().Include(m => m.Lot).ThenInclude(l => l.Item)
            .Where(m => m.FeedingSessionId != null && ids.Contains(m.FeedingSessionId.Value)).ToListAsync();
        dto.FeedLots = moves.GroupBy(m => m.LotId).Select(g =>
        {
            var first = g.First();
            var dates = g.Where(m => dateOf.ContainsKey(m.FeedingSessionId.Value)).Select(m => dateOf[m.FeedingSessionId.Value]).ToList();
            return new TraceFeedLotDto
            {
                Product = first.Lot.Item.DisplayName, FeedType = first.Lot.Item.FeedType, LotNumber = first.Lot.LotNumber, Supplier = first.Lot.Supplier,
                FirstFedOn = dates.Count > 0 ? dates.Min() : null, LastFedOn = dates.Count > 0 ? dates.Max() : null,
                Kg = g.Sum(m => Units.Convert(-m.Quantity, first.Lot.Item.Unit, "kg") ?? -m.Quantity),
                NeedsReconciliation = first.Lot.NeedsReconciliation,
            };
        }).Where(l => l.Kg != 0).OrderBy(l => l.FirstFedOn).ThenBy(l => l.Product).ToList();
    }

    private async Task AddTreatmentsAsync(BatchTraceDto dto, Guid batchId, TimeZoneInfo zone)
    {
        var logs = await _logs.GetAll().AsNoTracking().Include(l => l.ChemicalApplication)
            .Where(l => l.BatchId == batchId && l.LogType == LogType.ChemicalApplication).OrderBy(l => l.ObservedAtUtc).ToListAsync();
        var logIds = logs.Select(l => l.Id).ToList();
        if (logIds.Count == 0)
        {
            return;
        }

        var moves = await _movements.GetAll().AsNoTracking().Include(m => m.Lot).ThenInclude(l => l.Item)
            .Where(m => m.SourceLogId != null && logIds.Contains(m.SourceLogId.Value)).ToListAsync();
        foreach (var log in logs)
        {
            var detail = log.ChemicalApplication;
            var date = FarmClock.LocalDate(log.ObservedAtUtc, zone);
            var perLot = moves.Where(m => m.SourceLogId == log.Id).GroupBy(m => m.LotId).Where(g => g.Sum(m => m.Quantity) != 0).ToList();
            if (perLot.Count == 0)
            {
                dto.Treatments.Add(NewTreatment(date, detail, null, null, null, null, null, null, false));
                continue;
            }

            foreach (var g in perLot)
            {
                var lot = g.First().Lot;
                dto.Treatments.Add(NewTreatment(date, detail, lot.Item.DisplayName, lot.Item.ActiveIngredient,
                    lot.Item.Category == InventoryCategory.Chemical ? "Chemical" : "Supplement", lot.LotNumber, lot.Supplier, $"{-g.Sum(m => m.Quantity):0.###} {lot.Item.Unit}", lot.NeedsReconciliation));
            }
        }
    }

    private static TraceTreatmentDto NewTreatment(DateOnly date, ChemicalApplicationDetail d, string product, string ingredient, string category, string lotNumber, string supplier, string quantity, bool needsReconciliation) => new()
    {
        Date = date, EndDate = d?.EndDate, Product = product, ActiveIngredient = ingredient, Category = category, LotNumber = lotNumber, Supplier = supplier, Quantity = quantity,
        NeedsReconciliation = needsReconciliation, Reason = d?.Reason, Dosage = d?.Dosage, DosageUnit = d?.DosageUnit, Method = d?.Method, WithdrawalDays = d?.WithdrawalDays, PrescriptionRef = d?.PrescriptionRef,
        EarliestSafeHarvestDate = d == null ? null : ChemicalApplicationDetail.SafeHarvestDate(date, d.EndDate, d.WithdrawalDays),
    };

    private async Task AddHarvestsAndSalesAsync(BatchTraceDto dto, Guid batchId)
    {
        var harvests = await _harvests.GetAll().AsNoTracking().Include(h => h.GradeLines).ThenInclude(g => g.SizeGrade).Include(h => h.Disposals)
            .Where(h => h.BatchId == batchId).OrderBy(h => h.HarvestedAtUtc).ToListAsync();
        dto.Harvests = harvests.Select(h => new TraceHarvestDto
        {
            HarvestId = h.Id, HarvestedAtUtc = h.HarvestedAtUtc, HarvestType = (int)h.HarvestType, TotalKg = h.TotalKg, DeadAtHarvest = h.DeadAtHarvest, FinalCount = h.FinalCount,
            Grades = h.GradeLines.Select(g => new TraceGradeDto { GradeLineId = g.Id, SizeLabel = g.SizeGrade?.Label, Kg = g.Kg }).OrderBy(g => g.SizeLabel).ToList(),
        }).ToList();
        dto.HarvestedKg = harvests.Sum(h => h.TotalKg);
        dto.Disposals = harvests.SelectMany(h => h.Disposals).Select(d => new TraceDisposalDto { Reason = (int)d.Reason, Kg = d.Kg, Note = d.Note }).ToList();
        dto.DisposedKg = dto.Disposals.Sum(d => d.Kg);

        var gradeIds = harvests.SelectMany(h => h.GradeLines).Select(g => g.Id).ToList();
        if (gradeIds.Count > 0)
        {
            var lines = await _saleLines.GetAll().AsNoTracking().Include(l => l.HarvestGradeLine).ThenInclude(g => g.SizeGrade)
                .Where(l => gradeIds.Contains(l.HarvestGradeLineId)).ToListAsync();
            var saleIds = lines.Select(l => l.SaleId).Distinct().ToList();
            var sales = await _sales.GetAll().AsNoTracking().Include(s => s.Buyer).Include(s => s.Lines).Include(s => s.Payments)
                .Where(s => saleIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id);
            dto.Sales = lines.Where(l => sales.ContainsKey(l.SaleId)).Select(l =>
            {
                var s = sales[l.SaleId];
                return new TraceSaleDto
                {
                    SaleId = s.Id, DispatchReceiptNo = s.DispatchReceiptNo, SaleDate = s.SaleDate, BuyerName = s.Buyer?.Name, SizeLabel = l.HarvestGradeLine?.SizeGrade?.Label,
                    Kg = l.Kg, BuyerAcceptedKg = l.BuyerAcceptedKg, PaymentStatus = (int)s.Status, Transporter = s.Transporter, DriverName = s.DriverName, PlateNo = s.PlateNo,
                    LoadedAtUtc = s.LoadedAtUtc, Destination = s.Destination, LocalTransportPermitNo = s.LocalTransportPermitNo, TemperatureNote = s.TemperatureNote,
                };
            }).OrderBy(x => x.SaleDate).ThenBy(x => x.DispatchReceiptNo).ThenBy(x => x.SizeLabel).ToList();
            dto.SoldKg = dto.Sales.Sum(x => x.Kg);
        }

        dto.UnaccountedKg = Math.Max(0, dto.HarvestedKg - dto.SoldKg - dto.DisposedKg);
    }
}
