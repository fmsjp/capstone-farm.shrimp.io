using Abp.Authorization;
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
using Capstone.Reports.Dto;
using Capstone.Resources;
using Capstone.Sales;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Capstone.Reports;

public enum ExportListKind { Sales = 1, Harvests = 2, FryDeliveries = 3, Batches = 4, Cycles = 5, Logs = 6, FeedingSessions = 7, Buyers = 8, Ponds = 9, InventoryItems = 10 }

public enum ExportReportKind { CyclePerformance = 1, BatchHarvestAndSurvival = 2, SalesByBuyerAndPeriod = 3, WaterQualityExceptions = 4, FeedAndAdditivesUsage = 5, BatchFailure = 6 }

public class ExportListInput
{
    public ExportListKind List { get; set; }
    public string Search { get; set; }
    public Guid? BuyerId { get; set; }
    public Guid? BatchId { get; set; }
    public Guid? CycleId { get; set; }
    public Guid? PondId { get; set; }
    public Guid? HatcheryId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public PaymentStatus? PaymentStatus { get; set; }
    public bool OverdueOnly { get; set; }
    public HarvestType? HarvestType { get; set; }
    public FryDeliveryStatus? DeliveryStatus { get; set; }
    public BatchStatus? BatchStatus { get; set; }
    public CycleStatus? CycleStatus { get; set; }
    public bool OpenOnly { get; set; }
    public LogType? LogType { get; set; }
    public bool OutOfRangeOnly { get; set; }
}

public class ExportReportInput
{
    public ExportReportKind Report { get; set; }
    public Guid? CycleId { get; set; }
    public Guid? BatchId { get; set; }
    public Guid? BuyerId { get; set; }
    public Guid? ItemId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public class ExportFileDto
{
    public string FileName { get; set; }
    public string ContentType { get; set; } = "text/csv; charset=utf-8";

    public string ContentBase64 { get; set; }

    public int RowCount { get; set; }
}

[AbpAuthorize(PermissionNames.Reports)]
public class ExportAppService : CapstoneAppServiceBase
{
    public const int MaxRows = 100_000;

    private readonly SaleAppService _sales;
    private readonly HarvestAppService _harvests;
    private readonly FryDeliveryAppService _deliveries;
    private readonly BatchAppService _batches;
    private readonly CycleAppService _cycles;
    private readonly LogAppService _logs;
    private readonly FeedingAppService _feeding;
    private readonly BuyerAppService _buyers;
    private readonly PondAppService _ponds;
    private readonly InventoryAppService _inventory;
    private readonly ReportAppService _reports;

    public ExportAppService(SaleAppService sales, HarvestAppService harvests, FryDeliveryAppService deliveries, BatchAppService batches,
        CycleAppService cycles, LogAppService logs, FeedingAppService feeding, BuyerAppService buyers, PondAppService ponds,
        InventoryAppService inventory, ReportAppService reports)
    {
        _sales = sales;
        _harvests = harvests;
        _deliveries = deliveries;
        _batches = batches;
        _cycles = cycles;
        _logs = logs;
        _feeding = feeding;
        _buyers = buyers;
        _ponds = ponds;
        _inventory = inventory;
        _reports = reports;
    }

    public virtual async Task<ExportFileDto> ExportList(ExportListInput input)
    {
        if (!Enum.IsDefined(input.List))
        {
            throw new UserFriendlyException("Choose a list to export.");
        }

        await PermissionChecker.AuthorizeAsync(ListPermission(input.List));
        var zone = await Farm.GetTimeZoneAsync();
        var (csv, name) = input.List switch
        {
            ExportListKind.Sales => (await SalesAsync(input, zone), "Sales"),
            ExportListKind.Harvests => (await HarvestsAsync(input, zone), "Harvests"),
            ExportListKind.FryDeliveries => (await DeliveriesAsync(input, zone), "FryDeliveries"),
            ExportListKind.Batches => (await BatchesAsync(input, zone), "Batches"),
            ExportListKind.Cycles => (await CyclesAsync(input, zone), "Cycles"),
            ExportListKind.Logs => (await LogsAsync(input, zone), "ProductionLogs"),
            ExportListKind.FeedingSessions => (await FeedingAsync(input, zone), "FeedingSessions"),
            ExportListKind.Buyers => (await BuyersAsync(zone), "Buyers"),
            ExportListKind.Ponds => (await PondsAsync(zone), "Ponds"),
            _ => (await InventoryItemsAsync(zone), "InventoryItems"),
        };
        return await FileAsync(csv, $"{{0}}_{name}_{{1}}.csv", zone);
    }

