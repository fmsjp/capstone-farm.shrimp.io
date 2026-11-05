using Abp.Domain.Repositories;
using Abp.Domain.Services;
using Capstone.Alerts;
using Capstone.Domain;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Capstone.Inventory;

public class StockUsageResult
{
    public Guid MovementId { get; set; }
    public Guid LotId { get; set; }
    public decimal QuantityOnHandAfter { get; set; }
    public string Warning { get; set; }
}

public class InventoryManager : DomainService
{
    private readonly IRepository<InventoryItem, Guid> _items;
    private readonly IRepository<InventoryLot, Guid> _lots;
    private readonly IRepository<StockMovement, Guid> _movements;
    private readonly FarmAlertPublisher _alerts;

    public InventoryManager(
        IRepository<InventoryItem, Guid> items,
        IRepository<InventoryLot, Guid> lots,
        IRepository<StockMovement, Guid> movements,
        FarmAlertPublisher alerts)
    {
        _items = items;
        _lots = lots;
        _movements = movements;
        _alerts = alerts;
    }

    public async Task<decimal> GetLotQuantityAsync(Guid lotId)
    {
        return await _movements.GetAll().Where(m => m.LotId == lotId).SumAsync(m => (decimal?)m.Quantity) ?? 0m;
    }

    public async Task<decimal> GetItemQuantityAsync(Guid itemId)
    {
        return await (from m in _movements.GetAll()
                      join l in _lots.GetAll() on m.LotId equals l.Id
                      where l.ItemId == itemId
                      select (decimal?)m.Quantity).SumAsync() ?? 0m;
    }

