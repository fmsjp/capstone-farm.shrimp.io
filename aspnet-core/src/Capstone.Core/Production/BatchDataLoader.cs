using Abp.Dependency;
using Abp.Domain.Repositories;
using Capstone.Domain;
using Capstone.Harvests;
using Capstone.Inventory;
using Capstone.Ponds;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Capstone.Production;

public class BatchDataLoader : ITransientDependency
{
    private readonly IRepository<PondBatch, Guid> _batches;
    private readonly IRepository<Pond, Guid> _ponds;
    private readonly IRepository<ProductionLog, Guid> _logs;
    private readonly IRepository<StockMovement, Guid> _movements;
    private readonly IRepository<InventoryLot, Guid> _lots;
    private readonly IRepository<MortalityDetail, Guid> _mortality;
    private readonly IRepository<GrowthSamplingDetail, Guid> _growth;
    private readonly IRepository<Harvest, Guid> _harvests;

    public BatchDataLoader(
        IRepository<PondBatch, Guid> batches,
        IRepository<Pond, Guid> ponds,
        IRepository<ProductionLog, Guid> logs,
        IRepository<StockMovement, Guid> movements,
        IRepository<InventoryLot, Guid> lots,
        IRepository<MortalityDetail, Guid> mortality,
        IRepository<GrowthSamplingDetail, Guid> growth,
        IRepository<Harvest, Guid> harvests)
    {
        _batches = batches;
        _ponds = ponds;
        _logs = logs;
        _movements = movements;
        _lots = lots;
        _mortality = mortality;
        _growth = growth;
        _harvests = harvests;
    }

    public async Task<Dictionary<Guid, BatchFacts>> LoadAsync(IReadOnlyCollection<Guid> batchIds, TimeZoneInfo zone)
    {
        var ids = batchIds.Distinct().ToList();
        var result = new Dictionary<Guid, BatchFacts>();
        if (ids.Count == 0)
        {
            return result;
        }

        var batches = await _batches.GetAll().AsNoTracking()
            .Where(b => ids.Contains(b.Id))
            .Select(b => new { b.Id, b.PondId, b.PlCountStocked, b.StockingDate, b.PondAreaM2AtStocking })
            .ToListAsync();

        var logs = _logs.GetAll().AsNoTracking().Where(l => ids.Contains(l.BatchId));

        var feedRows = await (from m in _movements.GetAll().AsNoTracking()
                              join lot in _lots.GetAll().AsNoTracking() on m.LotId equals lot.Id
                              where m.FeedingSessionId != null && m.BatchId != null && ids.Contains(m.BatchId.Value)
                              group m by new { BatchId = m.BatchId.Value, lot.Item.Unit } into g
                              select new { g.Key.BatchId, g.Key.Unit, Qty = -g.Sum(x => x.Quantity) }).ToListAsync();
        var feed = feedRows.GroupBy(r => r.BatchId)
            .Select(g => new { BatchId = g.Key, Kg = g.Sum(r => Units.Convert(r.Qty, r.Unit, "kg") ?? r.Qty) }).ToList();

        var mortality = await (from l in logs
                               join d in _mortality.GetAll().AsNoTracking() on l.Id equals d.Id
                               select new { l.BatchId, l.ObservedAtUtc, Count = (long)d.Count }).ToListAsync();

        var growth = await (from l in logs
                            join d in _growth.GetAll().AsNoTracking() on l.Id equals d.Id
                            select new { l.BatchId, l.ObservedAtUtc, d.AbwG, d.EstimatedSurvivalPct }).ToListAsync();

        var harvests = await _harvests.GetAll().AsNoTracking()
            .Include(h => h.GradeLines).ThenInclude(g => g.SizeGrade)
            .Where(h => ids.Contains(h.BatchId))
            .ToListAsync();

        foreach (var b in batches)
        {
            result[b.Id] = new BatchFacts
            {
                PlCountStocked = b.PlCountStocked ?? 0,
                PondAreaM2 = b.PondAreaM2AtStocking,
                StockingDate = b.StockingDate,
                CumulativeFeedKg = feed.FirstOrDefault(x => x.BatchId == b.Id)?.Kg ?? 0m,
                CumulativeMortality = mortality.Where(x => x.BatchId == b.Id).Sum(x => x.Count),
                Mortalities = mortality.Where(x => x.BatchId == b.Id).Select(x => new MortalityFact { ObservedAtUtc = x.ObservedAtUtc, Count = x.Count }).ToList(),
                GrowthSamples = growth.Where(g => g.BatchId == b.Id)
                    .OrderBy(g => g.ObservedAtUtc)
                    .Select(g => new GrowthSampleFact
                    {
                        ObservedAtUtc = g.ObservedAtUtc, Date = FarmClock.LocalDate(g.ObservedAtUtc, zone), AbwG = g.AbwG, EstimatedSurvivalPct = g.EstimatedSurvivalPct,
                    })
                    .ToList(),
                Harvests = harvests.Where(h => h.BatchId == b.Id)
                    .OrderBy(h => h.HarvestedAtUtc)
                    .Select(h => HarvestFact.From(h, FarmClock.LocalDate(h.HarvestedAtUtc, zone)))
                    .ToList(),
            };
        }

        return result;
    }

    public async Task<BatchFacts> LoadAsync(Guid batchId, TimeZoneInfo zone)
    {
        var all = await LoadAsync(new[] { batchId }, zone);
        return all.TryGetValue(batchId, out var facts) ? facts : new BatchFacts();
    }
}
