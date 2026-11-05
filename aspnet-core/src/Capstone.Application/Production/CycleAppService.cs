using Abp.Application.Services.Dto;
using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.Runtime.Session;
using Abp.UI;
using Capstone.Alerts;
using Capstone.Authorization;
using Capstone.Common;
using Capstone.Domain;
using Capstone.Ponds;
using Capstone.Production.Dto;
using Capstone.ReferenceData;
using Capstone.Resources;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Capstone.Production;

public class GetCyclesInput
{
    public CycleStatus? Status { get; set; }
}

public class GetCyclePageInput : Capstone.Common.PagedInput
{
    public CycleStatus? Status { get; set; }
}

[AbpAuthorize(PermissionNames.Production)]
public class CycleAppService : CapstoneAppServiceBase
{
    private readonly IRepository<ProductionCycle, Guid> _cycles;
    private readonly IRepository<PondBatch, Guid> _batches;
    private readonly IRepository<FryDelivery, Guid> _deliveries;
    private readonly IRepository<FryDeliveryStatusChange, Guid> _deliveryChanges;
    private readonly IRepository<Pond, Guid> _ponds;
    private readonly IRepository<PondPreparationRound, Guid> _rounds;
    private readonly IRepository<HatcheryCertificate, Guid> _certificates;
    private readonly IRepository<ProductionLog, Guid> _logs;
    private readonly IRepository<WaterQualityMeasurement, Guid> _measurements;
    private readonly BatchDataLoader _loader;
    private readonly WaterQualityEvaluator _waterQuality;
    private readonly FarmAlertPublisher _alerts;
    private readonly INumberSequenceGenerator _sequences;
    private readonly IRowLocker _rowLocker;
    private readonly DensityEvaluator _density;
    private readonly IRepository<ReopenEvent, Guid> _reopenings;
    private readonly IRepository<FryDeliveryShortfall, Guid> _shortfalls;
    private readonly IRepository<BatchFailure, Guid> _failures;

    public CycleAppService(IRepository<ProductionCycle, Guid> cycles, IRepository<PondBatch, Guid> batches, IRepository<FryDelivery, Guid> deliveries,
        IRepository<FryDeliveryStatusChange, Guid> deliveryChanges, IRepository<Pond, Guid> ponds, IRepository<PondPreparationRound, Guid> rounds,
        IRepository<HatcheryCertificate, Guid> certificates, IRepository<ProductionLog, Guid> logs, IRepository<WaterQualityMeasurement, Guid> measurements,
        BatchDataLoader loader, WaterQualityEvaluator waterQuality, FarmAlertPublisher alerts, INumberSequenceGenerator sequences, IRowLocker rowLocker,
        DensityEvaluator density, IRepository<ReopenEvent, Guid> reopenings, IRepository<FryDeliveryShortfall, Guid> shortfalls,
        IRepository<BatchFailure, Guid> failures)
    {
        _failures = failures;
        _reopenings = reopenings;
        _shortfalls = shortfalls;
        _cycles = cycles;
        _batches = batches;
        _deliveries = deliveries;
        _deliveryChanges = deliveryChanges;
        _ponds = ponds;
        _rounds = rounds;
        _certificates = certificates;
        _logs = logs;
        _measurements = measurements;
        _loader = loader;
        _waterQuality = waterQuality;
        _alerts = alerts;
        _sequences = sequences;
        _rowLocker = rowLocker;
        _density = density;
    }

