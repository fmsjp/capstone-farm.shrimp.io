using Abp.Application.Services.Dto;
using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.Runtime.Session;
using Abp.UI;
using Capstone.Authorization;
using Capstone.Common;
using Capstone.Domain;
using Capstone.Inventory;
using Capstone.Logbooks.Dto;
using Capstone.Production;
using Capstone.Production.Dto;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Capstone.Logbooks;

[AbpAuthorize(PermissionNames.Logs)]
public class FeedingAppService : CapstoneAppServiceBase
{
    private readonly IRepository<FeedingSession, Guid> _sessions;
    private readonly IRepository<PondBatch, Guid> _batches;
    private readonly IRepository<ProductionCycle, Guid> _cycles;
    private readonly IRepository<StockMovement, Guid> _movements;
    private readonly InventoryManager _inventory;
    private readonly BatchDataLoader _loader;
    private readonly DailyLogStatusService _daily;
    private readonly IRepository<ProductionLog, Guid> _logs;
    private readonly ReferenceData.FarmStandardsProvider _standards;

    public FeedingAppService(IRepository<FeedingSession, Guid> sessions, IRepository<PondBatch, Guid> batches, IRepository<ProductionCycle, Guid> cycles,
        IRepository<StockMovement, Guid> movements, InventoryManager inventory, BatchDataLoader loader, DailyLogStatusService daily,
        IRepository<ProductionLog, Guid> logs, ReferenceData.FarmStandardsProvider standards)
    {
        _standards = standards;
        _daily = daily;
        _logs = logs;
        _sessions = sessions;
        _batches = batches;
        _cycles = cycles;
        _movements = movements;
        _inventory = inventory;
        _loader = loader;
    }

    private static readonly string[] SessionSortKeys = { "feedDate", "session" };

    private IQueryable<FeedingSession> FilteredSessions(GetFeedingSessionsInput input) => _sessions.GetAll().AsNoTracking()
        .Where(s => !input.BatchId.HasValue || s.BatchId == input.BatchId)
        .Where(s => !input.CycleId.HasValue || s.Batch.CycleId == input.CycleId)
        .Where(s => !input.From.HasValue || s.FeedDate >= input.From)
        .Where(s => !input.To.HasValue || s.FeedDate <= input.To);

    public virtual async Task<PagedResultDto<FeedingSessionDto>> GetList(GetFeedingSessionsInput input)
    {
        var query = FilteredSessions(input);
        var total = await query.CountAsync();
        var sort = SortSpec.Parse(input.Sorting, SessionSortKeys, "feedDate", true);
        var ordered = sort.Key == "session"
            ? sort.Apply(query, s => s.Session).ThenByDescending(s => s.FeedDate)
            : sort.Apply(query, s => s.FeedDate).ThenByDescending(s => s.Session);
        var ids = await ordered.Skip(input.SkipCount).Take(input.MaxResultCount).Select(s => s.Id).ToListAsync();
        var mapped = await MapAsync(ids);
        var position = ids.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
        return new PagedResultDto<FeedingSessionDto>(total, mapped.OrderBy(m => position.TryGetValue(m.Id, out var i) ? i : int.MaxValue).ToList());
    }

    internal async Task<List<FeedingSessionDto>> ListAllAsync(GetFeedingSessionsInput input)
    {
        var ids = await FilteredSessions(input).OrderByDescending(s => s.FeedDate).ThenByDescending(s => s.Session).Select(s => s.Id).ToListAsync();
        var mapped = await MapAsync(ids);
        var position = ids.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
        return mapped.OrderBy(m => position.TryGetValue(m.Id, out var i) ? i : int.MaxValue).ToList();
    }

