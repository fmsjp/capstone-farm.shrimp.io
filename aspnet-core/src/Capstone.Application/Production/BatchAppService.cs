using Capstone.Common;
using Abp.Application.Services.Dto;
using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.Runtime.Session;
using Abp.UI;
using Capstone.Authorization;
using Capstone.Domain;
using Capstone.Harvests;
using Capstone.Production.Dto;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Capstone.Production;

[AbpAuthorize(PermissionNames.Production)]
public class BatchAppService : CapstoneAppServiceBase
{
    private readonly IRepository<PondBatch, Guid> _batches;
    private readonly IRepository<BatchFailure, Guid> _failures;
    private readonly IRepository<ProductionLog, Guid> _logs;
    private readonly IRepository<Harvest, Guid> _harvests;
    private readonly BatchDataLoader _loader;
    private readonly DailyLogStatusService _daily;
    private readonly IRepository<ReopenEvent, Guid> _reopenings;
    private readonly IRepository<ProductionCycle, Guid> _cycles;
    private readonly TreatmentWithdrawal _withdrawal;
    private readonly IRepository<Sales.SaleLine, Guid> _saleLines;
    private readonly IRepository<Sales.Sale, Guid> _sales;
    private readonly IRepository<Inventory.StockMovement, Guid> _movements;

    public BatchAppService(IRepository<PondBatch, Guid> batches, IRepository<BatchFailure, Guid> failures, IRepository<ProductionLog, Guid> logs,
        IRepository<Harvest, Guid> harvests, BatchDataLoader loader, DailyLogStatusService daily, IRepository<ReopenEvent, Guid> reopenings,
        IRepository<ProductionCycle, Guid> cycles, TreatmentWithdrawal withdrawal, IRepository<Sales.SaleLine, Guid> saleLines, IRepository<Sales.Sale, Guid> sales,
        IRepository<Inventory.StockMovement, Guid> movements)
    {
        _movements = movements;
        _saleLines = saleLines;
        _sales = sales;
        _reopenings = reopenings;
        _cycles = cycles;
        _withdrawal = withdrawal;
        _daily = daily;
        _batches = batches;
        _failures = failures;
        _logs = logs;
        _harvests = harvests;
        _loader = loader;
    }

    internal static BatchListDto MapList(PondBatch b, string cycleCode, BatchIndicators ind) => new()
    {
        Id = b.Id, BatchCode = b.BatchCode, Letter = b.Letter, CycleId = b.CycleId, CycleCode = cycleCode, PondId = b.PondId, PondCode = b.Pond?.PondCode,
        Status = b.Status, StockingDate = b.StockingDate, PlCountStocked = b.PlCountStocked,
        Doc = b.Status == BatchStatus.Stocked ? ind.Doc : null,
        CurrentAbwG = b.Status == BatchStatus.Closed ? b.FinalAbwG : ind.CurrentAbwG,
        FcrToDate = b.Status == BatchStatus.Closed ? b.FinalFcr : ind.FcrToDate,
        EstimatedSurvivalPct = b.Status == BatchStatus.Closed ? b.SurvivalRatePct : ind.EstimatedSurvivalPct,
    };

    private static IQueryable<PondBatch> Matching(IQueryable<PondBatch> query, string search)
    {
        var s = search?.Trim();
        return string.IsNullOrEmpty(s) ? query : query.Where(b => b.BatchCode.Contains(s) || b.Pond.PondCode.Contains(s) || b.Cycle.CycleCode.Contains(s));
    }