    public virtual async Task<List<CycleListDto>> GetAll(GetCyclesInput input)
    {
        var cycles = await _cycles.GetAll().AsNoTracking().Include(c => c.FryDelivery).ThenInclude(d => d.Hatchery)
            .Where(c => !input.Status.HasValue || c.Status == input.Status).ToListAsync();
        var stats = await _batches.GetAll().GroupBy(b => b.CycleId)
            .Select(g => new { g.Key, N = g.Count(), Open = g.Count(b => b.Status != BatchStatus.Closed), Pl = g.Sum(b => b.PlCountStocked ?? 0), Start = g.Min(b => b.StockingDate) })
            .ToListAsync();
        return cycles.Select(c =>
        {
            var s = stats.FirstOrDefault(x => x.Key == c.Id);
            var dto = new CycleListDto();
            Fill(dto, c, s?.N ?? 0, s?.Open ?? 0, s?.Pl ?? 0, s?.Start);
            return dto;
        })
        .OrderBy(c => c.Status == CycleStatus.Closed).ThenByDescending(c => c.StartDate).ToList();
    }

    private static readonly string[] CycleSortKeys = { "default", "cycleCode", "status", "startDate", "plReceived", "plStocked", "hatcheryName" };

    public virtual async Task<PagedResultDto<CycleListDto>> GetPage(GetCyclePageInput input)
    {
        var all = (await GetAll(new GetCyclesInput { Status = input.Status })).Where(c => CycleMatches(c, input.Search)).ToList();
        var sort = SortSpec.Parse(input.Sorting, CycleSortKeys, "default", false);
        IEnumerable<CycleListDto> ordered = sort.Key switch
        {
            "cycleCode" => sort.Apply(all, c => c.CycleCode),
            "status" => sort.Apply(all, c => (int)c.Status),
            "startDate" => sort.Apply(all, c => c.StartDate ?? DateOnly.MinValue),
            "plReceived" => sort.Apply(all, c => c.PlReceived),
            "plStocked" => sort.Apply(all, c => c.PlStocked),
            "hatcheryName" => sort.Apply(all, c => c.HatcheryName ?? string.Empty),
            _ => all,
        };
        return ordered.ToList().ToPage(input);
    }

    private static bool CycleMatches(CycleListDto c, string search) =>
        SearchText.Matches(search, c.CycleCode, c.HatcheryName, c.HatcheryShortCode, c.HatcheryBatchNumber);

    public virtual async Task<List<StatusCountDto>> GetStatusCounts(GetCyclePageInput input) =>
        (await GetAll(new GetCyclesInput())).Where(c => CycleMatches(c, input.Search))
            .GroupBy(c => c.Status).Select(g => new StatusCountDto { Status = (int)g.Key, Count = g.Count() }).ToList();

    public virtual async Task<CycleDetailDto> Get(Guid id)
    {
        var c = await _cycles.GetAll().AsNoTracking().Include(x => x.FryDelivery).ThenInclude(d => d.Hatchery).FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new UserFriendlyException("Production cycle not found.");
        var batches = await _batches.GetAll().AsNoTracking().Include(b => b.Pond).Where(b => b.CycleId == id).OrderBy(b => b.Letter).ToListAsync();
        var zone = await Farm.GetTimeZoneAsync();
        var today = await Farm.GetTodayAsync();
        var facts = await _loader.LoadAsync(batches.Select(b => b.Id).ToList(), zone);
        var names = await GetUserNamesAsync(new[] { c.ClosedByUserId });

        var dto = new CycleDetailDto
        {
            DeliveryDate = c.FryDelivery.DeliveryDate, PlAgeDays = c.FryDelivery.PlAgeDays,
            ClosedAtUtc = c.ClosedAtUtc, ClosedBy = NameOf(names, c.ClosedByUserId), RowVersion = Concurrency.Encode(c.RowVersion),
            DeliveryCostPhp = c.FryDelivery.CostPhp, IsStockingFinished = c.FryDelivery.IsStockingFinished,
            PlNotStocked = await _shortfalls.GetAll().Where(s => s.DeliveryId == c.FryDeliveryId).SumAsync(s => (long?)s.Count) ?? 0,
        };
        dto.Reopenings = await ReopeningsAsync(ReopenedEntityType.Cycle, c.Id);
        Fill(dto, c, batches.Count, batches.Count(b => b.Status != BatchStatus.Closed), batches.Sum(b => b.PlCountStocked ?? 0), batches.Min(b => b.StockingDate));
        foreach (var b in batches)
        {
            var ind = IndicatorCalculator.Compute(facts.TryGetValue(b.Id, out var f) ? f : new BatchFacts(), today);
            dto.TotalHarvestKg += ind.TotalHarvestKg;
            dto.TotalFeedKg += ind.CumulativeFeedKg;
            dto.Batches.Add(BatchAppService.MapList(b, c.CycleCode, ind));
        }

        dto.PlLeft -= dto.PlNotStocked;
        var isOwner = await IsGrantedAsync(PermissionNames.Production_Manage);
        dto.CanCreateBatch = c.Status == CycleStatus.Active && dto.PlLeft > 0 && !c.FryDelivery.IsStockingFinished && await IsGrantedAsync(PermissionNames.Production_Stock);
        dto.CanClose = c.Status == CycleStatus.Active && batches.All(b => b.Status == BatchStatus.Closed) && isOwner;
        dto.CanReopen = c.Status == CycleStatus.Closed && isOwner;
        dto.SuggestedOutcome = await SuggestOutcomeAsync(c.Id);
        return dto;
    }

