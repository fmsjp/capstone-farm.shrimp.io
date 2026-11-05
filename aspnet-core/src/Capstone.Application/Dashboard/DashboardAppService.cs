using Abp.Authorization;
using Abp.Domain.Repositories;
using Capstone.Alerts;
using Capstone.Authorization;
using Capstone.Compliance;
using Capstone.Domain;
using Capstone.Inventory;
using Capstone.Ponds;
using Capstone.Production;
using Capstone.ReferenceData;
using Capstone.Resources;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Capstone.Dashboard;

public class PondTileDto
{
    public Guid PondId { get; set; }
    public string PondCode { get; set; }
    public string PondName { get; set; }
    public decimal AreaM2 { get; set; }
    public PondDisplayStatus Status { get; set; }
    public Guid? BatchId { get; set; }
    public string BatchCode { get; set; }
    public BatchStatus? BatchStatus { get; set; }
    public int? Doc { get; set; }
    public decimal? AbwG { get; set; }
    public decimal? FcrToDate { get; set; }
    public decimal? EstimatedSurvivalPct { get; set; }
    public bool HasWaterQualityAlert { get; set; }

    public decimal? EstimatedBiomassKg { get; set; }

    public decimal? CumulativeFeedKg { get; set; }

    public DateTime? LatestWaterObservedAtUtc { get; set; }

    public List<PondWaterReadingDto> LatestWater { get; set; } = new();
}

public class PondWaterReadingDto
{
    public string Code { get; set; }
    public string Name { get; set; }
    public decimal Value { get; set; }
    public string Unit { get; set; }
    public bool IsOutOfRange { get; set; }
}

public class CycleTileDto
{
    public Guid Id { get; set; }
    public string CycleCode { get; set; }
    public CycleStatus Status { get; set; }
    public DateOnly? StartDate { get; set; }
    public int BatchCount { get; set; }
    public int StockedCount { get; set; }

}

public class WaterQualityAlertDto
{
    public Guid LogId { get; set; }

    public List<Guid> LogIds { get; set; } = new();
    public Guid BatchId { get; set; }
    public DateOnly FarmDate { get; set; }
    public DateTime ObservedAtUtc { get; set; }
    public string PondCode { get; set; }
    public string BatchCode { get; set; }

    public List<string> Readings { get; set; } = new();
}

public class StockAlertDto
{
    public Guid ItemId { get; set; }
    public Guid? LotId { get; set; }
    public string ItemName { get; set; }
    public string LotNumber { get; set; }
    public decimal Quantity { get; set; }
    public string Unit { get; set; }

    public string Kind { get; set; }
}

public class DocumentAlertDto
{
    public Guid DocumentId { get; set; }
    public string Title { get; set; }
    public string CategoryName { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public ExpiryStatus Status { get; set; }
}

public class RecentLogDto
{
    public Guid Id { get; set; }
    public Guid BatchId { get; set; }
    public LogType LogType { get; set; }
    public DateTime ObservedAtUtc { get; set; }
    public string BatchCode { get; set; }
    public string RecordedBy { get; set; }
}

public class SetupTaskDto
{
    public string Key { get; set; }
    public string Label { get; set; }
    public bool Done { get; set; }
    public string Route { get; set; }
}

public class KpiDto
{
    public int ActiveCycles { get; set; }
    public int StockedBatches { get; set; }
    public decimal? AverageAbwG { get; set; }
    public decimal? AverageFcrToDate { get; set; }
    public decimal? AverageEstimatedSurvivalPct { get; set; }
    public decimal EstimatedBiomassKg { get; set; }
    public int WaterQualityAlerts7Days { get; set; }

    public int HighMortalityDays7Days { get; set; }

