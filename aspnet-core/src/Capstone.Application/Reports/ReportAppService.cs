using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.UI;
using Capstone.Authorization;
using Capstone.Common;
using Capstone.Domain;
using Capstone.Harvests;
using Capstone.Inventory;
using Capstone.Logbooks;
using Capstone.Logbooks.Dto;
using Capstone.Ponds;
using Capstone.Production;
using Capstone.Production.Dto;
using Capstone.ReferenceData;
using Capstone.Reports.Dto;
using Capstone.Resources;
using Capstone.Sales;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Capstone.Reports;

[AbpAuthorize(PermissionNames.Reports)]
public class ReportAppService : CapstoneAppServiceBase
{
    private readonly IRepository<ProductionCycle, Guid> _cycles;
    private readonly IRepository<PondBatch, Guid> _batches;
    private readonly IRepository<BatchFailure, Guid> _failures;
    private readonly IRepository<ProductionLog, Guid> _logs;
    private readonly IRepository<GrowthSamplingDetail, Guid> _growth;
    private readonly IRepository<MortalityDetail, Guid> _mortality;
    private readonly IRepository<ChemicalApplicationDetail, Guid> _chemical;
    private readonly IRepository<WaterQualityMeasurement, Guid> _measurements;
    private readonly IRepository<Harvest, Guid> _harvests;
    private readonly IRepository<Sale, Guid> _sales;
    private readonly IRepository<StockMovement, Guid> _movements;
    private readonly IRepository<InventoryLot, Guid> _lots;
    private readonly IRepository<InventoryItem, Guid> _items;
    private readonly IRepository<SaleLine, Guid> _saleLines;
    private readonly IRepository<Resources.FryDeliveryShortfall, Guid> _shortfalls;
    private readonly IRepository<HatcherySource, Guid> _hatcheries;
    private readonly BatchDataLoader _loader;
    private readonly WaterQualityEvaluator _waterQuality;
    private readonly IRepository<Pond, Guid> _ponds;

    public ReportAppService(
        IRepository<ProductionCycle, Guid> cycles,
        IRepository<PondBatch, Guid> batches,
        IRepository<BatchFailure, Guid> failures,
        IRepository<ProductionLog, Guid> logs,
        IRepository<GrowthSamplingDetail, Guid> growth,
        IRepository<MortalityDetail, Guid> mortality,
        IRepository<ChemicalApplicationDetail, Guid> chemical,
        IRepository<WaterQualityMeasurement, Guid> measurements,
        IRepository<Harvest, Guid> harvests,
        IRepository<Sale, Guid> sales,
        IRepository<StockMovement, Guid> movements,
        IRepository<InventoryLot, Guid> lots,
        IRepository<InventoryItem, Guid> items,
        IRepository<SaleLine, Guid> saleLines,
        IRepository<Resources.FryDeliveryShortfall, Guid> shortfalls,
        IRepository<HatcherySource, Guid> hatcheries,
        BatchDataLoader loader,
        WaterQualityEvaluator waterQuality,
        IRepository<Pond, Guid> ponds)
    {
        _ponds = ponds;
        _cycles = cycles;
        _batches = batches;
        _failures = failures;
        _logs = logs;
        _growth = growth;
        _mortality = mortality;
        _chemical = chemical;
        _measurements = measurements;
        _harvests = harvests;
        _sales = sales;
        _movements = movements;
        _lots = lots;
        _items = items;
        _saleLines = saleLines;
        _shortfalls = shortfalls;
        _hatcheries = hatcheries;
        _loader = loader;
        _waterQuality = waterQuality;
    }

    private async Task<ReportHeaderDto> HeaderAsync(string scope)
    {
        var profile = await Farm.GetProfileAsync();
        var tenant = await GetCurrentTenantAsync();
        var user = await GetCurrentUserAsync();
        return new ReportHeaderDto
        {
            FarmName = profile.FarmName, FarmCode = tenant.TenancyName, FarmAddress = profile.FormatAddress(), TimeZone = profile.TimeZone,
            GeneratedAtUtc = DateTime.UtcNow, GeneratedBy = $"{user.Name} {user.Surname}".Trim(), Scope = scope,
        };
    }

    private static string RangeText(DateOnly? from, DateOnly? to) =>
        from.HasValue || to.HasValue ? $"{(from.HasValue ? from.Value.ToString("yyyy-MM-dd") : "start")} to {(to.HasValue ? to.Value.ToString("yyyy-MM-dd") : "today")}" : "all dates";

    public const int MaxRangeMonths = 24;