    public virtual async Task<FeedingSheetDto> GetSheet(GetFeedingSheetInput input)
    {
        ProductionCycle cycle = null;
        if (input.CycleId.HasValue)
        {
            cycle = await _cycles.FirstOrDefaultAsync(c => c.Id == input.CycleId) ?? throw new UserFriendlyException("Production cycle not found.");
        }

        var batches = await _batches.GetAll().AsNoTracking().Include(b => b.Pond).Include(b => b.Cycle)
            .Where(b => b.Status != BatchStatus.Closed && (cycle == null || b.CycleId == cycle.Id) && (input.BatchId == null || b.Id == input.BatchId))
            .Where(b => b.StockingDate == null || b.StockingDate <= input.FeedDate)
            .OrderBy(b => b.Pond.PondCode).ThenBy(b => b.BatchCode).ToListAsync();
        var batchIds = batches.Select(b => b.Id).ToList();
        var ids = await _sessions.GetAll().Where(s => batchIds.Contains(s.BatchId) && s.FeedDate == input.FeedDate).Select(s => s.Id).ToListAsync();
        var sessions = await MapAsync(ids);

        var zone = await Farm.GetTimeZoneAsync();
        var open = new List<PondBatch>();
        foreach (var b in batches)
        {
            if (sessions.Any(s => s.BatchId == b.Id) || await _daily.LastLogDateAsync(b, zone) is not { } last || last >= input.FeedDate)
            {
                open.Add(b);
            }
        }

        batches = open;
        return new FeedingSheetDto
        {
            CycleId = cycle?.Id, CycleCode = cycle?.CycleCode, FeedDate = input.FeedDate,
            Rows = batches.Select(b => new FeedingSheetRowDto
            {
                BatchId = b.Id, BatchCode = b.BatchCode, PondCode = b.Pond?.PondCode, CycleId = b.CycleId, CycleCode = b.Cycle?.CycleCode, Status = b.Status,
                Am = sessions.FirstOrDefault(s => s.BatchId == b.Id && s.Session == FeedingSessionTime.AM),
                Pm = sessions.FirstOrDefault(s => s.BatchId == b.Id && s.Session == FeedingSessionTime.PM),
            }).ToList(),
        };
    }