    public int BlankDailyRecordsYesterday { get; set; }
}

public class HighMortalityDto
{
    public Guid LogId { get; set; }
    public Guid BatchId { get; set; }
    public string BatchCode { get; set; }
    public string PondCode { get; set; }
    public DateOnly FarmDate { get; set; }
    public int Count { get; set; }
}

public class DashboardDto
{
    public string FarmName { get; set; }
    public DateOnly Today { get; set; }
    public bool IsOwner { get; set; }
    public KpiDto Kpis { get; set; } = new();
    public List<PondTileDto> Ponds { get; set; } = new();
    public List<CycleTileDto> ActiveCycles { get; set; } = new();
    public List<WaterQualityAlertDto> WaterQualityAlerts { get; set; } = new();
    public List<HighMortalityDto> HighMortality { get; set; } = new();
    public List<StockAlertDto> StockAlerts { get; set; } = new();
    public List<DocumentAlertDto> DocumentAlerts { get; set; } = new();
    public List<string> MissingRequiredDocuments { get; set; } = new();
    public List<RecentLogDto> RecentLogs { get; set; } = new();
    public List<SetupTaskDto> SetupTasks { get; set; } = new();

    public List<Resources.OpenDeliveryDto> FinishStocking { get; set; } = new();
    public string MonitoringGuidance { get; set; }
}

[AbpAuthorize(PermissionNames.Dashboard)]
public class DashboardAppService : CapstoneAppServiceBase
{
    private readonly IRepository<Pond, Guid> _ponds;
    private readonly IRepository<PondBatch, Guid> _batches;
    private readonly IRepository<ProductionCycle, Guid> _cycles;
    private readonly IRepository<ProductionLog, Guid> _logs;
    private readonly IRepository<WaterQualityMeasurement, Guid> _measurements;
    private readonly IRepository<InventoryItem, Guid> _items;
    private readonly IRepository<InventoryLot, Guid> _lots;
    private readonly IRepository<StockMovement, Guid> _movements;
    private readonly IRepository<ComplianceDocument, Guid> _documents;
    private readonly IRepository<HatcherySource, Guid> _hatcheries;
    private readonly IRepository<Resources.Buyer, Guid> _buyers;
    private readonly IRepository<PondPreparationRound, Guid> _rounds;
    private readonly IRepository<MortalityDetail, Guid> _mortality;
    private readonly DailyLogStatusService _daily;
    private readonly BatchDataLoader _loader;
    private readonly WaterQualityEvaluator _waterQuality;
    private readonly ComplianceChecker _compliance;
    private readonly NotificationAppService _notifications;
    private readonly Resources.FryDeliveryAppService _deliveries;

    public DashboardAppService(
        IRepository<Pond, Guid> ponds,
        IRepository<PondBatch, Guid> batches,
        IRepository<ProductionCycle, Guid> cycles,
        IRepository<ProductionLog, Guid> logs,
        IRepository<WaterQualityMeasurement, Guid> measurements,
        IRepository<InventoryItem, Guid> items,
        IRepository<InventoryLot, Guid> lots,
        IRepository<StockMovement, Guid> movements,
        IRepository<ComplianceDocument, Guid> documents,
        IRepository<HatcherySource, Guid> hatcheries,
        IRepository<Resources.Buyer, Guid> buyers,
        IRepository<PondPreparationRound, Guid> rounds,
        IRepository<MortalityDetail, Guid> mortality,
        DailyLogStatusService daily,
        BatchDataLoader loader,
        WaterQualityEvaluator waterQuality,
        ComplianceChecker compliance,
        NotificationAppService notifications,
        Resources.FryDeliveryAppService deliveries)
    {
        _deliveries = deliveries;
        _ponds = ponds;
        _batches = batches;
        _cycles = cycles;
        _logs = logs;
        _measurements = measurements;
        _items = items;
        _lots = lots;
        _movements = movements;
        _documents = documents;
        _hatcheries = hatcheries;
        _buyers = buyers;
        _rounds = rounds;
        _mortality = mortality;
        _daily = daily;
        _loader = loader;
        _waterQuality = waterQuality;
        _compliance = compliance;
        _notifications = notifications;
    }