    private static string ListPermission(ExportListKind kind) => kind switch
    {
        ExportListKind.Sales => PermissionNames.Sales,
        ExportListKind.Harvests => PermissionNames.Harvests,
        ExportListKind.FryDeliveries => PermissionNames.FryDeliveries,
        ExportListKind.Batches or ExportListKind.Cycles => PermissionNames.Production,
        ExportListKind.Logs or ExportListKind.FeedingSessions => PermissionNames.Logs,
        ExportListKind.Buyers => PermissionNames.Buyers,
        ExportListKind.Ponds => PermissionNames.Ponds,
        _ => PermissionNames.Inventory,
    };

    private async Task<CsvBuilder> SalesAsync(ExportListInput i, TimeZoneInfo zone)
    {
        var rows = await _sales.ListAllAsync(new GetSalesInput { Search = i.Search, BuyerId = i.BuyerId, BatchId = i.BatchId, PaymentStatus = i.PaymentStatus, OverdueOnly = i.OverdueOnly, From = i.From, To = i.To });
        var csv = new CsvBuilder(zone, "Dispatch receipt", "Sale date", "Buyer", "Batches", "Kg", "Buyer kg", "Amount", "Paid", "Balance", "Payment status", "Overdue", "Due date", "Payment terms", "Recorded by");
        foreach (var s in Capped(rows))
        {
            csv.Add(s.DispatchReceiptNo, s.SaleDate, s.BuyerName, s.Batches, CsvNumber.Of(s.TotalKg, 3), CsvNumber.Of(s.TotalBuyerKg, 3), s.Total, s.Paid, s.Balance, s.PaymentStatus, s.IsOverdue, s.DueDate, s.PaymentTerms, s.RecordedBy);
        }

        return csv;
    }

    private async Task<CsvBuilder> HarvestsAsync(ExportListInput i, TimeZoneInfo zone)
    {
        var rows = await _harvests.ListAllAsync(new GetHarvestsInput { Search = i.Search, BatchId = i.BatchId, HarvestType = i.HarvestType });
        var csv = new CsvBuilder(zone, "Harvested at", "Cycle", "Pond", "Batch", "Type", "Total kg", "Graded kg", "Sold kg", "Not sold kg", "Unaccounted kg", "Boxes", "ABW g", "Final count", "Dead at harvest", "Recorded by");
        foreach (var h in Capped(rows))
        {
            csv.Add(h.HarvestedAtUtc, h.CycleCode, h.PondCode, h.BatchCode, h.HarvestType, CsvNumber.Of(h.TotalKg, 3), CsvNumber.Of(h.GradedKg, 3), CsvNumber.Of(h.SoldKg, 3), CsvNumber.Of(h.NotSoldKg, 3),
                CsvNumber.Of(h.UnaccountedKg, 3), h.Boxes, CsvNumber.Of(h.AbwG), h.FinalCount, h.DeadAtHarvest, h.RecordedBy);
        }

        return csv;
    }