    [AbpAuthorize(PermissionNames.Logs_Create)]
    public virtual async Task<SaveFeedingResultDto> Save(SaveFeedingSessionInput input)
    {
        if (input.ClientRequestId == Guid.Empty)
        {
            throw new UserFriendlyException("ClientRequestId is required.");
        }

        var duplicate = await _sessions.GetAll().AsNoTracking().FirstOrDefaultAsync(s => s.ClientRequestId == input.ClientRequestId);
        if (duplicate != null)
        {
            return new SaveFeedingResultDto { Session = (await MapAsync(new List<Guid> { duplicate.Id })).Single(), WasDuplicate = true };
        }

        var batch = await _batches.GetAll().Include(b => b.Pond).FirstOrDefaultAsync(b => b.Id == input.BatchId) ?? throw new UserFriendlyException("Batch not found.");
        batch.EnsureNotClosed();
        if (!batch.AllowsFeeding)
        {
            throw new UserFriendlyException($"Feeding cannot be recorded while the batch is {batch.Status}.");
        }

        if (!Enum.IsDefined(input.Session))
        {
            throw new UserFriendlyException("Choose AM or PM.");
        }

        var today = await Farm.GetTodayAsync();
        if (input.FeedDate > today)
        {
            throw new UserFriendlyException("The feeding date cannot be in the future.");
        }

        if (batch.StockingDate.HasValue && input.FeedDate < batch.StockingDate.Value)
        {
            throw new UserFriendlyException("The feeding date cannot be before the stocking date.");
        }

        var zone = await Farm.GetTimeZoneAsync();
        if (await _daily.LastLogDateAsync(batch, zone) is { } lastDay && input.FeedDate > lastDay)
        {
            throw new UserFriendlyException($"The batch had its Total harvest on {lastDay:yyyy-MM-dd}; feeding cannot be dated after it.");
        }

        if (!Enum.IsDefined(input.Status))
        {
            throw new UserFriendlyException("Choose Fed or Not fed.");
        }

        var notFed = input.Status == FeedingStatus.NotFed;
        var fedAtTime = notFed ? null : ParseFedAtTime(input.FedAtTime);
        var lines = (input.Lines ?? new List<FeedingLineInput>()).Where(l => l.LotId != Guid.Empty).GroupBy(l => l.LotId)
            .Select(g => new FeedingLineInput { LotId = g.Key, Kg = g.Sum(x => x.Kg) }).ToList();
        if (notFed && lines.Count > 0)
        {
            throw new UserFriendlyException("A Not fed session has no feed lots. Remove the lots or choose Fed.");
        }

        if (!notFed && (lines.Count == 0 || lines.Any(l => l.Kg <= 0)))
        {
            throw new UserFriendlyException("Choose a feed lot and enter the kilograms fed (more than 0), or mark the session Not fed.");
        }

        var totalKg = lines.Sum(l => l.Kg);
        if (totalKg > FeedingSession.MaxKg)
        {
            throw new UserFriendlyException($"{totalKg:0.###} kg in one session is more than the 1,000 kg limit. Check the amount.");
        }

        var lots = new List<InventoryLot>();
        foreach (var line in lines)
        {
            var lot = await _inventory.GetLotWithItemAsync(line.LotId);
            if (lot.Item.Category != InventoryCategory.Feed)
            {
                throw new UserFriendlyException($"{lot.Item.DisplayName} is not feed. Chemicals and supplements are recorded as an application log.");
            }

            if (Units.Convert(1, "kg", lot.Item.Unit) == null)
            {
                throw new UserFriendlyException($"Feed item '{lot.Item.DisplayName}' must be measured in kg or g.");
            }

            lots.Add(lot);
        }

        var session = await _sessions.FirstOrDefaultAsync(s => s.BatchId == batch.Id && s.FeedDate == input.FeedDate && s.Session == input.Session);
        var isUpdate = session != null;
        if (isUpdate)
        {
            if (session.CreatorUserId != AbpSession.UserId && !await IsGrantedAsync(PermissionNames.Logs_EditAny))
            {
                throw new UserFriendlyException($"The {input.Session} session of {input.FeedDate:yyyy-MM-dd} was recorded by someone else. Ask the Owner to correct it.");
            }

            await _inventory.ReverseUsageForSessionAsync(session.Id, DateTime.UtcNow, $"Correction of the {input.Session} feeding of {input.FeedDate:yyyy-MM-dd}");
            session.Notes = Clean(input.Notes);
            session.Status = input.Status;
            session.FedAtTime = fedAtTime;
        }
        else
        {
            session = new FeedingSession
            {
                BatchId = batch.Id, FeedDate = input.FeedDate, Session = input.Session, Status = input.Status, Notes = Clean(input.Notes),
                FedAtTime = fedAtTime, ClientRequestId = input.ClientRequestId,
            };
            session.Id = await _sessions.InsertAndGetIdAsync(session);
        }

        var movementUtc = FarmClock.StartOfDayUtc(input.FeedDate, zone).AddHours(input.Session == FeedingSessionTime.AM ? 6 : 16);
        var warnings = new List<WarningDto>();
        for (var i = 0; i < lines.Count; i++)
        {
            var itemQty = Units.Convert(lines[i].Kg, "kg", lots[i].Item.Unit).Value;
            var usage = await _inventory.RecordUsageAsync(lots[i], itemQty, batch.Id, movementUtc, feedingSessionId: session.Id, usedOn: input.FeedDate);
            if (!string.IsNullOrEmpty(usage.Warning))
            {
                warnings.Add(new WarningDto { Kind = "Stock", Message = usage.Warning });
            }
        }

        var average = notFed ? null : await AverageSessionKgAsync(batch.Id, input.FeedDate.AddDays(-7), input.FeedDate.AddDays(-1));
        var spike = await _standards.GetAsync(ReferenceData.FarmThresholds.FeedSpikeMultiplier);
        if (average.HasValue && average.Value > 0 && totalKg > average.Value * spike)
        {
            warnings.Add(new WarningDto { Kind = "Feeding", Message = $"{totalKg:0.###} kg is more than {spike:0.#}× the batch's average session over the last 7 days ({average:0.###} kg). Check the amount." });
        }

        await CurrentUnitOfWork.SaveChangesAsync();
        var facts = await _loader.LoadAsync(batch.Id, zone);
        return new SaveFeedingResultDto
        {
            Session = (await MapAsync(new List<Guid> { session.Id })).Single(),
            WasUpdate = isUpdate,
            Warnings = warnings,
            Indicators = IndicatorsDto.From(IndicatorCalculator.Compute(facts, today)),
        };
    }