    public virtual async Task<DashboardDto> Get()
    {
        var zone = await Farm.GetTimeZoneAsync();
        var today = FarmClock.Today(zone);
        var profile = await Farm.GetProfileAsync();
        var isOwner = await IsGrantedAsync(PermissionNames.Dashboard_Full);
        var parameters = await _waterQuality.GetParametersAsync();
        var dto = new DashboardDto { FarmName = profile.FarmName, Today = today, IsOwner = isOwner };

        var ponds = await _ponds.GetAll().AsNoTracking().Include(p => p.Prefix).Where(p => p.ManualStatus != PondManualStatus.Inactive && !p.IsArchived).OrderBy(p => p.PondCode).ToListAsync();
        var open = await _batches.GetAll().AsNoTracking().Where(b => b.Status != BatchStatus.Closed).ToListAsync();
        var facts = await _loader.LoadAsync(open.Select(b => b.Id).ToList(), zone);

        var since = FarmClock.StartOfDayUtc(today.AddDays(-6), zone);
        var wq = await (from l in _logs.GetAll().AsNoTracking()
                        join m in _measurements.GetAll().AsNoTracking() on l.Id equals m.LogId
                        join b in _batches.GetAll().AsNoTracking() on l.BatchId equals b.Id
                        where m.IsOutOfRange && l.ObservedAtUtc >= since
                        orderby l.ObservedAtUtc descending
                        select new { l.Id, l.BatchId, l.PondId, l.ObservedAtUtc, b.BatchCode, m.ParameterCode, m.Value, m.UnitCode, m.Side }).Take(500).ToListAsync();
        var pondCodes = ponds.ToDictionary(p => p.Id, p => p.PondCode);
        var grouped = wq.GroupBy(w => new { w.PondId, Day = FarmClock.LocalDate(w.ObservedAtUtc, zone) })
            .Select(g =>
            {
                var latest = g.OrderByDescending(w => w.ObservedAtUtc).First();
                return new WaterQualityAlertDto
                {
                    LogId = latest.Id, LogIds = g.Select(w => w.Id).Distinct().ToList(), BatchId = latest.BatchId, FarmDate = g.Key.Day, ObservedAtUtc = latest.ObservedAtUtc,
                    PondCode = pondCodes.TryGetValue(g.Key.PondId, out var pc) ? pc : null, BatchCode = latest.BatchCode,
                    Readings = g.OrderBy(w => parameters.TryGetValue(w.ParameterCode, out var p) ? p.SortOrder : 99).ThenBy(w => w.ObservedAtUtc).Select(w =>
                    {
                        var p = parameters.TryGetValue(w.ParameterCode, out var def) ? def : null;
                        var side = w.Side == ReadingSide.Low ? "low" : w.Side == ReadingSide.High ? "high" : "out of range";
                        return $"{p?.Name ?? w.ParameterCode} {w.Value:0.###} {w.UnitCode}".TrimEnd() + $" ({side}; optimal {p?.DescribeOptimalRange()})";
                    }).Distinct().ToList(),
                };
            })
            .OrderByDescending(a => a.FarmDate).ThenBy(a => a.PondCode).ToList();
        dto.WaterQualityAlerts = grouped.Take(12).ToList();
        dto.Kpis.WaterQualityAlerts7Days = grouped.Count;

        var highMortality = await (from l in _logs.GetAll().AsNoTracking()
                                   join d in _mortality.GetAll().AsNoTracking() on l.Id equals d.Id
                                   join b in _batches.GetAll().AsNoTracking() on l.BatchId equals b.Id
                                   where d.IsHighMortality && l.ObservedAtUtc >= since
                                   orderby l.ObservedAtUtc descending
                                   select new { l.Id, l.BatchId, l.PondId, l.ObservedAtUtc, b.BatchCode, d.Count }).Take(200).ToListAsync();
        dto.HighMortality = highMortality.Select(h => new HighMortalityDto
        {
            LogId = h.Id, BatchId = h.BatchId, BatchCode = h.BatchCode, PondCode = pondCodes.TryGetValue(h.PondId, out var pc) ? pc : null,
            FarmDate = FarmClock.LocalDate(h.ObservedAtUtc, zone), Count = h.Count,
        }).Take(12).ToList();
        dto.Kpis.HighMortalityDays7Days = highMortality.Select(h => FarmClock.LocalDate(h.ObservedAtUtc, zone)).Distinct().Count();

        var stocked = open.Where(b => b.Status == BatchStatus.Stocked).ToList();
        if (stocked.Count > 0)
        {
            var yesterday = today.AddDays(-1);
            var grid = await _daily.LoadAsync(stocked, yesterday, yesterday, zone);
            dto.Kpis.BlankDailyRecordsYesterday = grid.Values.SelectMany(d => d)
                .Sum(d => (d.Am == DailyCellStatus.Blank ? 1 : 0) + (d.Pm == DailyCellStatus.Blank ? 1 : 0) + (d.Water == DailyCellStatus.Blank ? 1 : 0));
        }

        var openIds = open.Select(b => b.Id).ToList();
        var waterLogs = await _logs.GetAll().AsNoTracking().Where(l => openIds.Contains(l.BatchId) && l.LogType == LogType.WaterQuality)
            .Select(l => new { l.Id, l.BatchId, l.ObservedAtUtc }).ToListAsync();
        var latestWaterLog = waterLogs.GroupBy(l => l.BatchId).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.ObservedAtUtc).First());
        var latestWaterIds = latestWaterLog.Values.Select(x => x.Id).ToList();
        var latestMeasures = await _measurements.GetAll().AsNoTracking().Where(m => latestWaterIds.Contains(m.LogId))
            .Select(m => new { m.LogId, m.ParameterCode, m.Value, m.UnitCode, m.IsOutOfRange }).ToListAsync();

