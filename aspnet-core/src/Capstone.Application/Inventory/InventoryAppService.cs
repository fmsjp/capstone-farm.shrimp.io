using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.UI;
using Capstone.Authorization;
using Capstone.Common;
using Capstone.Domain;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Capstone.Inventory;

public class InventoryItemDto
{
    public Guid Id { get; set; }
    public InventoryCategory Category { get; set; }

    [StringLength(InventoryItem.MaxShortLength)]
    public string Brand { get; set; }

    [Required, StringLength(InventoryItem.MaxNameLength)]
    public string ProductName { get; set; }

    [StringLength(50)]
    public string FeedType { get; set; }

    [StringLength(InventoryItem.MaxShortLength)]
    public string BaiRegistrationNo { get; set; }

    [Required]
    public string Unit { get; set; } = "kg";

    [Range(0, 100000000)]
    public decimal? ReorderLevel { get; set; }

    public bool IsActive { get; set; } = true;

    [Range(0.001, 100000)]
    public decimal? PackSize { get; set; }

    [StringLength(InventoryItem.MaxNameLength)]
    public string ActiveIngredient { get; set; }

    [Range(0, 365)]
    public int? DefaultWithdrawalDays { get; set; }

    public decimal QuantityOnHand { get; set; }
    public bool IsLowStock { get; set; }
    public bool IsNegativeStock { get; set; }
    public int LotCount { get; set; }
    public string DisplayName { get; set; }
}

public class InventoryLotDto
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public string ItemName { get; set; }
    public InventoryCategory Category { get; set; }
    public string Unit { get; set; }
    public string LotNumber { get; set; }
    public DateOnly ReceivedDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public string Supplier { get; set; }
    public decimal? UnitCost { get; set; }
    public decimal QuantityOnHand { get; set; }
    public bool NeedsReconciliation { get; set; }
    public bool IsNegative { get; set; }
    public bool IsExpired { get; set; }
}

public class StockMovementDto
{
    public Guid Id { get; set; }
    public Guid LotId { get; set; }
    public string LotNumber { get; set; }
    public string ItemName { get; set; }
    public string Unit { get; set; }
    public DateTime MovementDateUtc { get; set; }
    public StockMovementType Type { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitCost { get; set; }
    public string Reason { get; set; }
    public Guid? SourceLogId { get; set; }
    public Guid? FeedingSessionId { get; set; }

    public string Source { get; set; }

    public Guid? BatchId { get; set; }
    public string BatchCode { get; set; }
    public decimal RunningBalance { get; set; }
    public string RecordedBy { get; set; }
}

public class RecordReceiptInput
{
    public Guid ItemId { get; set; }

    [Required, StringLength(InventoryLot.MaxLotLength)]
    public string LotNumber { get; set; }

    public DateOnly ReceivedDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }

    [StringLength(150)]
    public string Supplier { get; set; }

    [Range(0, 100000000)]
    public decimal Quantity { get; set; }

    [Range(0, 1000000)]
    public decimal? PackCount { get; set; }

    [Range(0, 100000000)]
    public decimal? UnitCost { get; set; }

    [Range(0, 100000000)]
    public decimal? PackCost { get; set; }

    [StringLength(StockMovement.MaxReasonLength)]
    public string Note { get; set; }
}

public class RecordAdjustmentInput
{
    public Guid LotId { get; set; }

    public decimal Quantity { get; set; }

    [Required, StringLength(StockMovement.MaxReasonLength)]
    public string Reason { get; set; }

    public DateOnly? Date { get; set; }
}

public class ReceiptResultDto
{
    public Guid LotId { get; set; }
    public Guid MovementId { get; set; }
    public decimal QuantityOnHand { get; set; }
    public decimal? UnitCost { get; set; }
}

public class LotOptionDto
{
    public Guid LotId { get; set; }
    public Guid ItemId { get; set; }
    public InventoryCategory Category { get; set; }
    public string Label { get; set; }
    public string Unit { get; set; }
    public decimal QuantityOnHand { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public bool IsExpired { get; set; }

    public int? DefaultWithdrawalDays { get; set; }

    public string ActiveIngredient { get; set; }

    internal DateOnly SortReceived { get; set; }
}

[AbpAuthorize(PermissionNames.Inventory)]
public class InventoryAppService : CapstoneAppServiceBase
{
    private readonly IRepository<InventoryItem, Guid> _items;
    private readonly IRepository<InventoryLot, Guid> _lots;
    private readonly IRepository<StockMovement, Guid> _movements;
    private readonly InventoryManager _inventory;
    private readonly IRepository<Production.PondBatch, Guid> _batches;
    private readonly IRepository<Production.FeedingSession, Guid> _sessions;
    private readonly IRepository<Production.ProductionLog, Guid> _logs;