    public virtual async Task<List<BatchListDto>> GetAll(GetBatchesInput input)
    {
        var batches = await Matching(_batches.GetAll().AsNoTracking().Include(b => b.Pond).Include(b => b.Cycle), input.Search)
            .Where(b => !input.CycleId.HasValue || b.CycleId == input.CycleId)
            .Where(b => !input.PondId.HasValue || b.PondId == input.PondId)
            .Where(b => !input.Status.HasValue || b.Status == input.Status)
            .Where(b => !input.OpenOnly || b.Status != BatchStatus.Closed)
            .OrderBy(b => b.Status == BatchStatus.Closed).ThenBy(b => b.Pond.PondCode).ThenByDescending(b => b.StockedAtUtc)
            .ToListAsync();
        var facts = await _loader.LoadAsync(batches.Select(b => b.Id).ToList(), await Farm.GetTimeZoneAsync());
        var today = await Farm.GetTodayAsync();
        return batches.Select(b => MapList(b, b.Cycle?.CycleCode, IndicatorCalculator.Compute(facts.TryGetValue(b.Id, out var f) ? f : new BatchFacts(), today))).ToList();
    }

    private static readonly string[] BatchSortKeys = { "default", "batchCode", "pondCode", "cycleCode", "status", "stockingDate", "plCountStocked" };

    public virtual async Task<PagedResultDto<BatchListDto>> GetPage(GetBatchPageInput input)
    {
        var query = Matching(_batches.GetAll().AsNoTracking().Include(b => b.Pond).Include(b => b.Cycle), input.Search)
            .Where(b => !input.CycleId.HasValue || b.CycleId == input.CycleId)
            .Where(b => !input.PondId.HasValue || b.PondId == input.PondId)
            .Where(b => !input.Status.HasValue || b.Status == input.Status)
            .Where(b => !input.OpenOnly || b.Status != BatchStatus.Closed);
        var total = await query.CountAsync();
        var sort = SortSpec.Parse(input.Sorting, BatchSortKeys, "default", false);
        var ordered = sort.Key switch
        {
            "batchCode" => sort.Apply(query, b => b.BatchCode),
            "pondCode" => sort.Apply(query, b => b.Pond.PondCode).ThenBy(b => b.BatchCode),
            "cycleCode" => sort.Apply(query, b => b.Cycle.CycleCode).ThenBy(b => b.BatchCode),
            "status" => sort.Apply(query, b => b.Status).ThenBy(b => b.BatchCode),
            "stockingDate" => sort.Apply(query, b => b.StockingDate).ThenBy(b => b.BatchCode),
            "plCountStocked" => sort.Apply(query, b => b.PlCountStocked).ThenBy(b => b.BatchCode),
            _ => query.OrderBy(b => b.Status == BatchStatus.Closed).ThenBy(b => b.Pond.PondCode).ThenByDescending(b => b.StockedAtUtc),
        };
        var batches = await ordered.Skip(input.SkipCount).Take(input.MaxResultCount).ToListAsync();
        var facts = await _loader.LoadAsync(batches.Select(b => b.Id).ToList(), await Farm.GetTimeZoneAsync());
        var today = await Farm.GetTodayAsync();
        var items = batches.Select(b => MapList(b, b.Cycle?.CycleCode, IndicatorCalculator.Compute(facts.TryGetValue(b.Id, out var f) ? f : new BatchFacts(), today))).ToList();
        return new PagedResultDto<BatchListDto>(total, items);
    }

    public virtual async Task<List<StatusCountDto>> GetStatusCounts(GetBatchStatusCountsInput input)
    {
        var query = Matching(_batches.GetAll().AsNoTracking(), input.Search).Where(b => !input.OpenOnly || b.Status != BatchStatus.Closed);
        return await query.GroupBy(b => b.Status).Select(g => new StatusCountDto { Status = (int)g.Key, Count = g.Count() }).ToListAsync();
    }