        var stockedIndicators = new List<BatchIndicators>();
        var openRounds = await _rounds.GetAll().AsNoTracking().Where(r => r.UsedAtUtc == null).Select(r => new { r.PondId, r.PreparedAtUtc }).ToListAsync();
        foreach (var pond in ponds)
        {
            var batch = open.FirstOrDefault(b => b.PondId == pond.Id);
            var round = openRounds.FirstOrDefault(r => r.PondId == pond.Id);
            var tile = new PondTileDto
            {
                PondId = pond.Id, PondCode = pond.PondCode, PondName = PondNaming.DisplayName(pond.Prefix?.Name, pond.Number), AreaM2 = pond.AreaM2,
                Status = PondAppService.DeriveStatus(pond.ManualStatus, batch?.Status, round != null, round?.PreparedAtUtc != null),
                BatchId = batch?.Id, BatchCode = batch?.BatchCode, BatchStatus = batch?.Status,
                HasWaterQualityAlert = batch != null && wq.Any(w => w.BatchId == batch.Id),
            };
            if (batch != null && batch.StockingDate.HasValue)
            {
                var ind = IndicatorCalculator.Compute(facts.TryGetValue(batch.Id, out var f) ? f : new BatchFacts(), today);
                tile.Doc = batch.Status == BatchStatus.Stocked ? ind.Doc : null;
                tile.AbwG = ind.CurrentAbwG;
                tile.FcrToDate = ind.FcrToDate;
                tile.EstimatedSurvivalPct = ind.EstimatedSurvivalPct;
                tile.EstimatedBiomassKg = ind.EstimatedBiomassKg;
                tile.CumulativeFeedKg = ind.CumulativeFeedKg;
                if (latestWaterLog.TryGetValue(batch.Id, out var wl))
                {
                    tile.LatestWaterObservedAtUtc = wl.ObservedAtUtc;
                    tile.LatestWater = latestMeasures.Where(m => m.LogId == wl.Id)
                        .OrderBy(m => parameters.TryGetValue(m.ParameterCode, out var pd) ? pd.SortOrder : 99)
                        .Select(m => new PondWaterReadingDto
                        {
                            Code = m.ParameterCode, Name = parameters.TryGetValue(m.ParameterCode, out var pn) ? pn.Name : m.ParameterCode,
                            Value = m.Value, Unit = m.UnitCode, IsOutOfRange = m.IsOutOfRange,
                        }).ToList();
                }

                if (batch.Status == BatchStatus.Stocked)
                {
                    stockedIndicators.Add(ind);
                }
            }

            dto.Ponds.Add(tile);
        }