    private async Task<(DateTime? From, DateTime? To)> UtcRangeAsync(DateRangeInput input, bool scoped = false)
    {
        var from = input.From;
        var to = input.To;
        if (from.HasValue && to.HasValue && to < from)
        {
            throw new UserFriendlyException("The end date cannot be before the start date.");
        }

        var zone = await Farm.GetTimeZoneAsync();
        if (!scoped)
        {
            var end = to ?? FarmClock.Today(zone);
            var start = from ?? end.AddMonths(-MaxRangeMonths);
            if (start.AddMonths(MaxRangeMonths) < end)
            {
                throw new UserFriendlyException($"Reports cover at most {MaxRangeMonths} months. Choose a shorter range, or export the list as CSV.");
            }

            input.From = from = start;
            input.To = to = end;
        }

        return (from.HasValue ? FarmClock.StartOfDayUtc(from.Value, zone) : null, to.HasValue ? FarmClock.StartOfDayUtc(to.Value.AddDays(1), zone) : null);
    }

    public virtual async Task<CyclePerformanceDto> GetCyclePerformance(Guid cycleId)
    {
        var cycle = await _cycles.GetAll().AsNoTracking().Include(c => c.FryDelivery).ThenInclude(d => d.Hatchery).FirstOrDefaultAsync(c => c.Id == cycleId)
                    ?? throw new UserFriendlyException("Production cycle not found.");
        var batches = await _batches.GetAll().AsNoTracking().Include(b => b.Pond).Where(b => b.CycleId == cycleId).OrderBy(b => b.Pond.PondCode).ToListAsync();
        var zone = await Farm.GetTimeZoneAsync();
        var today = FarmClock.Today(zone);
        var batchIds = batches.Select(b => b.Id).ToList();
        var facts = await _loader.LoadAsync(batchIds, zone);
        var parameters = await _waterQuality.GetParametersAsync();
        var finance = await BatchFinanceAsync(cycle, batches);
        var failures = await _failures.GetAll().AsNoTracking().Where(f => batchIds.Contains(f.BatchId)).ToDictionaryAsync(f => f.BatchId);

        var dto = new CyclePerformanceDto
        {
            Header = await HeaderAsync($"Cycle {cycle.CycleCode}"),
            CycleId = cycle.Id, CycleCode = cycle.CycleCode, Status = cycle.Status, IsFinal = cycle.Status == CycleStatus.Closed,
            HatcheryName = cycle.FryDelivery?.Hatchery?.Name, HatcheryShortCode = cycle.FryDelivery?.Hatchery?.ShortCode,
            HatcheryLotNumber = cycle.FryDelivery?.HatcheryBatchNumber,
        };

        var feedMovements = await (from m in _movements.GetAll().AsNoTracking()
                                   join lot in _lots.GetAll().AsNoTracking() on m.LotId equals lot.Id
                                   join item in _items.GetAll().AsNoTracking() on lot.ItemId equals item.Id
                                   where m.FeedingSessionId != null && m.BatchId != null && batchIds.Contains(m.BatchId.Value)
                                   select new { BatchId = m.BatchId.Value, item.Brand, item.ProductName, item.BaiRegistrationNo, item.Unit, m.Quantity, item.Id }).ToListAsync();
        var feedRows = feedMovements.Select(x => new { x.BatchId, x.Brand, x.ProductName, x.BaiRegistrationNo, QuantityKg = Domain.Units.Convert(-x.Quantity, x.Unit, "kg") ?? -x.Quantity, x.Id }).ToList();
        var usageCostByItem = await (from m in _movements.GetAll().AsNoTracking()
                                     join lot in _lots.GetAll().AsNoTracking() on m.LotId equals lot.Id
                                     where m.BatchId != null && batchIds.Contains(m.BatchId.Value)
                                     group m by new { m.BatchId, lot.ItemId } into g
                                     select new { g.Key.BatchId, g.Key.ItemId, Cost = g.Sum(x => -x.Quantity * (x.UnitCost ?? 0)) }).ToListAsync();

        var wq = await (from l in _logs.GetAll().AsNoTracking()
                        join m in _measurements.GetAll().AsNoTracking() on l.Id equals m.LogId
                        where batchIds.Contains(l.BatchId) && l.LogType == LogType.WaterQuality
                        select new { l.BatchId, l.ObservedAtUtc, m.ParameterCode, m.Value, m.UnitCode, m.IsOutOfRange }).ToListAsync();

        var growth = await (from l in _logs.GetAll().AsNoTracking()
                            join g in _growth.GetAll().AsNoTracking() on l.Id equals g.Id
                            where batchIds.Contains(l.BatchId)
                            orderby l.ObservedAtUtc
                            select new { l.BatchId, l.ObservedAtUtc, g.SampleCount, g.TotalSampleWeightG, g.AbwG }).ToListAsync();

        var harvestRows = await HarvestRowsAsync(_harvests.GetAll().Where(h => batchIds.Contains(h.BatchId)), zone);

        foreach (var b in batches)
        {
            var f = facts.TryGetValue(b.Id, out var bf) ? bf : new BatchFacts();
            var ind = IndicatorCalculator.Compute(f, today);
            var isFinal = b.Status == BatchStatus.Closed;
            var indicators = IndicatorsDto.From(ind);
            if (isFinal)
            {
                indicators.FinalAbwG = b.FinalAbwG;
                indicators.FinalFcr = b.FinalFcr;
                indicators.SurvivalRatePct = b.SurvivalRatePct;
                indicators.CultureDays = b.CultureDays;
                indicators.Doc = null;
            }

            var bp = new BatchPerformanceDto
            {
                BatchId = b.Id, BatchCode = b.BatchCode, PondCode = b.Pond?.PondCode, PondAreaM2 = b.PondAreaM2AtStocking, Status = b.Status,
                StockingDate = b.StockingDate, PlCountStocked = b.PlCountStocked, PlAgeDays = cycle.FryDelivery?.PlAgeDays, HatcheryLotNumber = cycle.FryDelivery?.HatcheryBatchNumber,
                HatcheryCertificationStatusAtStocking = b.HatcheryCertificationStatusAtStocking, FailureReason = failures.TryGetValue(b.Id, out var batchFailure) ? batchFailure.Reason : null, IsFinal = isFinal,
                Indicators = indicators,
                Feed = feedRows.Where(r => r.BatchId == b.Id).GroupBy(r => new { r.Id, r.Brand, r.ProductName, r.BaiRegistrationNo })
                    .Select(g => new FeedByItemDto
                    {
                        ItemName = string.IsNullOrWhiteSpace(g.Key.Brand) ? g.Key.ProductName : $"{g.Key.Brand} {g.Key.ProductName}",
                        BaiRegistrationNo = g.Key.BaiRegistrationNo,
                        QuantityKg = g.Sum(x => x.QuantityKg),
                        Cost = Math.Round(usageCostByItem.Where(u => u.BatchId == b.Id && u.ItemId == g.Key.Id).Sum(u => u.Cost), 2),
                    }).ToList(),
                Finance = finance.TryGetValue(b.Id, out var bfin) ? bfin : new BatchFinanceDto(),
                Harvests = harvestRows.Where(h => h.BatchId == b.Id).ToList(),
            };

            bp.WaterQuality = wq.Where(r => r.BatchId == b.Id)
                .Select(r => new { r, Local = FarmClock.LocalDate(r.ObservedAtUtc, zone) })
                .GroupBy(x => new { Week = WeekStart(x.Local), x.r.ParameterCode })
                .OrderBy(g => g.Key.Week).ThenBy(g => parameters.TryGetValue(g.Key.ParameterCode, out var p) ? p.SortOrder : 99)
                .Select(g => new WeeklyWaterQualityDto
                {
                    WeekStart = g.Key.Week,
                    ParameterCode = g.Key.ParameterCode,
                    ParameterName = parameters.TryGetValue(g.Key.ParameterCode, out var p) ? p.Name : g.Key.ParameterCode,
                    UnitCode = g.First().r.UnitCode,
                    Readings = g.Count(),
                    Average = Math.Round(g.Average(x => x.r.Value), 3),
                    Min = g.Min(x => x.r.Value),
                    Max = g.Max(x => x.r.Value),
                    OutOfRangeCount = g.Count(x => x.r.IsOutOfRange),
                }).ToList();

            var samples = growth.Where(g => g.BatchId == b.Id).ToList();
            for (var i = 0; i < samples.Count; i++)
            {
                var s = samples[i];
                var date = FarmClock.LocalDate(s.ObservedAtUtc, zone);
                decimal? rate = null;
                if (i > 0)
                {
                    rate = IndicatorCalculator.GrowthRate(samples[i - 1].AbwG, FarmClock.LocalDate(samples[i - 1].ObservedAtUtc, zone), s.AbwG, date);
                }

                bp.Growth.Add(new GrowthRowDto
                {
                    Date = date, Doc = IndicatorCalculator.Doc(b.StockingDate, date), SampleCount = s.SampleCount, TotalSampleWeightG = s.TotalSampleWeightG,
                    AbwG = s.AbwG, GrowthRateGPerDay = rate,
                });
            }

            dto.Batches.Add(bp);
            dto.TotalPlStocked += b.PlCountStocked ?? 0;
            dto.TotalFeedKg += ind.CumulativeFeedKg;
            dto.TotalHarvestKg += ind.TotalHarvestKg;
            dto.TotalHarvestedPieces += ind.HarvestedPieces;
            dto.Finance.Revenue += bp.Finance.Revenue;
            dto.Finance.InventoryUsageCost += bp.Finance.InventoryUsageCost;
            dto.Finance.PlCost += bp.Finance.PlCost;
        }

        dto.CycleFcr = dto.TotalHarvestKg > 0 ? Math.Round(dto.TotalFeedKg / dto.TotalHarvestKg, 3) : null;
        dto.CycleSurvivalPct = dto.TotalPlStocked > 0 && dto.TotalHarvestedPieces > 0 ? Math.Round(dto.TotalHarvestedPieces / dto.TotalPlStocked * 100, 2) : null;
        return dto;
    }