    public virtual async Task<BatchDetailDto> Get(Guid id)
    {
        var b = await _batches.GetAll().AsNoTracking().Include(x => x.Pond).Include(x => x.Cycle).ThenInclude(c => c.FryDelivery).ThenInclude(d => d.Hatchery)
                    .FirstOrDefaultAsync(x => x.Id == id) ?? throw new UserFriendlyException("Batch not found.");
        var zone = await Farm.GetTimeZoneAsync();
        var facts = await _loader.LoadAsync(b.Id, zone);
        var ind = IndicatorCalculator.Compute(facts, await Farm.GetTodayAsync());
        var counts = await _logs.GetAll().Where(l => l.BatchId == id).GroupBy(l => l.LogType).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
        var hasFinal = await _harvests.GetAll().AnyAsync(h => h.BatchId == id && h.HarvestType == HarvestType.Total);
        var hasHarvest = await _harvests.GetAll().AnyAsync(h => h.BatchId == id);
        var failure = await _failures.GetAll().AsNoTracking().FirstOrDefaultAsync(f => f.BatchId == id);
        var names = await GetUserNamesAsync(new[] { b.ClosedByUserId });
        var isOwner = await IsGrantedAsync(PermissionNames.Production_Manage);
        var canHarvest = await IsGrantedAsync(PermissionNames.Harvests_Create);
        var delivery = b.Cycle?.FryDelivery;

        var list = MapList(b, b.Cycle?.CycleCode, ind);
        var dto = new BatchDetailDto
        {
            Id = list.Id, BatchCode = list.BatchCode, Letter = list.Letter, CycleId = list.CycleId, CycleCode = list.CycleCode, PondId = list.PondId, PondCode = list.PondCode,
            Status = list.Status, StockingDate = list.StockingDate, PlCountStocked = list.PlCountStocked, Doc = list.Doc, CurrentAbwG = list.CurrentAbwG,
            FcrToDate = list.FcrToDate, EstimatedSurvivalPct = list.EstimatedSurvivalPct,
            PondAreaM2AtStocking = b.PondAreaM2AtStocking, StockedAtUtc = b.StockedAtUtc, TransportMode = b.TransportMode, TimeProcessedUtc = b.TimeProcessedUtc,
            AcclimationMinutes = b.AcclimationMinutes, InitialSurvivalEstimatePct = b.InitialSurvivalEstimatePct,
            FryDeliveryId = b.Cycle?.FryDeliveryId ?? Guid.Empty, HatcheryName = delivery?.Hatchery?.Name, HatcheryBatchNumber = delivery?.HatcheryBatchNumber,
            PlAgeDays = delivery?.PlAgeDays,
            HatcheryCertificationStatusAtStocking = b.HatcheryCertificationStatusAtStocking, StockingDensityPerM2 = b.StockingDensityPerM2,
            DeadAtStocking = b.DeadAtStocking, PlHealthCertificateNo = delivery?.PlHealthCertificateNo, SfscNo = delivery?.SfscNo,
            EarliestSafeHarvestDate = await _withdrawal.EarliestSafeHarvestDateAsync(b.Id, zone),
            Reopenings = await ReopeningsAsync(b.Id),
            FarmingSystemAtStocking = b.FarmingSystemAtStocking, ConstructionTypeAtStocking = b.ConstructionTypeAtStocking, DensityFlag = b.DensityFlag,
            FailureReason = failure?.Reason, FailureDate = failure?.FailedOn, EstimatedLossPhp = failure?.EstimatedLossPhp,
            FinalAbwG = b.FinalAbwG, FinalFcr = b.FinalFcr, SurvivalRatePct = b.SurvivalRatePct, TotalHarvestKg = b.TotalHarvestKg, TotalFeedKg = b.TotalFeedKg,
            CultureDays = b.CultureDays, ClosedAtUtc = b.ClosedAtUtc, ClosedBy = NameOf(names, b.ClosedByUserId), RowVersion = Concurrency.Encode(b.RowVersion),
            Indicators = IndicatorsDto.From(ind),
            LogCounts = counts.ToDictionary(c => c.Key.ToString(), c => c.N),
            HasFinalHarvest = hasFinal,
            HasRecords = counts.Count > 0 || hasHarvest,
            AllowedLogTypes = Enum.GetValues<LogType>().Where(b.AllowsNewLog).Select(t => t.ToString()).ToList(),
            IsReadOnly = b.Status == BatchStatus.Closed,
        };

        dto.CanDeclareFailure = isOwner && b.Status == BatchStatus.Stocked;
        dto.CanClose = isOwner && (b.Status == BatchStatus.Harvested || b.Status == BatchStatus.Failed);
        dto.CanAddHarvest = canHarvest && (b.Status == BatchStatus.Stocked || b.Status == BatchStatus.Failed);
        if (b.Status == BatchStatus.Closed && isOwner)
        {
            dto.ReopenBlockedReason = await ReopenBlockedReasonAsync(b);
            dto.CanReopen = dto.ReopenBlockedReason == null;
        }

        return dto;
    }