        dto.Kpis.StockedBatches = stockedIndicators.Count;
        dto.Kpis.AverageAbwG = Avg(stockedIndicators.Select(i => i.CurrentAbwG));
        dto.Kpis.AverageFcrToDate = Avg(stockedIndicators.Select(i => i.FcrToDate));
        dto.Kpis.AverageEstimatedSurvivalPct = Avg(stockedIndicators.Select(i => i.EstimatedSurvivalPct));
        dto.Kpis.EstimatedBiomassKg = stockedIndicators.Sum(i => i.EstimatedBiomassKg ?? 0);

        var cycles = await _cycles.GetAll().AsNoTracking().Where(c => c.Status == CycleStatus.Active).OrderBy(c => c.CycleCode).ToListAsync();
        var cycleIds = cycles.Select(c => c.Id).ToList();
        var allCycleBatches = await _batches.GetAll().AsNoTracking().Where(b => cycleIds.Contains(b.CycleId)).Select(b => new { b.CycleId, b.Status, b.StockingDate }).ToListAsync();
        dto.ActiveCycles = cycles.Select(c => new CycleTileDto
        {
            Id = c.Id, CycleCode = c.CycleCode, Status = c.Status, StartDate = allCycleBatches.Where(b => b.CycleId == c.Id).Min(b => b.StockingDate),
            BatchCount = allCycleBatches.Count(b => b.CycleId == c.Id), StockedCount = allCycleBatches.Count(b => b.CycleId == c.Id && b.Status == BatchStatus.Stocked),
        }).ToList();
        dto.Kpis.ActiveCycles = cycles.Count(c => c.Status == CycleStatus.Active);

        var items = await _items.GetAll().AsNoTracking().Where(i => i.IsActive).ToListAsync();
        var itemQty = await (from m in _movements.GetAll()
                             join l in _lots.GetAll() on m.LotId equals l.Id
                             group m by l.ItemId into g
                             select new { g.Key, Qty = g.Sum(x => x.Quantity) }).ToDictionaryAsync(x => x.Key, x => x.Qty);
        foreach (var item in items)
        {
            var qty = itemQty.TryGetValue(item.Id, out var q) ? q : 0m;
            if (qty >= 0 && item.ReorderLevel.HasValue && qty <= item.ReorderLevel.Value)
            {
                dto.StockAlerts.Add(new StockAlertDto { ItemId = item.Id, ItemName = item.DisplayName, Quantity = qty, Unit = item.Unit, Kind = "Low" });
            }
        }

        var lots = await _lots.GetAll().AsNoTracking().Include(l => l.Item).ToListAsync();
        var lotQty = await _movements.GetAll().GroupBy(m => m.LotId).Select(g => new { g.Key, Qty = g.Sum(x => x.Quantity) }).ToDictionaryAsync(x => x.Key, x => x.Qty);
        foreach (var lot in lots)
        {
            var qty = lotQty.TryGetValue(lot.Id, out var q) ? q : 0m;
            if (qty < 0)
            {
                dto.StockAlerts.Add(new StockAlertDto { ItemId = lot.ItemId, LotId = lot.Id, ItemName = lot.Item.DisplayName, LotNumber = lot.LotNumber, Quantity = qty, Unit = lot.Item.Unit, Kind = "Negative" });
            }
            else if (lot.NeedsReconciliation)
            {
                dto.StockAlerts.Add(new StockAlertDto { ItemId = lot.ItemId, LotId = lot.Id, ItemName = lot.Item.DisplayName, LotNumber = lot.LotNumber, Quantity = qty, Unit = lot.Item.Unit, Kind = "Reconcile" });
            }
        }