    private async Task<CycleOutcome> SuggestOutcomeAsync(Guid cycleId)
    {
        var ids = await _batches.GetAll().Where(b => b.CycleId == cycleId).Select(b => b.Id).ToListAsync();
        if (ids.Count == 0)
        {
            return CycleOutcome.Completed;
        }

        var failed = await _failures.GetAll().CountAsync(f => ids.Contains(f.BatchId));
        return failed == ids.Count ? CycleOutcome.Failed : CycleOutcome.Completed;
    }

    internal async Task<List<ReopenEventDto>> ReopeningsAsync(ReopenedEntityType type, Guid id)
    {
        var list = await _reopenings.GetAll().AsNoTracking().Where(r => r.EntityType == type && r.EntityId == id).OrderByDescending(r => r.ReopenedAtUtc).ToListAsync();
        var names = await GetUserNamesAsync(list.Select(r => (long?)r.ReopenedByUserId));
        return list.Select(r => new ReopenEventDto { ReopenedAtUtc = r.ReopenedAtUtc, ReopenedBy = NameOf(names, r.ReopenedByUserId), Reason = r.Reason }).ToList();
    }

    [AbpAuthorize(PermissionNames.Production_Stock)]
    public virtual async Task<StockingOptionsDto> GetStockingOptions(GetStockingOptionsInput input)
    {
        var result = new StockingOptionsDto();
        var today = await Farm.GetTodayAsync();
        if (input.CycleId.HasValue)
        {
            var cycle = await _cycles.GetAll().AsNoTracking().Include(c => c.FryDelivery).ThenInclude(d => d.Hatchery).FirstOrDefaultAsync(c => c.Id == input.CycleId)
                        ?? throw new UserFriendlyException("Production cycle not found.");
            if (cycle.Status != CycleStatus.Active)
            {
                throw new UserFriendlyException("Batches can be added only to an Active cycle.");
            }

            if (cycle.FryDelivery.IsStockingFinished)
            {
                throw new UserFriendlyException("Stocking from this delivery is finished; no more batches can be added.");
            }

            result.CycleId = cycle.Id;
            result.CycleCode = cycle.CycleCode;
            result.Deliveries.Add(MapDelivery(cycle.FryDelivery, await PlStockedAsync(cycle.Id), today));
        }
        else
        {
            var usedIds = await _cycles.GetAll().Select(c => c.FryDeliveryId).ToListAsync();
            var candidates = await _deliveries.GetAll().AsNoTracking().Include(d => d.Hatchery)
                .Where(d => !usedIds.Contains(d.Id) && d.StockingFinishedAtUtc == null)
                .OrderByDescending(d => d.DeliveryDate).ThenByDescending(d => d.CreationTime).ToListAsync();
            foreach (var d in candidates)
            {
                if (await CurrentStatusAsync(d.Id) == FryDeliveryStatus.Received)
                {
                    result.Deliveries.Add(MapDelivery(d, 0, today));
                }
            }
        }

        var farmSystem = (await Farm.GetProfileAsync()).FarmingSystem;
        var prepared = await _rounds.GetAll().AsNoTracking().Where(r => r.UsedAtUtc == null && r.PreparedAtUtc != null).ToListAsync();
        var pondIds = prepared.Select(r => r.PondId).ToList();
        var ponds = await _ponds.GetAll().AsNoTracking().Include(p => p.Prefix).Where(p => pondIds.Contains(p.Id)).OrderBy(p => p.PondCode).ToListAsync();
        var openBatches = await _batches.GetAll().AsNoTracking().Where(b => pondIds.Contains(b.PondId) && b.Status != BatchStatus.Closed)
            .Select(b => new { b.PondId, b.BatchCode, b.Status }).ToListAsync();
        foreach (var p in ponds)
        {
            var dto = new StockingPondDto
            {
                Id = p.Id, PondCode = p.PondCode, Name = PondNaming.DisplayName(p.Prefix?.Name, p.Number), AreaM2 = p.AreaM2, AverageDepthM = p.AverageDepthM,
                FarmingSystem = p.EffectiveFarmingSystem(farmSystem), PreparedAtUtc = prepared.First(r => r.PondId == p.Id).PreparedAtUtc,
                ConstructionType = p.ConstructionType,
            };
            var (band, isFarmValue) = await _density.EffectiveBandAsync(dto.FarmingSystem, p.ConstructionType);
            dto.BandMinPlM2 = band?.MinPlM2;
            dto.BandMaxPlM2 = band?.MaxPlM2;
            dto.BandSource = isFarmValue ? "Farm Standards" : band?.Source;
            var open = openBatches.FirstOrDefault(b => b.PondId == p.Id);
            dto.BlockedReason = p.IsArchived ? "The pond is archived."
                : p.Purpose != PondPurpose.GrowOut ? "Not a grow-out pond; batches are stocked only in grow-out ponds."
                : p.ManualStatus != PondManualStatus.Active ? $"The pond is {p.ManualStatus}."
                : open != null ? $"Batch {open.BatchCode} in this pond is {open.Status} and not yet closed. The Owner must close it first."
                : null;
            (dto.BlockedReason == null ? result.Ponds : result.BlockedPonds).Add(dto);
        }

        return result;
    }