    public async Task<Dictionary<Guid, decimal>> GetLotQuantitiesAsync(IEnumerable<Guid> lotIds = null)
    {
        var query = _movements.GetAll().AsNoTracking();
        if (lotIds != null)
        {
            var ids = lotIds.ToList();
            query = query.Where(m => ids.Contains(m.LotId));
        }

        return await query.GroupBy(m => m.LotId)
            .Select(g => new { g.Key, Qty = g.Sum(x => x.Quantity) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty);
    }

    public async Task<InventoryLot> GetLotWithItemAsync(Guid lotId)
    {
        var lot = await _lots.GetAll().Include(l => l.Item).FirstOrDefaultAsync(l => l.Id == lotId);
        return lot ?? throw FpmsErrors.Rule("The selected inventory lot was not found in this farm.");
    }

    public async Task<StockMovement> RecordReceiptAsync(InventoryLot lot, decimal quantity, decimal? unitCost, DateOnly date, DateTime movementUtc, string reason)
    {
        FpmsErrors.Require(quantity > 0, "Receipt quantity must be greater than zero.");
        FpmsErrors.Require(!unitCost.HasValue || unitCost.Value >= 0, "Unit cost cannot be negative.");

        var item = lot.Item ?? await _items.GetAsync(lot.ItemId);
        lot.UnitCost = unitCost;
        await CurrentUnitOfWork.SaveChangesAsync();

        var movement = new StockMovement
        {
            LotId = lot.Id,
            MovementDateUtc = movementUtc,
            Type = StockMovementType.Receipt,
            Quantity = quantity,
            UnitCost = unitCost,
            Reason = reason,
        };
        movement.Id = await _movements.InsertAndGetIdAsync(movement);

        await RefreshLotFlagAsync(lot);
        return movement;
    }

    public async Task<StockMovement> RecordAdjustmentAsync(InventoryLot lot, decimal signedQuantity, string reason, DateTime movementUtc, Guid? batchId = null, Guid? reversesMovementId = null, Guid? sourceLogId = null)
    {
        FpmsErrors.Require(signedQuantity != 0, "Adjustment quantity cannot be zero.");
        FpmsErrors.Require(!string.IsNullOrWhiteSpace(reason), "An adjustment needs a reason.");
        var movement = new StockMovement
        {
            LotId = lot.Id,
            MovementDateUtc = movementUtc,
            Type = StockMovementType.Adjustment,
            Quantity = signedQuantity,
            UnitCost = lot.UnitCost,
            Reason = reason,
            BatchId = batchId,
            ReversesMovementId = reversesMovementId,
            SourceLogId = sourceLogId,
        };
        movement.Id = await _movements.InsertAndGetIdAsync(movement);
        await RefreshLotFlagAsync(lot);
        return movement;
    }

    public async Task<StockUsageResult> RecordUsageAsync(InventoryLot lot, decimal quantity, Guid batchId, DateTime movementUtc,
        Guid? sourceLogId = null, Guid? feedingSessionId = null, DateOnly? usedOn = null)
    {
        FpmsErrors.Require(sourceLogId.HasValue != feedingSessionId.HasValue, "A usage movement needs exactly one source.");
        FpmsErrors.Require(quantity >= 0, "Usage quantity cannot be negative.");
        var item = lot.Item ?? await _items.GetAsync(lot.ItemId);
        await CurrentUnitOfWork.SaveChangesAsync();
        var before = await GetLotQuantityAsync(lot.Id);

        var movement = new StockMovement
        {
            LotId = lot.Id,
            MovementDateUtc = movementUtc,
            Type = StockMovementType.Usage,
            Quantity = -quantity,
            UnitCost = lot.UnitCost,
            BatchId = batchId,
            SourceLogId = sourceLogId,
            FeedingSessionId = feedingSessionId,
        };
        movement.Id = await _movements.InsertAndGetIdAsync(movement);
        await CurrentUnitOfWork.SaveChangesAsync();

        var after = before - quantity;
        var result = new StockUsageResult { MovementId = movement.Id, LotId = lot.Id, QuantityOnHandAfter = after };
        if (after < 0)
        {
            lot.NeedsReconciliation = true;
            result.Warning = $"Stock shortfall: lot {lot.LotNumber} of {item.DisplayName} had {before:0.###} {item.Unit} on hand; " +
                             $"this entry uses {quantity:0.###} {item.Unit}. The entry was saved and the lot is flagged for reconciliation.";
        }
        else if (await LowStockWarningAsync(item) is { } low)
        {
            result.Warning = low;
        }

        if (usedOn.HasValue && lot.ExpiryDate.HasValue && lot.ExpiryDate.Value < usedOn.Value)
        {
            var expired = $"Lot {lot.LotNumber} of {item.DisplayName} expired on {lot.ExpiryDate:yyyy-MM-dd}.";
            result.Warning = string.IsNullOrEmpty(result.Warning) ? expired : $"{result.Warning} {expired}";
        }

        return result;
    }

    public Task ReverseUsageForLogAsync(Guid logId, DateTime movementUtc, string reason) => ReverseUsageAsync(logId, null, movementUtc, reason);

    public Task ReverseUsageForSessionAsync(Guid sessionId, DateTime movementUtc, string reason) => ReverseUsageAsync(null, sessionId, movementUtc, reason);

    private async Task ReverseUsageAsync(Guid? logId, Guid? sessionId, DateTime movementUtc, string reason)
    {
        var mine = _movements.GetAll().Where(m => logId.HasValue ? m.SourceLogId == logId : m.FeedingSessionId == sessionId);
        var originals = await mine.Where(m => m.Type == StockMovementType.Usage).ToListAsync();
        var reversed = await mine.Where(m => m.ReversesMovementId != null).Select(m => m.ReversesMovementId.Value).ToListAsync();

        foreach (var original in originals.Where(o => !reversed.Contains(o.Id)))
        {
            var lot = await _lots.GetAll().Include(l => l.Item).FirstAsync(l => l.Id == original.LotId);
            await _movements.InsertAsync(new StockMovement
            {
                LotId = original.LotId,
                MovementDateUtc = movementUtc,
                Type = StockMovementType.Adjustment,
                Quantity = -original.Quantity,
                UnitCost = original.UnitCost,
                Reason = reason,
                BatchId = original.BatchId,
                SourceLogId = original.SourceLogId,
                FeedingSessionId = original.FeedingSessionId,
                ReversesMovementId = original.Id,
            });
            await CurrentUnitOfWork.SaveChangesAsync();
            await RefreshLotFlagAsync(lot);
        }
    }

    private async Task RefreshLotFlagAsync(InventoryLot lot)
    {
        await CurrentUnitOfWork.SaveChangesAsync();
    }

    private async Task<string> LowStockWarningAsync(InventoryItem item)
    {
        if (!item.ReorderLevel.HasValue)
        {
            return null;
        }

        await CurrentUnitOfWork.SaveChangesAsync();
        var itemQty = await GetItemQuantityAsync(item.Id);
        return itemQty >= 0 && itemQty <= item.ReorderLevel.Value
            ? $"Low stock: {item.DisplayName} has {itemQty:0.###} {item.Unit} on hand (reorder level {item.ReorderLevel:0.###})."
            : null;
    }
}