    private async Task<CsvBuilder> DeliveriesAsync(ExportListInput i, TimeZoneInfo zone)
    {
        var rows = await _deliveries.ListAllAsync(new GetFryDeliveriesInput { Search = i.Search, HatcheryId = i.HatcheryId, Status = i.DeliveryStatus });
        var csv = new CsvBuilder(zone, "Delivery date", "Hatchery", "Hatchery code", "Hatchery lot", "Status", "PL received", "PL stocked", "PL not stocked", "PL left", "PL age days", "Cost", "Cycle", "Health certificate", "Transport permit (DTP/LTP)", "SFSC no.");
        foreach (var d in Capped(rows))
        {
            csv.Add(d.DeliveryDate, d.HatcheryName, d.HatcheryShortCode, d.HatcheryBatchNumber, d.Status, d.PlReceived, d.PlStocked, d.PlNotStocked, d.PlLeft, d.PlAgeDays, d.CostPhp, d.CycleCode, d.PlHealthCertificateNo, d.TransportPermitNo, d.SfscNo);
        }

        return csv;
    }

    private async Task<CsvBuilder> BatchesAsync(ExportListInput i, TimeZoneInfo zone)
    {
        var rows = await _batches.GetAll(new GetBatchesInput { Search = i.Search, CycleId = i.CycleId, PondId = i.PondId, Status = i.BatchStatus, OpenOnly = i.OpenOnly });
        var csv = new CsvBuilder(zone, "Batch", "Cycle", "Pond", "Status", "Stocking date", "PL stocked", "DOC", "ABW g", "FCR", "Survival %");
        foreach (var b in Capped(rows))
        {
            csv.Add(b.BatchCode, b.CycleCode, b.PondCode, b.Status, b.StockingDate, b.PlCountStocked, b.Doc, CsvNumber.Of(b.CurrentAbwG), CsvNumber.Of(b.FcrToDate, 3), CsvNumber.Of(b.EstimatedSurvivalPct, 1));
        }

        return csv;
    }

    private async Task<CsvBuilder> CyclesAsync(ExportListInput i, TimeZoneInfo zone)
    {
        var rows = (await _cycles.GetAll(new GetCyclesInput { Status = i.CycleStatus }))
            .Where(c => SearchText.Matches(i.Search, c.CycleCode, c.HatcheryName, c.HatcheryShortCode, c.HatcheryBatchNumber)).ToList();
        var csv = new CsvBuilder(zone, "Cycle", "Status", "Outcome", "Hatchery", "Hatchery code", "Hatchery lot", "Start date", "Batches", "Open batches", "PL received", "PL stocked", "PL left");
        foreach (var c in Capped(rows))
        {
            csv.Add(c.CycleCode, c.Status, c.Outcome, c.HatcheryName, c.HatcheryShortCode, c.HatcheryBatchNumber, c.StartDate, c.BatchCount, c.OpenBatchCount, c.PlReceived, c.PlStocked, c.PlLeft);
        }

        return csv;
    }

    private async Task<CsvBuilder> LogsAsync(ExportListInput i, TimeZoneInfo zone)
    {
        var rows = await _logs.ListAllAsync(new GetLogsInput { Search = i.Search, BatchId = i.BatchId, LogType = i.LogType, From = i.From, To = i.To, OutOfRangeOnly = i.OutOfRangeOnly });
        var csv = new CsvBuilder(zone, "Observed at", "Pond", "Batch", "Type", "Details", "Out of range", "Notes", "Recorded by", "Recorded at");
        foreach (var l in Capped(rows))
        {
            csv.Add(l.ObservedAtUtc, l.PondCode, l.BatchCode, l.LogType, LogDetails(l), l.HasOutOfRange, l.Notes, l.RecordedBy, l.CreationTime);
        }

        return csv;
    }