    private async Task<string> ReopenBlockedReasonAsync(PondBatch b)
    {
        if (b.Cycle?.Status == CycleStatus.Closed || await _cycles.GetAll().AnyAsync(c => c.Id == b.CycleId && c.Status == CycleStatus.Closed))
        {
            return $"Cycle {b.Cycle?.CycleCode} is Closed. Reopen the cycle first.";
        }

        var other = await _batches.GetAll().AsNoTracking().Where(x => x.PondId == b.PondId && x.Id != b.Id && x.Status != BatchStatus.Closed)
            .Select(x => x.BatchCode).FirstOrDefaultAsync();
        return other != null ? $"Pond {b.Pond?.PondCode} already has open batch {other}; this batch cannot be reopened while it is open." : null;
    }

    private async Task<List<ReopenEventDto>> ReopeningsAsync(Guid batchId)
    {
        var list = await _reopenings.GetAll().AsNoTracking().Where(r => r.EntityType == ReopenedEntityType.Batch && r.EntityId == batchId)
            .OrderByDescending(r => r.ReopenedAtUtc).ToListAsync();
        var names = await GetUserNamesAsync(list.Select(r => (long?)r.ReopenedByUserId));
        return list.Select(r => new ReopenEventDto { ReopenedAtUtc = r.ReopenedAtUtc, ReopenedBy = NameOf(names, r.ReopenedByUserId), Reason = r.Reason }).ToList();
    }

    [AbpAuthorize(PermissionNames.Production_Manage)]
    public virtual async Task Reopen(ReopenInput input)
    {
        var batch = await _batches.GetAll().Include(b => b.Pond).Include(b => b.Cycle).FirstOrDefaultAsync(b => b.Id == input.Id)
                    ?? throw new UserFriendlyException("Batch not found.");
        Concurrency.Check(batch.RowVersion, input.RowVersion);
        if (batch.Status != BatchStatus.Closed)
        {
            throw new UserFriendlyException("Only a Closed batch can be reopened.");
        }

        var reason = input.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new UserFriendlyException("Give the reason for reopening.");
        }

        if (await ReopenBlockedReasonAsync(batch) is { } blocked)
        {
            throw new UserFriendlyException(blocked);
        }