    public InventoryAppService(IRepository<InventoryItem, Guid> items, IRepository<InventoryLot, Guid> lots,
        IRepository<StockMovement, Guid> movements, InventoryManager inventory, IRepository<Production.PondBatch, Guid> batches,
        IRepository<Production.FeedingSession, Guid> sessions, IRepository<Production.ProductionLog, Guid> logs)
    {
        _items = items;
        _lots = lots;
        _movements = movements;
        _inventory = inventory;
        _batches = batches;
        _sessions = sessions;
        _logs = logs;
    }

    public virtual async Task<List<InventoryItemDto>> GetItems()
    {
        var items = await _items.GetAll().AsNoTracking().OrderBy(i => i.Category).ThenBy(i => i.Brand).ThenBy(i => i.ProductName).ToListAsync();
        var onHand = await (from m in _movements.GetAll()
                            join l in _lots.GetAll() on m.LotId equals l.Id
                            group m by l.ItemId into g
                            select new { ItemId = g.Key, Qty = g.Sum(x => x.Quantity) }).ToDictionaryAsync(x => x.ItemId, x => x.Qty);
        var lotCounts = await _lots.GetAll().GroupBy(l => l.ItemId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);

        return items.Select(i =>
        {
            var qty = onHand.TryGetValue(i.Id, out var q) ? q : 0m;
            return new InventoryItemDto
            {
                Id = i.Id, Category = i.Category, Brand = i.Brand, ProductName = i.ProductName, FeedType = i.FeedType, BaiRegistrationNo = i.BaiRegistrationNo,
                Unit = i.Unit, ReorderLevel = i.ReorderLevel, IsActive = i.IsActive,
                PackSize = i.PackSize, ActiveIngredient = i.ActiveIngredient, DefaultWithdrawalDays = i.DefaultWithdrawalDays,
                QuantityOnHand = qty, IsNegativeStock = qty < 0, IsLowStock = qty >= 0 && i.ReorderLevel.HasValue && qty <= i.ReorderLevel.Value,
                LotCount = lotCounts.TryGetValue(i.Id, out var n) ? n : 0, DisplayName = i.DisplayName,
            };
        }).ToList();
    }

    [AbpAuthorize(PermissionNames.Inventory_Manage)]
    public virtual async Task<Guid> SaveItem(InventoryItemDto input)
    {
        if (!Units.IsValidInventoryUnit(input.Unit))
        {
            throw new UserFriendlyException($"Unit must be one of: {string.Join(", ", Units.InventoryUnits)}.");
        }

        var brand = input.Brand?.Trim() ?? string.Empty;
        var product = input.ProductName.Trim();
        if (await _items.GetAll().AnyAsync(i => i.Brand == brand && i.ProductName == product && i.Id != input.Id))
        {
            throw new UserFriendlyException($"'{brand} {product}' already exists.");
        }

        InventoryItem item;
        if (input.Id == Guid.Empty)
        {
            item = new InventoryItem();
        }
        else
        {
            item = await _items.GetAsync(input.Id);
            if (item.Unit != input.Unit && await _lots.GetAll().AnyAsync(l => l.ItemId == item.Id))
            {
                throw new UserFriendlyException("The unit cannot change after lots have been recorded.");
            }
        }

        item.Category = input.Category;
        item.Brand = brand;
        item.ProductName = product;
        item.FeedType = input.Category == InventoryCategory.Feed ? input.FeedType : null;
        item.BaiRegistrationNo = input.BaiRegistrationNo;
        item.Unit = input.Unit;
        item.ReorderLevel = input.ReorderLevel;
        item.IsActive = input.IsActive;
        item.PackSize = input.PackSize;
        item.ActiveIngredient = string.IsNullOrWhiteSpace(input.ActiveIngredient) ? null : input.ActiveIngredient.Trim();
        item.DefaultWithdrawalDays = input.DefaultWithdrawalDays;
        return input.Id == Guid.Empty ? await _items.InsertAndGetIdAsync(item) : item.Id;
    }

    [AbpAuthorize(PermissionNames.Inventory_Manage)]
    public virtual async Task SetItemActive(SetActiveInput input)
    {
        var item = await _items.GetAsync(input.Id);
        item.IsActive = input.IsActive;
    }