    public virtual async Task<DailyLogStatusDto> GetDailyStatus(GetDailyLogStatusInput input)
    {
        var zone = await Farm.GetTimeZoneAsync();
        var today = FarmClock.Today(zone);
        var to = input.EndDate ?? today;
        if (to > today)
        {
            to = today;
        }

        var days = Math.Clamp(input.Days, 1, 31);
        var from = to.AddDays(-(days - 1));
        var batches = await _batches.GetAll().AsNoTracking().Include(b => b.Pond).Include(b => b.Cycle)
            .Where(b => b.Status == BatchStatus.Stocked && (b.StockingDate == null || b.StockingDate <= to))
            .OrderBy(b => b.Pond.PondCode).ThenBy(b => b.BatchCode).ToListAsync();
        var grid = await _daily.LoadAsync(batches, from, to, zone);
        var ids = batches.Select(b => b.Id).ToList();
        var events = await _logs.GetAll().AsNoTracking()
            .Where(l => ids.Contains(l.BatchId) && (l.LogType == LogType.Mortality || l.LogType == LogType.GrowthSampling || l.LogType == LogType.ChemicalApplication))
            .GroupBy(l => new { l.BatchId, l.LogType }).Select(g => new { g.Key.BatchId, g.Key.LogType, Last = g.Max(l => l.ObservedAtUtc) }).ToListAsync();

        var fromUtc = FarmClock.StartOfDayUtc(from, zone);
        var toUtc = FarmClock.StartOfDayUtc(to.AddDays(1), zone);
        var windowEvents = await _logs.GetAll().AsNoTracking()
            .Where(l => ids.Contains(l.BatchId) && (l.LogType == LogType.Mortality || l.LogType == LogType.GrowthSampling || l.LogType == LogType.ChemicalApplication)
                        && l.ObservedAtUtc >= fromUtc && l.ObservedAtUtc < toUtc)
            .Select(l => new { l.BatchId, l.LogType, l.ObservedAtUtc }).ToListAsync();
        var eventDays = windowEvents.Select(e => (e.BatchId, Day: FarmClock.LocalDate(e.ObservedAtUtc, zone), e.LogType)).ToHashSet();

        int? Since(Guid batchId, LogType type)
        {
            var e = events.FirstOrDefault(x => x.BatchId == batchId && x.LogType == type);
            return e == null ? null : FarmClock.DaysBetween(FarmClock.LocalDate(e.Last, zone), today);
        }

        return new DailyLogStatusDto
        {
            From = from, To = to,
            Rows = batches.Select(b => new DailyLogStatusRowDto
            {
                BatchId = b.Id, BatchCode = b.BatchCode, PondId = b.PondId, PondCode = b.Pond?.PondCode, CycleCode = b.Cycle?.CycleCode, StockingDate = b.StockingDate,
                Days = grid[b.Id].Select(d => new DailyLogCellDto
                {
                    Date = d.Date, Am = d.Am, Pm = d.Pm, Water = d.Water,
                    Mortality = eventDays.Contains((b.Id, d.Date, LogType.Mortality)),
                    GrowthSampling = eventDays.Contains((b.Id, d.Date, LogType.GrowthSampling)),
                    Chemical = eventDays.Contains((b.Id, d.Date, LogType.ChemicalApplication)),
                }).ToList(),
                DaysSinceMortality = Since(b.Id, LogType.Mortality),
                DaysSinceGrowthSampling = Since(b.Id, LogType.GrowthSampling),
                DaysSinceChemical = Since(b.Id, LogType.ChemicalApplication),
            }).ToList(),
        };
    }

    [AbpAuthorize(PermissionNames.Logs_Create)]
    public virtual async Task<List<SaveFeedingResultDto>> SaveSheet(SaveFeedingSheetInput input)
    {
        if (input.Sessions == null || input.Sessions.Count == 0)
        {
            throw new UserFriendlyException("Enter at least one session.");
        }

        var results = new List<SaveFeedingResultDto>();
        foreach (var s in input.Sessions)
        {
            results.Add(await Save(s));
        }

        return results;
    }

    private static string ParseFedAtTime(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!TimeOnly.TryParseExact(value.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            throw new UserFriendlyException("Enter the time of feeding as hours and minutes, for example 06:30, or leave it blank.");
        }

        return time.ToString("HH:mm", CultureInfo.InvariantCulture);
    }