        var recent = await _logs.GetAll().AsNoTracking().Include(l => l.Batch).OrderByDescending(l => l.CreationTime).Take(10).ToListAsync();
        var names = await GetUserNamesAsync(recent.Select(l => l.CreatorUserId));
        dto.RecentLogs = recent.Select(l => new RecentLogDto
        {
            Id = l.Id, BatchId = l.BatchId, LogType = l.LogType, ObservedAtUtc = l.ObservedAtUtc, BatchCode = l.Batch?.BatchCode, RecordedBy = NameOf(names, l.CreatorUserId),
        }).ToList();

        dto.MonitoringGuidance = string.Join("; ", parameters.Values.Where(p => p.IsActive).OrderBy(p => p.SortOrder).Select(p => $"{p.Name}: {p.GuidanceFrequency}"));

        if (isOwner)
        {
            await _notifications.RefreshDocumentAlertsAsync();
            var horizon = today.AddDays(profile.DocumentExpiryAlertDays);
            var docs = await _documents.GetAll().AsNoTracking().Include(d => d.Category)
                .Where(d => !d.IsArchived && d.ExpiryDate != null && d.ExpiryDate <= horizon).OrderBy(d => d.ExpiryDate).ToListAsync();
            dto.DocumentAlerts = docs.Select(d => new DocumentAlertDto
            {
                DocumentId = d.Id, Title = d.Title, CategoryName = d.Category?.Name, ExpiryDate = d.ExpiryDate, Status = d.GetExpiryStatus(today, profile.DocumentExpiryAlertDays),
            }).ToList();
            dto.MissingRequiredDocuments = await _compliance.GetMissingRequiredPermitsAsync(today);

            dto.FinishStocking = await _deliveries.GetOpenWithPlLeft();
            var userCount = await UserRepository.CountAsync();
            dto.SetupTasks = new List<SetupTaskDto>
            {
                new() { Key = "profile", Label = "Complete the farm profile", Done = !string.IsNullOrWhiteSpace(profile.AddressLine1) && !string.IsNullOrWhiteSpace(profile.CityMunicipality) && !string.IsNullOrWhiteSpace(profile.ContactPhone), Route = "/app/farm/profile" },
                new() { Key = "standards", Label = "Review Farm standards (water ranges, density bands, warning thresholds)", Done = profile.StandardsReviewedAtUtc.HasValue, Route = "/app/farm/standards" },
                new() { Key = "documents", Label = "Record permits and licenses", Done = await _documents.GetAll().AnyAsync(), Route = "/app/farm/documents" },
                new() { Key = "ponds", Label = "Add ponds", Done = ponds.Count > 0, Route = "/app/ponds" },
                new() { Key = "hatcheries", Label = "Add hatchery sources", Done = await _hatcheries.GetAll().AnyAsync(), Route = "/app/resources/hatcheries" },
                new() { Key = "buyers", Label = "Add buyers", Done = await _buyers.GetAll().AnyAsync(), Route = "/app/resources/buyers" },
                new() { Key = "inventory", Label = "Set up inventory items and record opening stock", Done = await _movements.GetAll().AnyAsync(), Route = "/app/inventory" },
                new() { Key = "users", Label = "Create user accounts", Done = userCount > 1, Route = "/app/farm/users" },
            };
        }

        return dto;
    }

    private static decimal? Avg(IEnumerable<decimal?> values)
    {
        var list = values.Where(v => v.HasValue).Select(v => v.Value).ToList();
        return list.Count == 0 ? null : Math.Round(list.Average(), 2);
    }
}