    public virtual async Task<List<InventoryLotDto>> GetLots(Guid? itemId)
    {
        var today = await Farm.GetTodayAsync();
        var lots = await _lots.GetAll().AsNoTracking().Include(l => l.Item)
            .Where(l => !itemId.HasValue || l.ItemId == itemId)
            .OrderBy(l => l.Item.ProductName).ThenBy(l => l.ReceivedDate).ToListAsync();
        var qty = await _inventory.GetLotQuantitiesAsync(lots.Select(l => l.Id));
        return lots.Select(l =>
        {
            var q = qty.TryGetValue(l.Id, out var v) ? v : 0m;
            return new InventoryLotDto
            {
                Id = l.Id, ItemId = l.ItemId, ItemName = l.Item.DisplayName, Category = l.Item.Category, Unit = l.Item.Unit, LotNumber = l.LotNumber,
                ReceivedDate = l.ReceivedDate, ExpiryDate = l.ExpiryDate, Supplier = l.Supplier, UnitCost = l.UnitCost, QuantityOnHand = q,
                NeedsReconciliation = l.NeedsReconciliation, IsNegative = q < 0, IsExpired = l.ExpiryDate.HasValue && l.ExpiryDate.Value < today,
            };
        }).ToList();
    }

    public virtual async Task<List<LotOptionDto>> GetLotOptions(InventoryCategory? category)
    {
        var lots = await _lots.GetAll().AsNoTracking().Include(l => l.Item)
            .Where(l => l.Item.IsActive)
            .Where(l => !category.HasValue || l.Item.Category == category)
            .ToListAsync();
        var qty = await _inventory.GetLotQuantitiesAsync(lots.Select(l => l.Id));
        var today = await Farm.GetTodayAsync();
        return lots.Select(l =>
        {
            var q = qty.TryGetValue(l.Id, out var v) ? v : 0m;
            var expired = l.ExpiryDate.HasValue && l.ExpiryDate.Value < today;
            return new LotOptionDto
            {
                LotId = l.Id, ItemId = l.ItemId, Category = l.Item.Category, Unit = l.Item.Unit, QuantityOnHand = q, ExpiryDate = l.ExpiryDate, IsExpired = expired,
                DefaultWithdrawalDays = l.Item.DefaultWithdrawalDays, ActiveIngredient = l.Item.ActiveIngredient,
                Label = $"{l.Item.DisplayName} — lot {l.LotNumber} ({q:0.###} {l.Item.Unit}{(l.ExpiryDate.HasValue ? $", {(expired ? "expired" : "exp.")} {l.ExpiryDate:yyyy-MM-dd}" : "")})",
                SortReceived = l.ReceivedDate,
            };
        })
        .OrderBy(o => o.QuantityOnHand > 0 ? 0 : 1)
        .ThenBy(o => o.ExpiryDate ?? DateOnly.MaxValue)
        .ThenBy(o => o.SortReceived)
        .ToList();
    }

    [AbpAuthorize(PermissionNames.Inventory_Manage)]
    public virtual async Task<ReceiptResultDto> RecordReceipt(RecordReceiptInput input)
    {
        var item = await _items.FirstOrDefaultAsync(i => i.Id == input.ItemId) ?? throw new UserFriendlyException("Item not found.");
        var lotNumber = input.LotNumber.Trim();
        if (await _lots.GetAll().AnyAsync(l => l.ItemId == item.Id && l.LotNumber == lotNumber))
        {
            throw new UserFriendlyException($"Lot {lotNumber} of {item.DisplayName} already exists. Record new stock as a new lot number.");
        }

        var quantity = input.Quantity;
        var unitCost = input.UnitCost;
        if (input.PackCount.HasValue)
        {
            if (!item.PackSize.HasValue)
            {
                throw new UserFriendlyException($"{item.DisplayName} has no pack size. Set one on the item or enter the quantity in {item.Unit}.");
            }

            if (input.PackCount <= 0)
            {
                throw new UserFriendlyException("The number of packs must be greater than zero.");
            }

            quantity = input.PackCount.Value * item.PackSize.Value;
            if (input.PackCost.HasValue)
            {
                unitCost = Math.Round(input.PackCost.Value / item.PackSize.Value, 4);
            }
        }

        if (quantity <= 0)
        {
            throw new UserFriendlyException("The quantity must be greater than zero.");
        }

        var lot = new InventoryLot
        {
            ItemId = item.Id, Item = item, LotNumber = lotNumber, ReceivedDate = input.ReceivedDate, ExpiryDate = input.ExpiryDate, Supplier = input.Supplier,
        };
        lot.Id = await _lots.InsertAndGetIdAsync(lot);

        var zone = await Farm.GetTimeZoneAsync();
        var when = FarmClock.StartOfDayUtc(input.ReceivedDate, zone).AddHours(12);
        var movement = await _inventory.RecordReceiptAsync(lot, quantity, unitCost, input.ReceivedDate, when, input.Note);
        await CurrentUnitOfWork.SaveChangesAsync();
        return new ReceiptResultDto
        {
            LotId = lot.Id, MovementId = movement.Id,
            QuantityOnHand = await _inventory.GetLotQuantityAsync(lot.Id), UnitCost = lot.UnitCost,
        };
    }