    [AbpAuthorize(PermissionNames.Production_Stock)]
    public virtual async Task<CreateBatchesResultDto> CreateBatches(CreateBatchesInput input)
    {
        if (input.Batches == null || input.Batches.Count == 0)
        {
            throw new UserFriendlyException("Choose at least one pond.");
        }

        if (input.Batches.Select(b => b.PondId).Distinct().Count() != input.Batches.Count)
        {
            throw new UserFriendlyException("Each pond can be chosen only once.");
        }

        var requestIds = input.Batches.Select((_, i) => input.ClientRequestId.HasValue ? DeriveRequestId(input.ClientRequestId.Value, i) : Guid.NewGuid()).ToList();
        if (input.ClientRequestId.HasValue)
        {
            var existing = await _logs.GetAll().AsNoTracking().Where(l => requestIds.Contains(l.ClientRequestId)).Select(l => l.BatchId).ToListAsync();
            if (existing.Count > 0)
            {
                return await DuplicateResultAsync(existing);
            }
        }

        ProductionCycle cycle = null;
        FryDelivery delivery;
        if (input.CycleId.HasValue)
        {
            cycle = await _cycles.GetAll().Include(c => c.FryDelivery).ThenInclude(d => d.Hatchery).FirstOrDefaultAsync(c => c.Id == input.CycleId)
                    ?? throw new UserFriendlyException("Production cycle not found.");
            if (cycle.Status != CycleStatus.Active)
            {
                throw new UserFriendlyException("Batches can be added only to an Active cycle.");
            }

            delivery = cycle.FryDelivery;
        }
        else
        {
            if (!input.FryDeliveryId.HasValue)
            {
                throw new UserFriendlyException("Choose the fry delivery to stock from.");
            }

            delivery = await _deliveries.GetAll().Include(d => d.Hatchery).FirstOrDefaultAsync(d => d.Id == input.FryDeliveryId)
                       ?? throw new UserFriendlyException("Fry delivery not found.");
        }

        await _rowLocker.LockAsync(RowLockTarget.FryDelivery, delivery.Id);
        if (cycle == null)
        {
            if (await _cycles.GetAll().AnyAsync(c => c.FryDeliveryId == delivery.Id))
            {
                throw new UserFriendlyException("This delivery already started a production cycle. Add batches from that cycle instead.");
            }

            if (await CurrentStatusAsync(delivery.Id) != FryDeliveryStatus.Received)
            {
                throw new UserFriendlyException("Only a Received delivery can be stocked. Update the delivery's status first.");
            }
        }

        if (delivery.IsStockingFinished)
        {
            throw new UserFriendlyException("Stocking from this delivery is finished; it cannot be stocked from again.");
        }

        var plStocked = cycle == null ? 0 : await PlStockedAsync(cycle.Id);
        var plLeft = delivery.PlReceived - plStocked;
        var requested = input.Batches.Sum(b => b.PlCount);
        if (requested > plLeft)
        {
            throw new UserFriendlyException($"The ponds are allocated {requested:N0} PL but only {plLeft:N0} PL are left in this delivery.");
        }

        var zone = await Farm.GetTimeZoneAsync();
        var profile = await Farm.GetProfileAsync();
        var result = new CreateBatchesResultDto();

        var plans = new List<(BatchStockingInput Input, Pond Pond, PondPreparationRound Round, DateTime StockedAtUtc, DateTime? ProcessedUtc, List<EvaluatedReading> Readings)>();
        foreach (var b in input.Batches)
        {
            var pond = await _ponds.GetAll().Include(p => p.Prefix).FirstOrDefaultAsync(p => p.Id == b.PondId) ?? throw new UserFriendlyException("Pond not found.");
            if (pond.IsArchived)
            {
                throw new UserFriendlyException($"Pond {pond.PondCode} is archived. Restore it before stocking.");
            }

            if (pond.Purpose != PondPurpose.GrowOut)
            {
                throw new UserFriendlyException($"Pond {pond.PondCode} is not a grow-out pond.");
            }

            if (pond.ManualStatus != PondManualStatus.Active)
            {
                throw new UserFriendlyException($"Pond {pond.PondCode} is {pond.ManualStatus}.");
            }

            if (await _batches.GetAll().AnyAsync(x => x.PondId == pond.Id && x.Status != BatchStatus.Closed))
            {
                throw new UserFriendlyException($"Pond {pond.PondCode} still has a batch that is not closed. The Owner must close it first.");
            }

            var round = await _rounds.FirstOrDefaultAsync(r => r.PondId == pond.Id && r.UsedAtUtc == null);
            if (round?.PreparedAtUtc == null)
            {
                throw new UserFriendlyException($"Pond {pond.PondCode} is not marked Prepared.");
            }

            if (!Enum.IsDefined(b.TransportMode))
            {
                throw new UserFriendlyException("Choose the transport mode.");
            }

            var stockedAtUtc = FarmClock.ToUtc(b.StockedAt);
            if (stockedAtUtc > DateTime.UtcNow.AddMinutes(5))
            {
                throw new UserFriendlyException($"{pond.PondCode}: the stocking date and time cannot be in the future.");
            }

            var stockingDay = FarmClock.LocalDate(stockedAtUtc, zone);
            if (stockingDay < delivery.DeliveryDate)
            {
                throw new UserFriendlyException($"{pond.PondCode}: stocking cannot be before the delivery was received ({delivery.DeliveryDate:yyyy-MM-dd}).");
            }

            if (b.DeadAtStocking is < 0)
            {
                throw new UserFriendlyException($"{pond.PondCode}: dead at stocking cannot be negative.");
            }

            DateTime? processedUtc = b.TimeProcessed.HasValue ? FarmClock.ToUtc(b.TimeProcessed.Value) : null;
            if (processedUtc > stockedAtUtc)
            {
                throw new UserFriendlyException($"{pond.PondCode}: the time processed cannot be after the stocking time.");
            }

            var readings = await _waterQuality.EvaluateAsync(b.Readings.Select(r => (r.ParameterCode, r.Value)), requireStockingSet: true);
            plans.Add((b, pond, round, stockedAtUtc, processedUtc, readings));
        }

        if (cycle == null)
        {
            var first = FarmClock.LocalDate(plans.Min(p => p.StockedAtUtc), zone);
            var scope = $"{first:yyyy-MM}-{delivery.Hatchery.ShortCode}";
            var seq = await _sequences.NextAsync(NumberSequenceType.CycleCode, scope);
            cycle = new ProductionCycle { CycleCode = $"{scope}-{seq:00}", Status = CycleStatus.Active, FryDeliveryId = delivery.Id };
            cycle.Id = await _cycles.InsertAndGetIdAsync(cycle);
            result.CycleCreated = true;
        }

        var usedLetters = await _batches.GetAll().IgnoreQueryFilters().Where(x => x.CycleId == cycle.Id && x.TenantId == Farm.TenantId).Select(x => x.Letter).ToListAsync();
        var certificates = await _certificates.GetAll().AsNoTracking().Where(c => c.HatcheryId == delivery.HatcheryId).ToListAsync();
        for (var i = 0; i < plans.Count; i++)
        {
            var (b, pond, round, stockedAtUtc, processedUtc, readings) = plans[i];
            var letter = NextLetter(usedLetters);
            usedLetters.Add(letter);
            var stockingDate = FarmClock.LocalDate(stockedAtUtc, zone);
            var batch = new PondBatch
            {
                CycleId = cycle.Id, PondId = pond.Id, PreparationRoundId = round.Id, Letter = letter, BatchCode = $"{cycle.CycleCode}-{letter}",
                Status = BatchStatus.Stocked, StockedAtUtc = stockedAtUtc, StockingDate = stockingDate, PlCountStocked = b.PlCount,
                PondAreaM2AtStocking = pond.AreaM2, TransportMode = b.TransportMode, TimeProcessedUtc = processedUtc,
                AcclimationMinutes = b.AcclimationMinutes, InitialSurvivalEstimatePct = b.InitialSurvivalEstimatePct, DeadAtStocking = b.DeadAtStocking,
                FarmingSystemAtStocking = pond.EffectiveFarmingSystem(profile.FarmingSystem), ConstructionTypeAtStocking = pond.ConstructionType,
            };
            var judgement = await _density.JudgeAsync(batch.FarmingSystemAtStocking.Value, pond.ConstructionType, batch.StockingDensityPerM2);
            batch.DensityFlag = judgement.Flag;
            var certificate = HatcheryCertificate.CurrentHealthCertificate(certificates, stockingDate);
            batch.HatcheryCertificationStatusAtStocking = delivery.Hatchery.DescribeCertification(certificate, stockingDate);
            batch.Id = await _batches.InsertAndGetIdAsync(batch);
            round.UsedAtUtc = stockedAtUtc;

            var log = new ProductionLog
            {
                Id = Guid.NewGuid(), BatchId = batch.Id, PondId = pond.Id, LogType = LogType.PreStockingWaterQuality, ObservedAtUtc = stockedAtUtc,
                Notes = "Pre-stocking water quality", ClientRequestId = requestIds[i],
            };
            await _logs.InsertAsync(log);
            foreach (var r in readings)
            {
                await _measurements.InsertAsync(new WaterQualityMeasurement
                {
                    Id = Guid.NewGuid(), LogId = log.Id, ParameterCode = r.Parameter.Id, Value = r.Value, UnitCode = r.Parameter.UnitCode, IsOutOfRange = r.IsOutOfRange,
                    Side = r.Parameter.SideOf(r.Value),
                });
                if (r.IsOutOfRange)
                {
                    result.Warnings.Add(new WarningDto
                    {
                        Kind = "WaterQuality",
                        Message = $"{pond.PondCode}: {r.Parameter.Name} {r.Value:0.###} {r.Parameter.UnitCode} is outside the optimal range ({r.Parameter.DescribeOptimalRange()})."
                                  + (r.Parameter.RecommendationFor(r.Value) is { } advice ? $" Recommendation: {advice}" : string.Empty),
                    });
                }
            }

            if (certificate != null && certificate.IsExpiredOn(stockingDate))
            {
                result.Warnings.Add(new WarningDto { Kind = "Hatchery", Message = $"{pond.PondCode}: the hatchery's health certificate had expired on the stocking date." });
            }

            if (processedUtc.HasValue && stockedAtUtc - processedUtc.Value > TimeSpan.FromHours(24))
            {
                result.Warnings.Add(new WarningDto { Kind = "Transport", Message = $"{pond.PondCode}: stocked more than 24 hours after the fry were processed." });
            }

            if (b.DeadAtStocking > b.PlCount)
            {
                result.Warnings.Add(new WarningDto { Kind = "DeadAtStocking", Message = $"{pond.PondCode}: dead at stocking ({b.DeadAtStocking:N0}) is more than the stocking count ({b.PlCount:N0}). Check both numbers." });
            }

            var density = batch.StockingDensityPerM2;
            if (judgement.Warning != null)
            {
                result.Warnings.Add(new WarningDto { Kind = "Density", Message = $"{pond.PondCode}: {judgement.Warning}" });
            }

            result.Batches.Add(new CreatedBatchDto { Id = batch.Id, BatchCode = batch.BatchCode, PondCode = pond.PondCode, StockingDensityPerM2 = density, DensityFlag = batch.DensityFlag });
        }

        result.CycleId = cycle.Id;
        result.CycleCode = cycle.CycleCode;
        result.PlLeft = plLeft - requested;
        return result;
    }

