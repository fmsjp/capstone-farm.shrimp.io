using Abp.Dependency;
using Abp.Domain.Repositories;
using Capstone.Domain;
using Capstone.Harvests;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Capstone.Production;

public enum DailyCellStatus { Blank = 0, Logged = 1, NotFed = 2 }

public class DailyLogDay
{
    public DateOnly Date { get; set; }
    public DailyCellStatus Am { get; set; }
    public DailyCellStatus Pm { get; set; }
    public DailyCellStatus Water { get; set; }

    public bool HasBlank => Am == DailyCellStatus.Blank || Pm == DailyCellStatus.Blank || Water == DailyCellStatus.Blank;

    public string DescribeBlanks()
    {
        var missing = new List<string>();
        if (Am == DailyCellStatus.Blank) missing.Add("AM feeding");
        if (Pm == DailyCellStatus.Blank) missing.Add("PM feeding");
        if (Water == DailyCellStatus.Blank) missing.Add("water quality");
        return string.Join(", ", missing);
    }
}

public class DailyLogStatusService : ITransientDependency
{
    private readonly IRepository<FeedingSession, Guid> _sessions;
    private readonly IRepository<ProductionLog, Guid> _logs;
    private readonly IRepository<Harvest, Guid> _harvests;
    private readonly IRepository<BatchFailure, Guid> _failures;

    public DailyLogStatusService(IRepository<FeedingSession, Guid> sessions, IRepository<ProductionLog, Guid> logs, IRepository<Harvest, Guid> harvests,
        IRepository<BatchFailure, Guid> failures)
    {
        _sessions = sessions;
        _logs = logs;
        _harvests = harvests;
        _failures = failures;
    }

    public async Task<Dictionary<Guid, List<DailyLogDay>>> LoadAsync(IReadOnlyCollection<PondBatch> batches, DateOnly from, DateOnly to, TimeZoneInfo zone)
    {
        var ids = batches.Select(b => b.Id).ToList();
        var sessions = await _sessions.GetAll().AsNoTracking()
            .Where(s => ids.Contains(s.BatchId) && s.FeedDate >= from && s.FeedDate <= to)
            .Select(s => new { s.BatchId, s.FeedDate, s.Session, s.Status }).ToListAsync();
        var fromUtc = FarmClock.StartOfDayUtc(from, zone);
        var toUtc = FarmClock.StartOfDayUtc(to.AddDays(1), zone);
        var water = await _logs.GetAll().AsNoTracking()
            .Where(l => ids.Contains(l.BatchId) && (l.LogType == LogType.WaterQuality || l.LogType == LogType.PreStockingWaterQuality)
                        && l.ObservedAtUtc >= fromUtc && l.ObservedAtUtc < toUtc)
            .Select(l => new { l.BatchId, l.ObservedAtUtc }).ToListAsync();
        var waterDays = water.Select(w => (w.BatchId, Day: FarmClock.LocalDate(w.ObservedAtUtc, zone))).ToHashSet();

        DailyCellStatus Feed(Guid batchId, DateOnly day, FeedingSessionTime time)
        {
            var s = sessions.FirstOrDefault(x => x.BatchId == batchId && x.FeedDate == day && x.Session == time);
            return s == null ? DailyCellStatus.Blank : s.Status == FeedingStatus.NotFed ? DailyCellStatus.NotFed : DailyCellStatus.Logged;
        }

        var result = new Dictionary<Guid, List<DailyLogDay>>();
        foreach (var b in batches)
        {
            var start = b.StockingDate.HasValue && b.StockingDate.Value > from ? b.StockingDate.Value : from;
            var days = new List<DailyLogDay>();
            for (var d = start; d <= to; d = d.AddDays(1))
            {
                days.Add(new DailyLogDay
                {
                    Date = d,
                    Am = Feed(b.Id, d, FeedingSessionTime.AM),
                    Pm = Feed(b.Id, d, FeedingSessionTime.PM),
                    Water = waterDays.Contains((b.Id, d)) ? DailyCellStatus.Logged : DailyCellStatus.Blank,
                });
            }

            result[b.Id] = days;
        }

        return result;
    }

    public async Task<DateOnly?> LastLogDateAsync(PondBatch batch, TimeZoneInfo zone)
    {
        if (batch.Status == BatchStatus.Failed)
        {
            return null;
        }

        var final = await _harvests.GetAll().AsNoTracking()
            .Where(h => h.BatchId == batch.Id && h.HarvestType == HarvestType.Total)
            .OrderByDescending(h => h.HarvestedAtUtc).Select(h => (DateTime?)h.HarvestedAtUtc).FirstOrDefaultAsync();
        return final.HasValue ? FarmClock.LocalDate(final.Value, zone) : null;
    }

    public async Task<DateOnly> ExpectedUntilAsync(PondBatch batch, DateOnly today, TimeZoneInfo zone)
    {
        var failure = await _failures.GetAll().AsNoTracking().Where(f => f.BatchId == batch.Id).Select(f => (DateOnly?)f.FailedOn).FirstOrDefaultAsync();
        if (failure.HasValue)
        {
            return failure.Value < today ? failure.Value : today;
        }

        var final = await _harvests.GetAll().AsNoTracking()
            .Where(h => h.BatchId == batch.Id && h.HarvestType == HarvestType.Total)
            .OrderByDescending(h => h.HarvestedAtUtc).Select(h => (DateTime?)h.HarvestedAtUtc).FirstOrDefaultAsync();
        var finalDay = final.HasValue ? FarmClock.LocalDate(final.Value, zone) : (DateOnly?)null;
        return finalDay.HasValue && finalDay.Value < today ? finalDay.Value : today;
    }
}