    [AbpAuthorize(PermissionNames.Inventory_Manage)]
    public virtual async Task<decimal> RecordAdjustment(RecordAdjustmentInput input)
    {
        var lot = await _inventory.GetLotWithItemAsync(input.LotId);
        var zone = await Farm.GetTimeZoneAsync();
        var when = input.Date.HasValue ? FarmClock.StartOfDayUtc(input.Date.Value, zone).AddHours(12) : DateTime.UtcNow;
        await _inventory.RecordAdjustmentAsync(lot, input.Quantity, input.Reason.Trim(), when);
        await CurrentUnitOfWork.SaveChangesAsync();
        return await _inventory.GetLotQuantityAsync(lot.Id);
    }

    [AbpAuthorize(PermissionNames.Inventory_Manage)]
    public virtual async Task ClearReconciliationFlag(IdInput input)
    {
        var lot = await _lots.GetAsync(input.Id);
        lot.NeedsReconciliation = false;
    }

    public virtual async Task<List<StockMovementDto>> GetMovements(Guid lotId)
    {
        var lot = await _inventory.GetLotWithItemAsync(lotId);
        var list = await _movements.GetAll().AsNoTracking().Where(m => m.LotId == lotId).OrderBy(m => m.MovementDateUtc).ThenBy(m => m.CreationTime).ToListAsync();
        var batchIds = list.Where(m => m.BatchId.HasValue).Select(m => m.BatchId.Value).Distinct().ToList();
        var batchCodes = await _batches.GetAll().AsNoTracking().Where(b => batchIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.BatchCode);
        var names = await GetUserNamesAsync(list.Select(m => m.CreatorUserId));

        var sessionIds = list.Where(m => m.FeedingSessionId.HasValue).Select(m => m.FeedingSessionId.Value).Distinct().ToList();
        var sessions = await _sessions.GetAll().IgnoreQueryFilters().AsNoTracking().Where(x => sessionIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => $"{x.Session} feeding, {x.FeedDate:yyyy-MM-dd}");
        var logIds = list.Where(m => m.SourceLogId.HasValue).Select(m => m.SourceLogId.Value).Distinct().ToList();
        var logs = await _logs.GetAll().IgnoreQueryFilters().AsNoTracking().Where(x => logIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => Logbooks.LogAppService.Describe(x.LogType));
        string SourceOf(StockMovement m) =>
            m.FeedingSessionId.HasValue && sessions.TryGetValue(m.FeedingSessionId.Value, out var s) ? s
            : m.SourceLogId.HasValue && logs.TryGetValue(m.SourceLogId.Value, out var l) ? char.ToUpperInvariant(l[0]) + l[1..]
            : null;

        decimal running = 0;
        var result = new List<StockMovementDto>();
        foreach (var m in list)
        {
            running += m.Quantity;
            result.Add(new StockMovementDto
            {
                Id = m.Id, LotId = m.LotId, LotNumber = lot.LotNumber, ItemName = lot.Item.DisplayName, Unit = lot.Item.Unit, MovementDateUtc = m.MovementDateUtc,
                Type = m.Type, Quantity = m.Quantity, UnitCost = m.UnitCost, Reason = m.Reason, SourceLogId = m.SourceLogId,
                FeedingSessionId = m.FeedingSessionId, Source = SourceOf(m), BatchId = m.BatchId,
                BatchCode = m.BatchId.HasValue && batchCodes.TryGetValue(m.BatchId.Value, out var code) ? code : null,
                RunningBalance = running, RecordedBy = NameOf(names, m.CreatorUserId),
            });
        }

        return result;
    }
}
