using Capstone.Domain;
using System;

namespace Capstone.Inventory;

public enum InventoryCategory { Feed = 1, AdditiveSupplement = 2, Chemical = 3 }

public enum StockMovementType { Receipt = 1, Usage = 2, Adjustment = 3 }

public class InventoryItem : FarmEntity
{
    public const int MaxNameLength = 150;
    public const int MaxShortLength = 100;

    public InventoryCategory Category { get; set; }
    public string Brand { get; set; }
    public string ProductName { get; set; }
    public string FeedType { get; set; }
    public string BaiRegistrationNo { get; set; }
    public string Unit { get; set; } = "kg";
    public decimal? ReorderLevel { get; set; }
    public bool IsActive { get; set; } = true;

    public decimal? PackSize { get; set; }

    public string ActiveIngredient { get; set; }

    public int? DefaultWithdrawalDays { get; set; }

    public string DisplayName => string.IsNullOrWhiteSpace(Brand) ? ProductName : $"{Brand} {ProductName}";
}

public class InventoryLot : FarmEntity
{
    public const int MaxLotLength = 100;

    public Guid ItemId { get; set; }
    public InventoryItem Item { get; set; }
    public string LotNumber { get; set; }
    public DateOnly ReceivedDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public string Supplier { get; set; }

    public decimal? UnitCost { get; set; }

    public bool NeedsReconciliation { get; set; }
}

public class StockMovement : FarmEntity
{
    public const int MaxReasonLength = 500;

    public Guid LotId { get; set; }
    public InventoryLot Lot { get; set; }
    public DateTime MovementDateUtc { get; set; }
    public StockMovementType Type { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitCost { get; set; }
    public string Reason { get; set; }

    public Guid? SourceLogId { get; set; }

    public Guid? FeedingSessionId { get; set; }

    public Guid? BatchId { get; set; }
    public Guid? ReversesMovementId { get; set; }
}