    [AbpAuthorize(PermissionNames.Production_Manage)]
    public virtual async Task Close(CloseCycleInput input)
    {
        var cycle = await _cycles.GetAsync(input.Id);
        Concurrency.Check(cycle.RowVersion, input.RowVersion);
        if (cycle.Status != CycleStatus.Active)
        {
            throw new UserFriendlyException("This cycle is already closed.");
        }

        if (await _batches.GetAll().AnyAsync(b => b.CycleId == cycle.Id && b.Status != BatchStatus.Closed))
        {
            throw new UserFriendlyException("Close every batch of this cycle before closing the cycle.");
        }

        if (input.Outcome is not { } outcome || !Enum.IsDefined(outcome))
        {
            throw new UserFriendlyException("Confirm the cycle's outcome: Completed or Failed.");
        }

        cycle.Status = CycleStatus.Closed;
        cycle.Outcome = outcome;
        cycle.ClosedAtUtc = DateTime.UtcNow;
        cycle.ClosedByUserId = AbpSession.GetUserId();
    }

    [AbpAuthorize(PermissionNames.Production_Manage)]
    public virtual async Task Reopen(ReopenInput input)
    {
        var cycle = await _cycles.GetAsync(input.Id);
        Concurrency.Check(cycle.RowVersion, input.RowVersion);
        if (cycle.Status != CycleStatus.Closed)
        {
            throw new UserFriendlyException("Only a Closed cycle can be reopened.");
        }

        var reason = input.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new UserFriendlyException("Give the reason for reopening.");
        }