        batch.Status = await _failures.GetAll().AnyAsync(f => f.BatchId == batch.Id) ? BatchStatus.Failed : BatchStatus.Harvested;
        batch.ClosedAtUtc = null;
        batch.ClosedByUserId = null;
        batch.FinalAbwG = null;
        batch.FinalFcr = null;
        batch.SurvivalRatePct = null;
        batch.TotalHarvestKg = null;
        batch.TotalFeedKg = null;
        batch.CultureDays = null;
        await _reopenings.InsertAsync(new ReopenEvent
        {
            EntityType = ReopenedEntityType.Batch, EntityId = batch.Id, ReopenedAtUtc = DateTime.UtcNow, ReopenedByUserId = AbpSession.GetUserId(), Reason = reason,
        });
    }

    [AbpAuthorize(PermissionNames.Production_Manage)]
    public virtual async Task DeclareFailure(FailureInput input)
    {
        var batch = await _batches.GetAsync(input.Id);
        Concurrency.Check(batch.RowVersion, input.RowVersion);
        if (batch.Status != BatchStatus.Stocked)
        {
            throw new UserFriendlyException("Only a Stocked batch can be declared failed.");
        }

        var today = await Farm.GetTodayAsync();
        if (input.FailureDate > today || (batch.StockingDate.HasValue && input.FailureDate < batch.StockingDate.Value))
        {
            throw new UserFriendlyException("The failure date must be between the stocking date and today.");
        }

        batch.Status = BatchStatus.Failed;
        await _failures.InsertAsync(new BatchFailure
        {
            BatchId = batch.Id, FailedOn = input.FailureDate, Reason = input.Reason.Trim(), EstimatedLossPhp = input.EstimatedLossPhp,
        });
    }

    public virtual async Task<CloseCheckDto> GetCloseCheck(Guid id)
    {
        var batch = await _batches.GetAll().AsNoTracking().FirstOrDefaultAsync(b => b.Id == id) ?? throw new UserFriendlyException("Batch not found.");
        var result = new CloseCheckDto { BatchId = id };
        await FillFlaggedLotsAsync(result, id);
        if (!batch.StockingDate.HasValue)
        {
            await FillUnaccountedAsync(result, id);
            return result;
        }

        var zone = await Farm.GetTimeZoneAsync();
        var until = await _daily.ExpectedUntilAsync(batch, FarmClock.Today(zone), zone);
        if (until < batch.StockingDate.Value)
        {
            return result;
        }

        var days = (await _daily.LoadAsync(new[] { batch }, batch.StockingDate.Value, until, zone))[batch.Id];
        result.From = batch.StockingDate;
        result.To = until;
        result.BlankFeedingSlots = days.Count(d => d.Am == DailyCellStatus.Blank) + days.Count(d => d.Pm == DailyCellStatus.Blank);
        result.NotFedSlots = days.Count(d => d.Am == DailyCellStatus.NotFed) + days.Count(d => d.Pm == DailyCellStatus.NotFed);
        result.BlankWaterDays = days.Count(d => d.Water == DailyCellStatus.Blank);
        var blanks = days.Where(d => d.HasBlank).ToList();
        result.BlankDaysTotal = blanks.Count;
        await FillUnaccountedAsync(result, batch.Id);
        result.BlankDays = blanks.Take(60).Select(d => new BlankDayDto { Date = d.Date, Missing = d.DescribeBlanks() }).ToList();
        return result;
    }

    public virtual async Task<BatchLogCalendarDto> GetLogCalendar(Guid id)
    {
        var batch = await _batches.GetAll().AsNoTracking().FirstOrDefaultAsync(b => b.Id == id) ?? throw new UserFriendlyException("Batch not found.");
        var result = new BatchLogCalendarDto { BatchId = id };
        if (!batch.StockingDate.HasValue)
        {
            return result;
        }

        var zone = await Farm.GetTimeZoneAsync();
        var until = await _daily.ExpectedUntilAsync(batch, FarmClock.Today(zone), zone);
        if (until < batch.StockingDate.Value)
        {
            return result;
        }

        var days = (await _daily.LoadAsync(new[] { batch }, batch.StockingDate.Value, until, zone))[batch.Id];
        var events = await _logs.GetAll().AsNoTracking()
            .Where(l => l.BatchId == id && (l.LogType == LogType.Mortality || l.LogType == LogType.GrowthSampling || l.LogType == LogType.ChemicalApplication))
            .Select(l => new { l.LogType, l.ObservedAtUtc }).ToListAsync();
        var eventDays = events.Select(e => (e.LogType, Day: FarmClock.LocalDate(e.ObservedAtUtc, zone))).ToHashSet();

        result.From = batch.StockingDate;
        result.To = until;
        result.Days = days.Select(d => new BatchLogCalendarDayDto
        {
            Date = d.Date, Am = d.Am, Pm = d.Pm, Water = d.Water,
            HasMortality = eventDays.Contains((LogType.Mortality, d.Date)),
            HasGrowthSampling = eventDays.Contains((LogType.GrowthSampling, d.Date)),
            HasChemical = eventDays.Contains((LogType.ChemicalApplication, d.Date)),
        }).ToList();
        return result;
    }

    private async Task FillFlaggedLotsAsync(CloseCheckDto result, Guid batchId)
    {
        var moves = await _movements.GetAll().AsNoTracking().Include(m => m.Lot).ThenInclude(l => l.Item)
            .Where(m => m.BatchId == batchId && m.Lot.NeedsReconciliation).ToListAsync();
        result.FlaggedLots = moves.GroupBy(m => m.LotId).Select(g => g.First().Lot)
            .Select(l => new FlaggedLotDto { LotNumber = l.LotNumber, Product = l.Item.DisplayName }).OrderBy(l => l.Product).ThenBy(l => l.LotNumber).ToList();
    }

    private async Task FillUnaccountedAsync(CloseCheckDto result, Guid batchId)
    {
        var harvests = await _harvests.GetAll().AsNoTracking().Include(h => h.GradeLines).Include(h => h.Disposals).Where(h => h.BatchId == batchId).ToListAsync();
        var lineIds = harvests.SelectMany(h => h.GradeLines).Select(g => g.Id).ToList();
        var sold = await (from l in _saleLines.GetAll()
                          join s in _sales.GetAll() on l.SaleId equals s.Id
                          where lineIds.Contains(l.HarvestGradeLineId)
                          select (decimal?)l.Kg).SumAsync() ?? 0m;
        result.HarvestedKg = harvests.Sum(h => h.TotalKg);
        result.SoldKg = sold;
        result.NotSoldKg = harvests.SelectMany(h => h.Disposals).Sum(d => d.Kg);
        result.UnaccountedKg = Math.Max(0, result.HarvestedKg - result.SoldKg - result.NotSoldKg);
    }

    [AbpAuthorize(PermissionNames.Production_Manage)]
    public virtual async Task<IndicatorsDto> Close(CloseBatchInput input)
    {
        var batch = await _batches.GetAsync(input.Id);
        Concurrency.Check(batch.RowVersion, input.RowVersion);
        if (batch.Status is not (BatchStatus.Harvested or BatchStatus.Failed))
        {
            throw new UserFriendlyException("Only a Harvested or Failed batch can be closed.");
        }

        if (!input.ConfirmReadOnly)
        {
            throw new UserFriendlyException("Confirm that the batch's records will become read-only.");
        }

        var facts = await _loader.LoadAsync(batch.Id, await Farm.GetTimeZoneAsync());
        var ind = IndicatorCalculator.Compute(facts, await Farm.GetTodayAsync());
        var failure = await _failures.FirstOrDefaultAsync(f => f.BatchId == batch.Id);
        batch.FinalAbwG = ind.FinalAbwG;
        batch.FinalFcr = ind.FinalFcr;
        batch.SurvivalRatePct = ind.SurvivalRatePct ?? (facts.PlCountStocked > 0 ? 0m : null);
        batch.TotalHarvestKg = ind.TotalHarvestKg;
        batch.TotalFeedKg = ind.CumulativeFeedKg;
        batch.CultureDays = ind.CultureDays ?? (failure != null && batch.StockingDate.HasValue
            ? FarmClock.DaysBetween(batch.StockingDate.Value, failure.FailedOn)
            : null);
        batch.Status = BatchStatus.Closed;
        batch.ClosedAtUtc = DateTime.UtcNow;
        batch.ClosedByUserId = AbpSession.GetUserId();
        return IndicatorsDto.From(ind);
    }
}