    internal static string LogDetails(LogDto l)
    {
        var parts = new List<string>();
        if (l.Measurements.Count > 0)
        {
            parts.Add(string.Join("; ", l.Measurements.Select(m => $"{m.ParameterName ?? m.ParameterCode} {m.Value:0.###} {m.UnitCode}{(m.IsOutOfRange ? " (out of range)" : string.Empty)}")));
        }

        if (l.Mortality != null)
        {
            parts.Add($"{l.Mortality.Count:N0} dead, cause {l.Mortality.Cause}");
        }

        if (l.AbwG.HasValue)
        {
            parts.Add($"ABW {l.AbwG:0.##} g");
        }

        if (l.ChemicalApplication != null)
        {
            parts.Add($"treatment: {l.ChemicalApplication.Reason}");
        }

        if (l.PondPreparation != null)
        {
            parts.Add($"preparation: {l.PondPreparation.ActivityType}");
        }

        if (l.PostProduction != null)
        {
            parts.Add($"post-production: {l.PostProduction.ActivityType}");
        }

        if (l.Usages.Count > 0)
        {
            parts.Add("used " + string.Join(", ", l.Usages.Select(u => $"{u.Label} {u.Quantity:0.###} {u.Unit}")));
        }

        return string.Join(" | ", parts);
    }

    private async Task<CsvBuilder> FeedingAsync(ExportListInput i, TimeZoneInfo zone)
    {
        var rows = await _feeding.ListAllAsync(new GetFeedingSessionsInput { BatchId = i.BatchId, CycleId = i.CycleId, From = i.From, To = i.To });
        var csv = new CsvBuilder(zone, "Feed date", "Session", "Pond", "Batch", "Status", "Kg", "Feed lots", "Time fed", "Notes", "Recorded by", "Recorded at");
        foreach (var s in Capped(rows))
        {
            csv.Add(s.FeedDate, s.Session, s.PondCode, s.BatchCode, s.Status, CsvNumber.Of(s.Kg, 3), string.Join("; ", s.Lines.Select(l => $"{l.Label} {l.Kg:0.###} kg")), s.FedAtTime, s.Notes, s.RecordedBy, s.CreationTime);
        }

        return csv;
    }

    private async Task<CsvBuilder> BuyersAsync(TimeZoneInfo zone)
    {
        var rows = await _buyers.GetAll();
        var csv = new CsvBuilder(zone, "Buyer", "Type", "Contact person", "Phone", "Address", "Active", "Sales", "Total sales");
        foreach (var b in Capped(rows))
        {
            csv.Add(b.Name, b.BuyerType, b.ContactPerson, b.Phone, b.Address, b.IsActive, b.SaleCount, b.TotalSales);
        }

        return csv;
    }

    private async Task<CsvBuilder> PondsAsync(TimeZoneInfo zone)
    {
        var rows = await _ponds.GetAll(includeArchived: true);
        var csv = new CsvBuilder(zone, "Pond code", "Name", "Area m2", "Average depth m", "Max depth m", "Purpose", "Construction", "Farming system", "Status", "Current batch");
        foreach (var p in Capped(rows))
        {
            csv.Add(p.PondCode, p.Name, CsvNumber.Of(p.AreaM2), CsvNumber.Of(p.AverageDepthM), CsvNumber.Of(p.MaxDepthM), p.Purpose, p.ConstructionType, p.FarmingSystem, p.Status, p.CurrentBatchCode);
        }

        return csv;
    }

    private async Task<CsvBuilder> InventoryItemsAsync(TimeZoneInfo zone)
    {
        var rows = await _inventory.GetItems();
        var csv = new CsvBuilder(zone, "Item", "Category", "Unit", "On hand", "Reorder level", "Low stock", "Negative stock", "Lots", "Active");
        foreach (var x in Capped(rows))
        {
            csv.Add(x.DisplayName, x.Category, x.Unit, CsvNumber.Of(x.QuantityOnHand, 3), CsvNumber.Of(x.ReorderLevel, 3), x.IsLowStock, x.IsNegativeStock, x.LotCount, x.IsActive);
        }

        return csv;
    }