    private static DateOnly WeekStart(DateOnly date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-offset);
    }

    private async Task<Dictionary<Guid, BatchFinanceDto>> BatchFinanceAsync(ProductionCycle cycle, List<PondBatch> batches)
    {
        var batchIds = batches.Select(b => b.Id).ToList();
        var usage = await _movements.GetAll().Where(m => m.BatchId != null && batchIds.Contains(m.BatchId.Value))
            .GroupBy(m => m.BatchId).Select(g => new { g.Key, Cost = g.Sum(x => -x.Quantity * (x.UnitCost ?? 0)) }).ToListAsync();
        var revenueRows = await (from l in _saleLines.GetAll()
                                 join s in _sales.GetAll() on l.SaleId equals s.Id
                                 join h in _harvests.GetAll() on l.HarvestGradeLine.HarvestId equals h.Id
                                 where batchIds.Contains(h.BatchId)
                                 select new { h.BatchId, l.Kg, l.BuyerAcceptedKg, l.PricePerKg }).ToListAsync();
        var deliveryCost = cycle.FryDelivery?.CostPhp ?? 0m;
        var stockedPl = batches.Sum(b => b.PlCountStocked ?? 0);
        var result = new Dictionary<Guid, BatchFinanceDto>();
        foreach (var b in batches)
        {
            result[b.Id] = new BatchFinanceDto
            {
                Revenue = revenueRows.Where(r => r.BatchId == b.Id).Sum(r => Math.Round((r.BuyerAcceptedKg ?? r.Kg) * r.PricePerKg, 2, MidpointRounding.AwayFromZero)),
                InventoryUsageCost = Math.Round(usage.FirstOrDefault(u => u.Key == b.Id)?.Cost ?? 0m, 2),
                PlCost = stockedPl > 0 && b.PlCountStocked > 0 ? Math.Round(deliveryCost * b.PlCountStocked.Value / stockedPl, 2) : 0m,
            };
        }

        return result;
    }

    private async Task<List<HarvestRowDto>> HarvestRowsAsync(IQueryable<Harvest> query, TimeZoneInfo zone)
    {
        var harvests = await query.AsNoTracking().Include(h => h.GradeLines).ThenInclude(g => g.SizeGrade).Include(h => h.Batch).ThenInclude(b => b.Pond)
            .Include(h => h.Batch).ThenInclude(b => b.Cycle).OrderBy(h => h.HarvestedAtUtc).ToListAsync();
        return harvests.Select(h =>
        {
            var fact = HarvestFact.From(h, FarmClock.LocalDate(h.HarvestedAtUtc, zone));
            string Check(bool? v) => v == null ? "Not assessed" : v.Value ? "Yes" : "No";
            return new HarvestRowDto
            {
                HarvestId = h.Id, BatchId = h.BatchId, BatchCode = h.Batch.BatchCode, PondCode = h.Batch.Pond?.PondCode, CycleCode = h.Batch.Cycle?.CycleCode,
                Date = FarmClock.LocalDate(h.HarvestedAtUtc, zone), HarvestType = h.HarvestType, TotalKg = h.TotalKg, Boxes = h.Boxes, AbwG = h.AbwG,
                Pieces = Math.Round(IndicatorCalculator.HarvestedPieces(fact, null), 0),
                QualityChecks = $"Smooth shell: {Check(h.SmoothShell)}; intact appendages: {Check(h.IntactAppendages)}; no necrosis: {Check(h.NoNecrosis)}",
                Grades = h.GradeLines.OrderBy(g => g.SizeGrade?.IsUngraded == true).ThenBy(g => g.SizeGrade?.MinCountPerKg)
                    .Select(g => new GradeLineDto { Id = g.Id, SizeGradeId = g.SizeGradeId, SizeLabel = g.SizeGrade?.Label, MinCountPerKg = g.SizeGrade?.MinCountPerKg, MaxCountPerKg = g.SizeGrade?.MaxCountPerKg, IsUngraded = g.SizeGrade?.IsUngraded ?? false, Kg = g.Kg })
                    .ToList(),
            };
        }).ToList();
    }

    public virtual async Task<HarvestSummaryDto> GetHarvestSummary(HarvestSummaryInput input)
    {
        var zone = await Farm.GetTimeZoneAsync();
        var (fromUtc, toUtc) = await UtcRangeAsync(input, scoped: input.BatchId.HasValue || input.CycleId.HasValue);
        var query = _harvests.GetAll()
            .Where(h => !input.BatchId.HasValue || h.BatchId == input.BatchId)
            .Where(h => !input.CycleId.HasValue || h.Batch.CycleId == input.CycleId)
            .Where(h => !fromUtc.HasValue || h.HarvestedAtUtc >= fromUtc)
            .Where(h => !toUtc.HasValue || h.HarvestedAtUtc < toUtc);
        var rows = await HarvestRowsAsync(query, zone);
        var harvestIds = rows.Select(r => r.HarvestId).ToList();
        var batchIds = rows.Select(r => r.BatchId).Distinct().ToList();
        var stocked = await _batches.GetAll().AsNoTracking().Where(b => batchIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.PlCountStocked ?? 0);

        var dto = new HarvestSummaryDto
        {
            Header = await HeaderAsync($"Harvests, {RangeText(input.From, input.To)}"),
            Harvests = rows,
            TotalKg = rows.Sum(r => r.TotalKg),
            TotalPieces = rows.Sum(r => r.Pieces),
        };
        dto.WeightedAbwG = dto.TotalPieces > 0 ? Math.Round(dto.TotalKg * 1000 / dto.TotalPieces, 2) : null;
        dto.Batches = rows.GroupBy(r => new { r.BatchId, r.BatchCode, r.PondCode, r.CycleCode }).Select(g =>
        {
            var kg = g.Sum(x => x.TotalKg);
            var pcs = g.Sum(x => x.Pieces);
            var pl = stocked.TryGetValue(g.Key.BatchId, out var n) ? n : 0;
            return new BatchHarvestTotalsDto
            {
                BatchId = g.Key.BatchId, BatchCode = g.Key.BatchCode, PondCode = g.Key.PondCode, CycleCode = g.Key.CycleCode, TotalKg = kg, Pieces = pcs,
                WeightedAbwG = pcs > 0 ? Math.Round(kg * 1000 / pcs, 2) : null,
                SurvivalRatePct = pl > 0 ? Math.Round(pcs / pl * 100, 2) : null,
            };
        }).ToList();

        var gradeKg = rows.SelectMany(r => r.Grades).Sum(g => g.Kg);
        dto.SizeDistribution = rows.SelectMany(r => r.Grades).GroupBy(g => new { g.SizeLabel, g.MinCountPerKg })
            .OrderBy(g => g.Key.MinCountPerKg ?? int.MaxValue)
            .Select(g => new SizeDistributionDto
            {
                SizeLabel = g.Key.SizeLabel, MinCountPerKg = g.Key.MinCountPerKg, Kg = g.Sum(x => x.Kg), Pct = gradeKg > 0 ? Math.Round(g.Sum(x => x.Kg) / gradeKg * 100, 1) : 0,
            }).ToList();

        var soldLines = await (from l in _saleLines.GetAll()
                               join s in _sales.GetAll() on l.SaleId equals s.Id
                               where harvestIds.Contains(l.HarvestGradeLine.HarvestId)
                               select new { BuyerName = s.Buyer.Name, l.Kg, l.BuyerAcceptedKg, l.PricePerKg }).ToListAsync();
        decimal Amount(decimal kg, decimal? buyerKg, decimal price) => Math.Round((buyerKg ?? kg) * price, 2, MidpointRounding.AwayFromZero);
        dto.SoldKg = soldLines.Sum(l => l.Kg);
        dto.SalesAmount = soldLines.Sum(l => Amount(l.Kg, l.BuyerAcceptedKg, l.PricePerKg));
        dto.BuyerAllocation = soldLines.GroupBy(l => l.BuyerName ?? "(unknown)").Select(g => new BuyerAllocationDto
        {
            BuyerName = g.Key,
            Kg = g.Sum(l => l.Kg),
            Amount = g.Sum(l => Amount(l.Kg, l.BuyerAcceptedKg, l.PricePerKg)),
            Pct = dto.SoldKg > 0 ? Math.Round(g.Sum(l => l.Kg) / dto.SoldKg * 100, 1) : 0,
        }).OrderByDescending(b => b.Kg).ToList();
        return dto;
    }

    public virtual async Task<FailureReportDto> GetFailureReport(FailureReportInput input)
    {
        List<PondBatch> batches;
        ProductionCycle cycle;
        if (input.BatchId.HasValue)
        {
            var batch = await _batches.GetAll().AsNoTracking().Include(b => b.Pond).FirstOrDefaultAsync(b => b.Id == input.BatchId)
                        ?? throw new UserFriendlyException("Batch not found.");
            batches = new List<PondBatch> { batch };
            cycle = await _cycles.GetAll().AsNoTracking().Include(c => c.FryDelivery).ThenInclude(d => d.Hatchery).FirstAsync(c => c.Id == batch.CycleId);
        }
        else if (input.CycleId.HasValue)
        {
            cycle = await _cycles.GetAll().AsNoTracking().Include(c => c.FryDelivery).ThenInclude(d => d.Hatchery).FirstOrDefaultAsync(c => c.Id == input.CycleId)
                    ?? throw new UserFriendlyException("Production cycle not found.");
            var failedIds = await _failures.GetAll().Select(f => f.BatchId).ToListAsync();
            batches = await _batches.GetAll().AsNoTracking().Include(b => b.Pond).Where(b => b.CycleId == cycle.Id && failedIds.Contains(b.Id)).ToListAsync();
        }
        else
        {
            throw new UserFriendlyException("Choose a batch or a cycle.");
        }

        var zone = await Farm.GetTimeZoneAsync();
        var batchIds = batches.Select(b => b.Id).ToList();
        var facts = await _loader.LoadAsync(batchIds, zone);
        var batchFailures = await _failures.GetAll().AsNoTracking().Where(f => batchIds.Contains(f.BatchId)).ToDictionaryAsync(f => f.BatchId);
        var mortality = await (from l in _logs.GetAll().AsNoTracking()
                               join m in _mortality.GetAll().AsNoTracking() on l.Id equals m.Id
                               where batchIds.Contains(l.BatchId)
                               orderby l.ObservedAtUtc
                               select new { l.BatchId, l.ObservedAtUtc, m.Count, m.Cause, m.DiseaseName, m.OtherCause, m.Symptoms }).ToListAsync();
        var treatments = await (from l in _logs.GetAll().AsNoTracking()
                                join t in _chemical.GetAll().AsNoTracking() on l.Id equals t.Id
                                where batchIds.Contains(l.BatchId)
                                orderby l.ObservedAtUtc
                                select new { l.BatchId, l.ObservedAtUtc, t.Reason }).ToListAsync();
        var parameters = await _waterQuality.GetParametersAsync();
        var wq = await (from l in _logs.GetAll().AsNoTracking()
                        join m in _measurements.GetAll().AsNoTracking() on l.Id equals m.LogId
                        where batchIds.Contains(l.BatchId)
                        orderby l.ObservedAtUtc descending
                        select new { l.BatchId, l.ObservedAtUtc, m.ParameterCode, m.Value, m.UnitCode, m.IsOutOfRange }).Take(200).ToListAsync();
        var usage = await _movements.GetAll().Where(m => m.BatchId != null && batchIds.Contains(m.BatchId.Value))
            .GroupBy(m => m.BatchId).Select(g => new { g.Key, Cost = g.Sum(x => -x.Quantity * (x.UnitCost ?? 0)) }).ToListAsync();

        string Local(DateTime utc) => FarmClock.ToLocal(utc, zone).ToString("yyyy-MM-dd HH:mm");
        var dto = new FailureReportDto
        {
            Header = await HeaderAsync(input.BatchId.HasValue ? $"Batch failure report" : $"Cycle failure report, {cycle.CycleCode}"),
            CycleCode = cycle.CycleCode,
            HatcheryName = cycle.FryDelivery?.Hatchery?.Name,
            HatcheryShortCode = cycle.FryDelivery?.Hatchery?.ShortCode,
            HatcheryAccreditation = cycle.FryDelivery?.Hatchery == null ? null : $"{cycle.FryDelivery.Hatchery.AccreditationStatus} {cycle.FryDelivery.Hatchery.BfarAccreditationNo}".Trim(),

        };

        foreach (var b in batches)
        {
            var failure = batchFailures.TryGetValue(b.Id, out var bf) ? bf : null;
            var onDate = failure?.FailedOn ?? FarmClock.Today(zone);
            var ind = IndicatorCalculator.Compute(facts.TryGetValue(b.Id, out var f) ? f : new BatchFacts(), onDate);
            dto.Batches.Add(new FailureBatchDto
            {
                BatchCode = b.BatchCode, PondCode = b.Pond?.PondCode, PondAreaM2 = b.PondAreaM2AtStocking, Status = b.Status, StockingDate = b.StockingDate,
                PlCountStocked = b.PlCountStocked, HatcheryLotNumber = cycle.FryDelivery?.HatcheryBatchNumber, HatcheryCertificationStatusAtStocking = b.HatcheryCertificationStatusAtStocking,
                FailureReason = failure?.Reason, FailureDate = failure?.FailedOn, EstimatedLossPhp = failure?.EstimatedLossPhp,
                DocAtFailure = IndicatorCalculator.Doc(b.StockingDate, onDate), Indicators = IndicatorsDto.From(ind),
                MortalityEvents = mortality.Where(m => m.BatchId == b.Id)
                    .Select(m => $"{Local(m.ObservedAtUtc)}: {m.Count:N0} dead; cause {m.Cause}{(m.DiseaseName != null ? $" ({m.DiseaseName})" : m.OtherCause != null ? $" ({m.OtherCause})" : string.Empty)}{(string.IsNullOrWhiteSpace(m.Symptoms) ? string.Empty : $"; {m.Symptoms}")}").ToList(),
                Treatments = treatments.Where(t => t.BatchId == b.Id)
                    .Select(t => $"{Local(t.ObservedAtUtc)}: chemical or supplement application{(string.IsNullOrWhiteSpace(t.Reason) ? string.Empty : $" — {t.Reason}")}").ToList(),
                RecentWaterQuality = wq.Where(w => w.BatchId == b.Id).Take(24)
                    .Select(w => $"{Local(w.ObservedAtUtc)}: {(parameters.TryGetValue(w.ParameterCode, out var p) ? p.Name : w.ParameterCode)} {w.Value:0.###} {w.UnitCode}{(w.IsOutOfRange ? " (out of range)" : string.Empty)}").ToList(),
                InventoryUsageCost = Math.Round(usage.FirstOrDefault(u => u.Key == b.Id)?.Cost ?? 0m, 2),
            });
        }

        return dto;
    }

    public virtual async Task<WaterQualityExceptionReportDto> GetWaterQualityExceptions(WaterQualityExceptionInput input)
    {
        var (fromUtc, toUtc) = await UtcRangeAsync(input, scoped: input.BatchId.HasValue);
        var parameters = await _waterQuality.GetParametersAsync();
        var inRange = from l in _logs.GetAll().AsNoTracking()
                      join m in _measurements.GetAll().AsNoTracking() on l.Id equals m.LogId
                      join b in _batches.GetAll().AsNoTracking() on l.BatchId equals b.Id
                      where (!input.BatchId.HasValue || l.BatchId == input.BatchId)
                            && (!fromUtc.HasValue || l.ObservedAtUtc >= fromUtc)
                            && (!toUtc.HasValue || l.ObservedAtUtc < toUtc)
                      select new { l.ObservedAtUtc, b.BatchCode, b.PondId, m.ParameterCode, m.Value, m.UnitCode, m.IsOutOfRange, l.CreatorUserId, l.BatchId };
        var readingsChecked = await inRange.CountAsync();
        var rows = await inRange.Where(r => r.IsOutOfRange).OrderByDescending(r => r.ObservedAtUtc).ToListAsync();
        var pondIds = rows.Select(r => r.PondId).Distinct().ToList();
        var ponds = await _ponds.GetAll().AsNoTracking().Where(p => pondIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.PondCode);
        var names = await GetUserNamesAsync(rows.Select(r => r.CreatorUserId));
        string ParameterName(string code) => parameters.TryGetValue(code, out var p) ? p.Name : code;
        var top = rows.GroupBy(r => r.ParameterCode).OrderByDescending(g => g.Count()).FirstOrDefault();
        return new WaterQualityExceptionReportDto
        {
            Header = await HeaderAsync($"Water quality exceptions, {RangeText(input.From, input.To)}"),
            ExceptionCount = rows.Count, ReadingsChecked = readingsChecked,
            ExceptionRatePct = readingsChecked > 0 ? Math.Round((decimal)rows.Count / readingsChecked * 100, 1) : null,
            PondsAffected = pondIds.Count, BatchesAffected = rows.Select(r => r.BatchId).Distinct().Count(),
            MostFrequentParameter = top == null ? null : $"{ParameterName(top.Key)} ({top.Count()})",
            Items = rows.Select(r => new WaterQualityExceptionDto
            {
                ObservedAtUtc = r.ObservedAtUtc, BatchCode = r.BatchCode, PondCode = ponds.TryGetValue(r.PondId, out var pc) ? pc : null,
                ParameterName = ParameterName(r.ParameterCode), Value = r.Value, UnitCode = r.UnitCode,
                OptimalRange = parameters.TryGetValue(r.ParameterCode, out var p2) ? p2.DescribeOptimalRange() : null, RecordedBy = NameOf(names, r.CreatorUserId),
            }).ToList(),
        };
    }

    public virtual async Task<SalesByBuyerDto> GetSalesByBuyer(SalesByBuyerInput input)
    {
        await UtcRangeAsync(input);
        var today = await Farm.GetTodayAsync();
        var sales = await _sales.GetAll().AsNoTracking().Include(s => s.Buyer).Include(s => s.Lines).Include(s => s.Payments)
            .Where(s => !input.BuyerId.HasValue || s.BuyerId == input.BuyerId)
            .Where(s => !input.From.HasValue || s.SaleDate >= input.From)
            .Where(s => !input.To.HasValue || s.SaleDate <= input.To)
            .OrderByDescending(s => s.SaleDate).ThenByDescending(s => s.DispatchReceiptNo)
            .ToListAsync();

        decimal PaidKg(Sale s) => s.Lines.Sum(l => l.BuyerAcceptedKg ?? l.Kg);
        var rows = sales.Select(s => new SalesByBuyerSaleDto
        {
            SaleId = s.Id, DispatchReceiptNo = s.DispatchReceiptNo, SaleDate = s.SaleDate, DueDate = s.DueDate, BuyerName = s.Buyer?.Name,
            Kg = s.Lines.Sum(l => l.Kg), Amount = s.Total, Paid = s.Paid, Balance = Math.Max(0, s.Total - s.Paid), PaymentStatus = s.Status, IsOverdue = s.IsOverdue(today),
        }).ToList();

        var dto = new SalesByBuyerDto
        {
            Header = await HeaderAsync($"Sales by buyer, {RangeText(input.From, input.To)}"),
            SaleCount = sales.Count, TotalKg = rows.Sum(r => r.Kg), TotalAmount = rows.Sum(r => r.Amount), TotalPaid = rows.Sum(r => r.Paid), TotalBalance = rows.Sum(r => r.Balance),
            OverdueCount = rows.Count(r => r.IsOverdue), OverdueBalance = rows.Where(r => r.IsOverdue).Sum(r => r.Balance),
            Sales = rows,
        };
        var paidKg = sales.Sum(PaidKg);
        dto.AveragePricePerKg = paidKg > 0 ? Math.Round(dto.TotalAmount / paidKg, 2) : null;
        dto.Buyers = sales.GroupBy(s => new { s.BuyerId, Name = s.Buyer?.Name }).Select(g =>
        {
            var gRows = rows.Where(r => g.Any(s => s.Id == r.SaleId)).ToList();
            var kgPaid = g.Sum(PaidKg);
            return new SalesByBuyerRowDto
            {
                BuyerId = g.Key.BuyerId, BuyerName = g.Key.Name, SaleCount = g.Count(), Kg = gRows.Sum(r => r.Kg), Amount = gRows.Sum(r => r.Amount),
                Paid = gRows.Sum(r => r.Paid), Balance = gRows.Sum(r => r.Balance), AveragePricePerKg = kgPaid > 0 ? Math.Round(gRows.Sum(r => r.Amount) / kgPaid, 2) : null,
                OverdueCount = gRows.Count(r => r.IsOverdue),
            };
        }).OrderByDescending(b => b.Amount).ToList();
        return dto;
    }

    public virtual async Task<InventoryMovementReportDto> GetInventoryMovements(InventoryMovementInput input)
    {
        var (fromUtc, toUtc) = await UtcRangeAsync(input);
        var items = await _items.GetAll().AsNoTracking().Where(i => !input.ItemId.HasValue || i.Id == input.ItemId).OrderBy(i => i.Category).ThenBy(i => i.ProductName).ToListAsync();
        var itemIds = items.Select(i => i.Id).ToList();
        var all = await (from m in _movements.GetAll().AsNoTracking()
                         join l in _lots.GetAll().AsNoTracking() on m.LotId equals l.Id
                         where itemIds.Contains(l.ItemId)
                         select new { m.MovementDateUtc, m.Type, m.Quantity, m.UnitCost, m.BatchId, m.Reason, l.ItemId, l.LotNumber, m.CreationTime }).ToListAsync();
        var batchIds = all.Where(m => m.BatchId.HasValue).Select(m => m.BatchId.Value).Distinct().ToList();
        var batchCodes = await _batches.GetAll().AsNoTracking().Where(b => batchIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.BatchCode);
        bool InRange(DateTime t) => (!fromUtc.HasValue || t >= fromUtc) && (!toUtc.HasValue || t < toUtc);

        var dto = new InventoryMovementReportDto { Header = await HeaderAsync($"Inventory movements, {RangeText(input.From, input.To)}") };
        foreach (var item in items)
        {
            var moves = all.Where(m => m.ItemId == item.Id).ToList();
            var inRange = moves.Where(m => InRange(m.MovementDateUtc)).ToList();
            var opening = moves.Where(m => fromUtc.HasValue && m.MovementDateUtc < fromUtc).Sum(m => m.Quantity);
            var summary = new InventoryItemSummaryDto
            {
                ItemName = item.DisplayName, Category = item.Category, Unit = item.Unit, Opening = opening,
                Receipts = inRange.Where(m => m.Type == StockMovementType.Receipt).Sum(m => m.Quantity),
                Usage = -inRange.Where(m => m.Type == StockMovementType.Usage).Sum(m => m.Quantity),
                Adjustments = inRange.Where(m => m.Type == StockMovementType.Adjustment).Sum(m => m.Quantity),
                UsageCost = Math.Round(inRange.Where(m => m.Type == StockMovementType.Usage || (m.Type == StockMovementType.Adjustment && m.BatchId != null))
                    .Sum(m => -m.Quantity * (m.UnitCost ?? 0)), 2),
            };
            summary.Closing = summary.Opening + summary.Receipts - summary.Usage + summary.Adjustments;
            dto.Items.Add(summary);
            dto.Movements.AddRange(inRange.OrderBy(m => m.MovementDateUtc).ThenBy(m => m.CreationTime).Select(m => new InventoryMovementRowDto
            {
                MovementDateUtc = m.MovementDateUtc, ItemName = item.DisplayName, LotNumber = m.LotNumber, Unit = item.Unit, Type = m.Type, Quantity = m.Quantity,
                UnitCost = m.UnitCost, BatchCode = m.BatchId.HasValue && batchCodes.TryGetValue(m.BatchId.Value, out var bc) ? bc : null, Reason = m.Reason,
            }));
        }

        dto.Movements = dto.Movements.OrderBy(m => m.MovementDateUtc).ToList();
        dto.TotalUsageCost = dto.Items.Sum(i => i.UsageCost);
        dto.FeedUsedKg = Math.Round(dto.Items.Where(i => i.Category == InventoryCategory.Feed).Sum(i => Domain.Units.Convert(i.Usage, i.Unit, "kg") ?? i.Usage), 3);
        return dto;
    }
}