    [AbpAuthorize(PermissionNames.Logs_EditAny)]
    public virtual async Task Void(VoidInput input)
    {
        var session = await _sessions.FirstOrDefaultAsync(s => s.Id == input.Id) ?? throw new UserFriendlyException("Feeding session not found.");
        var batch = await _batches.GetAsync(session.BatchId);
        batch.EnsureNotClosed();
        await _inventory.ReverseUsageForSessionAsync(session.Id, DateTime.UtcNow, $"Void of the {session.Session} feeding of {session.FeedDate:yyyy-MM-dd}: {input.Reason.Trim()}");
        session.VoidReason = input.Reason.Trim();
        await CurrentUnitOfWork.SaveChangesAsync();
        await _sessions.DeleteAsync(session);
    }

    private static string Clean(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<decimal?> AverageSessionKgAsync(Guid batchId, DateOnly from, DateOnly to)
    {
        var ids = await _sessions.GetAll().Where(s => s.BatchId == batchId && s.FeedDate >= from && s.FeedDate <= to && s.Status == FeedingStatus.Fed)
            .Select(s => s.Id).ToListAsync();
        if (ids.Count == 0)
        {
            return null;
        }

        var kg = await SessionKgAsync(ids);
        return kg.Values.Sum() / ids.Count;
    }

    private async Task<Dictionary<Guid, decimal>> SessionKgAsync(List<Guid> sessionIds)
    {
        var rows = await _movements.GetAll().AsNoTracking().Include(m => m.Lot).ThenInclude(l => l.Item)
            .Where(m => m.FeedingSessionId != null && sessionIds.Contains(m.FeedingSessionId.Value)).ToListAsync();
        return rows.GroupBy(m => m.FeedingSessionId.Value)
            .ToDictionary(g => g.Key, g => g.Sum(m => Units.Convert(-m.Quantity, m.Lot.Item.Unit, "kg") ?? -m.Quantity));
    }

    private async Task<List<FeedingSessionDto>> MapAsync(List<Guid> ids)
    {
        if (ids.Count == 0)
        {
            return new List<FeedingSessionDto>();
        }

        var sessions = await _sessions.GetAll().AsNoTracking().Include(s => s.Batch).ThenInclude(b => b.Pond).Where(s => ids.Contains(s.Id)).ToListAsync();
        var movements = await _movements.GetAll().AsNoTracking().Include(m => m.Lot).ThenInclude(l => l.Item)
            .Where(m => m.FeedingSessionId != null && ids.Contains(m.FeedingSessionId.Value)).ToListAsync();
        var names = await GetUserNamesAsync(sessions.Select(s => s.CreatorUserId));
        var canCreate = await IsGrantedAsync(PermissionNames.Logs_Create);
        var canEditAny = await IsGrantedAsync(PermissionNames.Logs_EditAny);
        return sessions.Select(s =>
        {
            var lines = movements.Where(m => m.FeedingSessionId == s.Id).GroupBy(m => m.LotId)
                .Select(g => new FeedingLineDto
                {
                    LotId = g.Key,
                    Label = $"{g.First().Lot.Item.DisplayName} — lot {g.First().Lot.LotNumber}",
                    Kg = Math.Round(g.Sum(m => Units.Convert(-m.Quantity, m.Lot.Item.Unit, "kg") ?? -m.Quantity), 3),
                })
                .Where(l => l.Kg != 0).ToList();
            var open = s.Batch?.Status != BatchStatus.Closed;
            return new FeedingSessionDto
            {
                Id = s.Id, BatchId = s.BatchId, BatchCode = s.Batch?.BatchCode, PondCode = s.Batch?.Pond?.PondCode, FeedDate = s.FeedDate, Session = s.Session, Status = s.Status,
                Kg = lines.Sum(l => l.Kg), Lines = lines, Notes = s.Notes, FedAtTime = s.FedAtTime, RecordedBy = NameOf(names, s.CreatorUserId), CreationTime = s.CreationTime,
                CanEdit = canCreate && open && (canEditAny || s.CreatorUserId == AbpSession.UserId),
                CanVoid = canEditAny && open,
            };
        }).OrderByDescending(s => s.FeedDate).ThenByDescending(s => s.Session).ToList();
    }
}