    public virtual async Task<ExportFileDto> ExportReport(ExportReportInput input)
    {
        if (!Enum.IsDefined(input.Report))
        {
            throw new UserFriendlyException("Choose a report to export.");
        }

        var zone = await Farm.GetTimeZoneAsync();
        CsvBuilder csv;
        string name;
        string record = null;
        switch (input.Report)
        {
            case ExportReportKind.CyclePerformance:
            {
                var r = await _reports.GetCyclePerformance(input.CycleId ?? throw new UserFriendlyException("Choose a cycle."));
                name = "ProductionCyclePerformance";
                record = r.CycleCode;
                csv = new CsvBuilder(zone, "Cycle", "Hatchery", "Hatchery code", "Hatchery lot", "Pond", "Batch", "Status", "Stocking date", "PL stocked", "DOC", "Culture days", "ABW g", "FCR", "Survival %",
                    "Feed kg", "Harvest kg", "Revenue", "Inventory cost", "PL cost", "Margin over feed, chemicals and PL");
                foreach (var b in r.Batches)
                {
                    var ind = b.Indicators;
                    csv.Add(r.CycleCode, r.HatcheryName, r.HatcheryShortCode, r.HatcheryLotNumber, b.PondCode, b.BatchCode, b.Status, b.StockingDate, b.PlCountStocked, ind?.Doc, ind?.CultureDays,
                        CsvNumber.Of(ind?.FinalAbwG ?? ind?.CurrentAbwG), CsvNumber.Of(ind?.FinalFcr ?? ind?.FcrToDate, 3), CsvNumber.Of(ind?.SurvivalRatePct ?? ind?.EstimatedSurvivalPct, 1),
                        CsvNumber.Of(ind?.CumulativeFeedKg, 3), CsvNumber.Of(ind?.TotalHarvestKg, 3), b.Finance.Revenue, b.Finance.InventoryUsageCost, b.Finance.PlCost, b.Finance.Margin);
                }

                break;
            }

            case ExportReportKind.BatchHarvestAndSurvival:
            {
                var r = await _reports.GetHarvestSummary(new HarvestSummaryInput { CycleId = input.CycleId, BatchId = input.BatchId, From = input.From, To = input.To });
                name = "BatchHarvestAndSurvival";
                record = input.BatchId.HasValue ? r.Batches.FirstOrDefault()?.BatchCode : null;
                csv = new CsvBuilder(zone, "Date", "Cycle", "Pond", "Batch", "Type", "Total kg", "Pieces", "ABW g", "Boxes", "Sizes", "Quality checks");
                foreach (var h in r.Harvests)
                {
                    csv.Add(h.Date, h.CycleCode, h.PondCode, h.BatchCode, h.HarvestType, CsvNumber.Of(h.TotalKg, 3), CsvNumber.Of(h.Pieces, 0), CsvNumber.Of(h.AbwG), h.Boxes,
                        string.Join("; ", h.Grades.Select(g => $"{g.SizeLabel} {g.Kg:0.###} kg")), h.QualityChecks);
                }

                break;
            }

            case ExportReportKind.SalesByBuyerAndPeriod:
            {
                var r = await _reports.GetSalesByBuyer(new SalesByBuyerInput { BuyerId = input.BuyerId, From = input.From, To = input.To });
                name = "SalesByBuyerAndPeriod";
                csv = new CsvBuilder(zone, "Dispatch receipt", "Sale date", "Buyer", "Kg", "Amount", "Paid", "Balance", "Payment status", "Overdue", "Due date");
                foreach (var s in r.Sales)
                {
                    csv.Add(s.DispatchReceiptNo, s.SaleDate, s.BuyerName, CsvNumber.Of(s.Kg, 3), s.Amount, s.Paid, s.Balance, s.PaymentStatus, s.IsOverdue, s.DueDate);
                }

                break;
            }

            case ExportReportKind.WaterQualityExceptions:
            {
                var r = await _reports.GetWaterQualityExceptions(new WaterQualityExceptionInput { BatchId = input.BatchId, From = input.From, To = input.To });
                name = "WaterQualityExceptions";
                csv = new CsvBuilder(zone, "Observed at", "Pond", "Batch", "Parameter", "Value", "Unit", "Optimal range", "Recorded by");
                foreach (var x in r.Items)
                {
                    csv.Add(x.ObservedAtUtc, x.PondCode, x.BatchCode, x.ParameterName, CsvNumber.Of(x.Value, 3), x.UnitCode, x.OptimalRange, x.RecordedBy);
                }

                break;
            }

            case ExportReportKind.FeedAndAdditivesUsage:
            {
                var r = await _reports.GetInventoryMovements(new InventoryMovementInput { ItemId = input.ItemId, From = input.From, To = input.To });
                name = "FeedAndAdditivesUsage";
                csv = new CsvBuilder(zone, "Item", "Category", "Unit", "Opening", "Receipts", "Usage", "Adjustments", "Closing", "Usage cost");
                foreach (var x in r.Items)
                {
                    csv.Add(x.ItemName, x.Category, x.Unit, CsvNumber.Of(x.Opening, 3), CsvNumber.Of(x.Receipts, 3), CsvNumber.Of(x.Usage, 3), CsvNumber.Of(x.Adjustments, 3), CsvNumber.Of(x.Closing, 3), x.UsageCost);
                }

                break;
            }

            default:
            {
                var r = await _reports.GetFailureReport(new FailureReportInput { BatchId = input.BatchId, CycleId = input.CycleId });
                name = "BatchFailureReport";
                record = input.BatchId.HasValue ? r.Batches.FirstOrDefault()?.BatchCode : r.CycleCode;
                csv = new CsvBuilder(zone, "Cycle", "Hatchery", "Hatchery code", "Pond", "Batch", "Stocking date", "PL stocked", "Hatchery lot", "Failure date", "Reason", "Estimated loss", "DOC at failure", "Mortality events", "Treatments", "Inventory cost");
                foreach (var b in r.Batches)
                {
                    csv.Add(r.CycleCode, r.HatcheryName, r.HatcheryShortCode, b.PondCode, b.BatchCode, b.StockingDate, b.PlCountStocked, b.HatcheryLotNumber, b.FailureDate, b.FailureReason, b.EstimatedLossPhp,
                        b.DocAtFailure, b.MortalityEvents.Count, b.Treatments.Count, b.InventoryUsageCost);
                }

                break;
            }
        }

        string pattern;
        if (record != null && (input.Report is ExportReportKind.CyclePerformance or ExportReportKind.BatchFailure || input.BatchId.HasValue))
        {
            pattern = $"{{0}}_{name}_{record}.csv";
        }
        else
        {
            var from = input.From?.ToString("yyyy-MM-dd") ?? "start";
            var to = input.To?.ToString("yyyy-MM-dd") ?? "today";
            pattern = $"{{0}}_{name}_{from}_{to}_{{1}}.csv";
        }

        return await FileAsync(csv, pattern, zone);
    }

    private static IEnumerable<T> Capped<T>(IReadOnlyCollection<T> rows)
    {
        if (rows.Count > MaxRows)
        {
            throw new UserFriendlyException($"This list has {rows.Count:N0} rows; one export holds at most {MaxRows:N0}. Narrow the dates and export again.");
        }

        return rows;
    }

    private async Task<ExportFileDto> FileAsync(CsvBuilder csv, string pattern, TimeZoneInfo zone)
    {
        var tenant = await GetCurrentTenantAsync();
        var stamp = FarmClock.ToLocal(DateTime.UtcNow, zone).ToString("yyyyMMdd-HHmm");
        var farmCode = tenant.TenancyName.ToUpperInvariant();
        return new ExportFileDto
        {
            FileName = string.Format(pattern, farmCode, stamp),
            ContentBase64 = Convert.ToBase64String(csv.ToBytes()),
            RowCount = csv.RowCount,
        };
    }
}