        cycle.Status = CycleStatus.Active;
        cycle.Outcome = null;
        cycle.ClosedAtUtc = null;
        cycle.ClosedByUserId = null;
        await _reopenings.InsertAsync(new ReopenEvent
        {
            EntityType = ReopenedEntityType.Cycle, EntityId = cycle.Id, ReopenedAtUtc = DateTime.UtcNow, ReopenedByUserId = AbpSession.GetUserId(), Reason = reason,
        });
    }

    public static string NextLetter(ICollection<string> used)
    {
        for (var n = 0; ; n++)
        {
            var letter = n < 26 ? ((char)('A' + n)).ToString() : $"{(char)('A' + n / 26 - 1)}{(char)('A' + n % 26)}";
            if (!used.Contains(letter))
            {
                return letter;
            }
        }
    }

    private static Guid DeriveRequestId(Guid requestId, int index)
    {
        if (index == 0)
        {
            return requestId;
        }

        var bytes = requestId.ToByteArray();
        bytes[15] ^= (byte)index;
        bytes[14] ^= (byte)(index >> 8);
        return new Guid(bytes);
    }

    private async Task<CreateBatchesResultDto> DuplicateResultAsync(List<Guid> batchIds)
    {
        var batches = await _batches.GetAll().AsNoTracking().Include(b => b.Pond).Include(b => b.Cycle).ThenInclude(c => c.FryDelivery)
            .Where(b => batchIds.Contains(b.Id)).ToListAsync();
        var cycle = batches.First().Cycle;
        return new CreateBatchesResultDto
        {
            CycleId = cycle.Id, CycleCode = cycle.CycleCode, WasDuplicate = true,
            Batches = batches.Select(b => new CreatedBatchDto { Id = b.Id, BatchCode = b.BatchCode, PondCode = b.Pond?.PondCode, StockingDensityPerM2 = b.StockingDensityPerM2, DensityFlag = b.DensityFlag }).ToList(),
            PlLeft = cycle.FryDelivery.PlReceived - await PlStockedAsync(cycle.Id),
        };
    }

    private async Task<long> PlStockedAsync(Guid cycleId) =>
        await _batches.GetAll().Where(b => b.CycleId == cycleId).SumAsync(b => b.PlCountStocked ?? 0);

    private async Task<FryDeliveryStatus?> CurrentStatusAsync(Guid deliveryId) =>
        await _deliveryChanges.GetAll().Where(c => c.DeliveryId == deliveryId)
            .OrderByDescending(c => c.ChangedAtUtc).ThenByDescending(c => c.CreationTime)
            .Select(c => (FryDeliveryStatus?)c.Status).FirstOrDefaultAsync();

    private static StockingDeliveryDto MapDelivery(FryDelivery d, long plStocked, DateOnly today) => new()
    {
        Id = d.Id, HatcheryName = d.Hatchery?.Name, HatcheryShortCode = d.Hatchery?.ShortCode, HatcheryBatchNumber = d.HatcheryBatchNumber,
        DeliveryDate = d.DeliveryDate, PlAgeDays = d.PlAgeDays, PlReceived = d.PlReceived, PlLeft = d.PlReceived - plStocked,
        PlHealthCertificateNo = d.PlHealthCertificateNo, DaysSinceReceived = Math.Max(0, FarmClock.DaysBetween(d.DeliveryDate, today)),
    };

    private static void Fill(CycleListDto dto, ProductionCycle c, int batchCount, int openCount, long plStocked, DateOnly? start)
    {
        dto.Id = c.Id;
        dto.CycleCode = c.CycleCode;
        dto.Status = c.Status;
        dto.FryDeliveryId = c.FryDeliveryId;
        dto.HatcheryName = c.FryDelivery?.Hatchery?.Name;
        dto.HatcheryShortCode = c.FryDelivery?.Hatchery?.ShortCode;
        dto.HatcheryBatchNumber = c.FryDelivery?.HatcheryBatchNumber;
        dto.StartDate = start;
        dto.BatchCount = batchCount;
        dto.OpenBatchCount = openCount;
        dto.PlReceived = c.FryDelivery?.PlReceived ?? 0;
        dto.PlStocked = plStocked;
        dto.PlLeft = dto.PlReceived - plStocked;
        dto.Outcome = c.Outcome;
    }
}

