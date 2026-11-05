using Abp.Application.Services.Dto;
using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.UI;
using Capstone.Authorization;
using Capstone.Common;
using Capstone.Production.Dto;
using Capstone.Compliance;
using Capstone.Domain;
using Capstone.Harvests;
using Capstone.Production;
using Capstone.ReferenceData;
using Capstone.Resources;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Capstone.Sales;

public class SalePageDto : PagedResultDto<SaleDto>
{
    public decimal TotalKg { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalBalance { get; set; }
    public int OverdueCount { get; set; }
}

public class SaleLineInput
{
    public Guid HarvestGradeLineId { get; set; }

    [Range(0.001, 100000000, ErrorMessage = "Kg must be greater than zero.")]
    public decimal Kg { get; set; }

    [Range(0, 100000000, ErrorMessage = "The buyer's accepted kg cannot be negative.")]
    public decimal? BuyerAcceptedKg { get; set; }

    [Range(0, 100000000, ErrorMessage = "Price per kg cannot be negative.")]
    public decimal PricePerKg { get; set; }
}

public class PaymentInput
{
    public DateOnly PaidOn { get; set; }

    [Range(0.01, 1000000000, ErrorMessage = "A payment must be greater than zero.")]
    public decimal Amount { get; set; }

    [StringLength(50)]
    public string Method { get; set; }

    [StringLength(SalePayment.MaxTextLength)]
    public string Note { get; set; }
}

public class AddPaymentInput : PaymentInput
{
    public Guid SaleId { get; set; }
}

public class SaveSaleInput
{
    public Guid? Id { get; set; }
    public Guid BuyerId { get; set; }
    public DateOnly SaleDate { get; set; }

    [StringLength(Sale.MaxTextLength)]
    public string PaymentTerms { get; set; }

    public DateOnly? DueDate { get; set; }

    [StringLength(Sale.MaxShortLength)]
    public string Transporter { get; set; }

    [StringLength(Sale.MaxShortLength)]
    public string DriverName { get; set; }

    [StringLength(20)]
    public string PlateNo { get; set; }

    public DateTime? LoadedAt { get; set; }

    [StringLength(Sale.MaxTextLength)]
    public string Destination { get; set; }

    [StringLength(Sale.MaxShortLength)]
    public string LocalTransportPermitNo { get; set; }

    [StringLength(Sale.MaxTextLength)]
    public string TemperatureNote { get; set; }

    public List<SaleLineInput> Lines { get; set; } = new();

    public PaymentInput InitialPayment { get; set; }
}

public class VoidSaleInput
{
    public Guid Id { get; set; }

    [Required, StringLength(Sale.MaxTextLength)]
    public string Reason { get; set; }
}

public class SaleLineDto
{
    public Guid Id { get; set; }
    public Guid HarvestGradeLineId { get; set; }
    public Guid HarvestId { get; set; }
    public DateTime HarvestedAtUtc { get; set; }
    public Guid BatchId { get; set; }
    public string BatchCode { get; set; }
    public string PondCode { get; set; }
    public string CycleCode { get; set; }
    public string SizeLabel { get; set; }
    public decimal Kg { get; set; }
    public decimal? BuyerAcceptedKg { get; set; }

    public decimal? WeightDifferenceKg { get; set; }

    public decimal PricePerKg { get; set; }
    public decimal Amount { get; set; }
}

public class SalePaymentDto
{
    public Guid Id { get; set; }
    public DateOnly PaidOn { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; }
    public string Note { get; set; }
    public string RecordedBy { get; set; }
}

public class SaleDto
{
    public Guid Id { get; set; }
    public string DispatchReceiptNo { get; set; }
    public Guid BuyerId { get; set; }
    public string BuyerName { get; set; }
    public string BuyerContact { get; set; }
    public string BuyerPhone { get; set; }
    public string BuyerAddress { get; set; }
    public DateOnly SaleDate { get; set; }
    public string PaymentTerms { get; set; }
    public DateOnly? DueDate { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public bool IsOverdue { get; set; }
    public decimal Total { get; set; }
    public decimal Paid { get; set; }
    public decimal Balance { get; set; }

    public decimal Overpaid { get; set; }

    public decimal TotalKg { get; set; }
    public decimal? TotalBuyerKg { get; set; }
    public string Transporter { get; set; }
    public string DriverName { get; set; }
    public string PlateNo { get; set; }
    public DateTime? LoadedAtUtc { get; set; }
    public string Destination { get; set; }
    public string LocalTransportPermitNo { get; set; }
    public string TemperatureNote { get; set; }
    public List<SaleLineDto> Lines { get; set; } = new();
    public List<SalePaymentDto> Payments { get; set; } = new();
    public string RecordedBy { get; set; }
    public DateTime CreationTime { get; set; }

    public string Batches { get; set; }

    public string FarmName { get; set; }
    public string FarmAddress { get; set; }
    public string FarmContact { get; set; }

    public string BfarRegistrationNo { get; set; }

    public string SfscNos { get; set; }

    public string DocumentNotice { get; set; } = "Dispatch receipt for the farm's records. This is not an official receipt or invoice.";

    public List<WarningDto> Warnings { get; set; } = new();
}

public class SellableLineDto
{
    public Guid HarvestGradeLineId { get; set; }
    public Guid HarvestId { get; set; }
    public DateTime HarvestedAtUtc { get; set; }
    public DateOnly HarvestDate { get; set; }
    public Guid BatchId { get; set; }
    public string BatchCode { get; set; }
    public string PondCode { get; set; }
    public string SizeLabel { get; set; }
    public decimal Kg { get; set; }
    public decimal SoldKg { get; set; }
    public decimal UnsoldKg { get; set; }
}

public class GetSalesInput : PagedInput
{
    public Guid? BuyerId { get; set; }
    public Guid? BatchId { get; set; }
    public PaymentStatus? PaymentStatus { get; set; }
    public bool OverdueOnly { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

[AbpAuthorize(PermissionNames.Sales)]
public class SaleAppService : CapstoneAppServiceBase
{
    public const string BfarPreset = "BFAR Fishpond / Aquafarm Registration";

    private readonly IRepository<Sale, Guid> _sales;
    private readonly IRepository<SaleLine, Guid> _lines;
    private readonly IRepository<SalePayment, Guid> _payments;
    private readonly IRepository<HarvestGradeLine, Guid> _gradeLines;
    private readonly IRepository<Harvest, Guid> _harvests;
    private readonly IRepository<Buyer, Guid> _buyers;
    private readonly IRepository<ComplianceDocument, Guid> _documents;
    private readonly INumberSequenceGenerator _sequences;
    private readonly FarmStandardsProvider _standards;

    public SaleAppService(IRepository<Sale, Guid> sales, IRepository<SaleLine, Guid> lines, IRepository<SalePayment, Guid> payments,
        IRepository<HarvestGradeLine, Guid> gradeLines, IRepository<Harvest, Guid> harvests, IRepository<Buyer, Guid> buyers,
        IRepository<ComplianceDocument, Guid> documents, INumberSequenceGenerator sequences, FarmStandardsProvider standards)
    {
        _sales = sales;
        _lines = lines;
        _payments = payments;
        _gradeLines = gradeLines;
        _harvests = harvests;
        _buyers = buyers;
        _documents = documents;
        _sequences = sequences;
        _standards = standards;
    }

    private static readonly string[] SaleSortKeys = { "saleDate", "dispatchReceiptNo", "buyerName", "total", "paid", "balance", "paymentStatus" };

    public virtual async Task<SalePageDto> GetAll(GetSalesInput input)
    {
        var matching = await FilteredAsync(input);
        var sort = SortSpec.Parse(input.Sorting, SaleSortKeys, "saleDate", true);
        IOrderedEnumerable<SaleDto> ordered = sort.Key switch
        {
            "dispatchReceiptNo" => sort.Apply(matching, s => s.DispatchReceiptNo ?? string.Empty),
            "buyerName" => sort.Apply(matching, s => s.BuyerName ?? string.Empty),
            "total" => sort.Apply(matching, s => s.Total),
            "paid" => sort.Apply(matching, s => s.Paid),
            "balance" => sort.Apply(matching, s => s.Balance),
            "paymentStatus" => sort.Apply(matching, s => (int)s.PaymentStatus),
            _ => sort.Apply(matching, s => s.SaleDate),
        };
        var list = ordered.ThenByDescending(s => s.CreationTime).ToList();
        var page = list.ToPage(input);
        return new SalePageDto
        {
            TotalCount = page.TotalCount, Items = page.Items,
            TotalKg = list.Sum(s => s.TotalKg), TotalAmount = list.Sum(s => s.Total), TotalPaid = list.Sum(s => s.Paid), TotalBalance = list.Sum(s => s.Balance),
            OverdueCount = list.Count(s => s.IsOverdue),
        };
    }

    internal async Task<List<SaleDto>> ListAllAsync(GetSalesInput input) =>
        (await FilteredAsync(input)).OrderByDescending(s => s.SaleDate).ThenByDescending(s => s.CreationTime).ToList();

    private async Task<List<SaleDto>> FilteredAsync(GetSalesInput input)
    {
        var sales = await Query()
            .Where(s => !input.BuyerId.HasValue || s.BuyerId == input.BuyerId)
            .Where(s => !input.From.HasValue || s.SaleDate >= input.From)
            .Where(s => !input.To.HasValue || s.SaleDate <= input.To)
            .OrderByDescending(s => s.SaleDate).ThenByDescending(s => s.CreationTime)
            .ToListAsync();
        var list = await MapAsync(sales, detail: false);
        if (input.BatchId.HasValue)
        {
            list = list.Where(s => s.Lines.Any(l => l.BatchId == input.BatchId)).ToList();
        }

        return list
            .Where(s => SearchText.Matches(input.Search, s.DispatchReceiptNo, s.BuyerName, s.Batches))
            .Where(s => !input.PaymentStatus.HasValue || s.PaymentStatus == input.PaymentStatus)
            .Where(s => !input.OverdueOnly || s.IsOverdue)
            .ToList();
    }

    public virtual async Task<List<StatusCountDto>> GetPaymentStatusCounts(GetSalesInput input)
    {
        input.PaymentStatus = null;
        return (await FilteredAsync(input)).GroupBy(s => s.PaymentStatus).Select(g => new StatusCountDto { Status = (int)g.Key, Count = g.Count() }).ToList();
    }

    public virtual async Task<SaleDto> Get(Guid id)
    {
        var sale = await Query().FirstOrDefaultAsync(s => s.Id == id) ?? throw new UserFriendlyException("Sale not found.");
        return (await MapAsync(new List<Sale> { sale }, detail: true)).Single();
    }

    public virtual async Task<List<SellableLineDto>> GetSellableLines(Guid? saleId)
    {
        var zone = await Farm.GetTimeZoneAsync();
        var lines = await _gradeLines.GetAll().AsNoTracking().Include(g => g.SizeGrade).ToListAsync();
        var harvestIds = lines.Select(l => l.HarvestId).Distinct().ToList();
        var harvests = await _harvests.GetAll().AsNoTracking().Include(h => h.Batch).ThenInclude(b => b.Pond).Include(h => h.Disposals)
            .Where(h => harvestIds.Contains(h.Id)).ToDictionaryAsync(h => h.Id);
        var sold = await SoldByGradeLineAsync(lines.Select(l => l.Id).ToList(), saleId);
        var onSale = saleId.HasValue ? await _lines.GetAll().Where(l => l.SaleId == saleId).Select(l => l.HarvestGradeLineId).ToListAsync() : new List<Guid>();
        return lines.Where(l => harvests.ContainsKey(l.HarvestId))
            .Select(l =>
            {
                var h = harvests[l.HarvestId];
                var s = sold.TryGetValue(l.Id, out var kg) ? kg : 0m;
                return new SellableLineDto
                {
                    HarvestGradeLineId = l.Id, HarvestId = h.Id, HarvestedAtUtc = h.HarvestedAtUtc, HarvestDate = FarmClock.LocalDate(h.HarvestedAtUtc, zone),
                    BatchId = h.BatchId, BatchCode = h.Batch?.BatchCode, PondCode = h.Batch?.Pond?.PondCode, SizeLabel = l.SizeGrade?.Label, Kg = l.Kg, SoldKg = s, UnsoldKg = l.Kg - s,
                };
            })
            .Where(l => l.UnsoldKg > 0 || onSale.Contains(l.HarvestGradeLineId))
            .OrderBy(l => l.HarvestedAtUtc).ThenBy(l => l.SizeLabel)
            .ToList();
    }

    public virtual async Task<SaleDto> Create(SaveSaleInput input)
    {
        var sale = new Sale { Id = Guid.NewGuid() };
        var warnings = await ApplyAsync(sale, input, isNew: true);
        sale.DispatchReceiptNo = $"DR-{input.SaleDate:yyyy}-{await _sequences.NextAsync(NumberSequenceType.DispatchReceipt, input.SaleDate.ToString("yyyy")):0000}";
        await _sales.InsertAsync(sale);
        if (input.InitialPayment is { Amount: > 0 } p)
        {
            ValidatePayment(sale, p);
            await _payments.InsertAsync(new SalePayment { SaleId = sale.Id, PaidOn = p.PaidOn, Amount = p.Amount, Method = Clean(p.Method), Note = Clean(p.Note) });
        }

        await CurrentUnitOfWork.SaveChangesAsync();
        return await WithWarningsAsync(sale.Id, warnings);
    }

    public virtual async Task<SaleDto> Update(SaveSaleInput input)
    {
        if (!input.Id.HasValue)
        {
            throw new UserFriendlyException("Sale id is required.");
        }

        var sale = await _sales.GetAll().Include(s => s.Lines).Include(s => s.Payments).FirstOrDefaultAsync(s => s.Id == input.Id.Value)
                   ?? throw new UserFriendlyException("Sale not found.");
        var warnings = await ApplyAsync(sale, input, isNew: false);
        await CurrentUnitOfWork.SaveChangesAsync();
        return await WithWarningsAsync(sale.Id, warnings);
    }

    public virtual async Task<SaleDto> AddPayment(AddPaymentInput input)
    {
        var sale = await _sales.GetAll().Include(s => s.Lines).Include(s => s.Payments).FirstOrDefaultAsync(s => s.Id == input.SaleId)
                   ?? throw new UserFriendlyException("Sale not found.");
        ValidatePayment(sale, input);
        await _payments.InsertAsync(new SalePayment { SaleId = sale.Id, PaidOn = input.PaidOn, Amount = input.Amount, Method = Clean(input.Method), Note = Clean(input.Note) });
        await CurrentUnitOfWork.SaveChangesAsync();
        var warnings = new List<WarningDto>();
        var paid = sale.Payments.Sum(p => p.Amount) + input.Amount;
        if (paid > sale.Total)
        {
            warnings.Add(new WarningDto { Kind = "Overpayment", Message = $"Payments (₱{paid:N2}) are more than the sale total (₱{sale.Total:N2}); the difference shows as overpaid." });
        }

        return await WithWarningsAsync(sale.Id, warnings);
    }

    [AbpAuthorize(PermissionNames.Production_Manage)]
    public virtual async Task<SaleDto> RemovePayment(Abp.Application.Services.Dto.EntityDto<Guid> input)
    {
        var payment = await _payments.GetAsync(input.Id);
        await _payments.DeleteAsync(payment);
        await CurrentUnitOfWork.SaveChangesAsync();
        return await Get(payment.SaleId);
    }

    [AbpAuthorize(PermissionNames.Production_Manage)]
    public virtual async Task Void(VoidSaleInput input)
    {
        var sale = await _sales.GetAsync(input.Id);
        sale.VoidReason = input.Reason.Trim();
        await CurrentUnitOfWork.SaveChangesAsync();
        await _sales.DeleteAsync(sale);
    }

    private IQueryable<Sale> Query() => _sales.GetAll().AsNoTracking()
        .Include(s => s.Buyer).Include(s => s.Payments)
        .Include(s => s.Lines).ThenInclude(l => l.HarvestGradeLine).ThenInclude(g => g.SizeGrade);

    private static string Clean(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ValidatePayment(Sale sale, PaymentInput p)
    {
        if (p.Amount <= 0)
        {
            throw new UserFriendlyException("A payment must be greater than zero.");
        }

        if (p.PaidOn < sale.SaleDate)
        {
            throw new UserFriendlyException("A payment cannot be dated before the sale date.");
        }
    }

    private async Task<List<WarningDto>> ApplyAsync(Sale sale, SaveSaleInput input, bool isNew)
    {
        var warnings = new List<WarningDto>();
        var buyer = await _buyers.FirstOrDefaultAsync(b => b.Id == input.BuyerId) ?? throw new UserFriendlyException("Choose a buyer.");
        if (!buyer.IsActive && buyer.Id != sale.BuyerId)
        {
            throw new UserFriendlyException("The selected buyer is inactive.");
        }

        if (input.DueDate.HasValue && input.DueDate.Value < input.SaleDate)
        {
            throw new UserFriendlyException("The due date cannot be before the sale date.");
        }

        var lines = input.Lines ?? new List<SaleLineInput>();
        if (lines.Count == 0)
        {
            throw new UserFriendlyException("Add at least one grade line.");
        }

        if (lines.GroupBy(l => l.HarvestGradeLineId).Any(g => g.Count() > 1))
        {
            throw new UserFriendlyException("Each harvest grade can appear only once on a sale.");
        }

        if (!isNew && sale.Payments.Any(p => p.PaidOn < input.SaleDate))
        {
            throw new UserFriendlyException("A payment is dated before this sale date.");
        }

        var zone = await Farm.GetTimeZoneAsync();
        var gradeIds = lines.Select(l => l.HarvestGradeLineId).ToList();
        var gradeLines = await _gradeLines.GetAll().Include(g => g.SizeGrade).Where(g => gradeIds.Contains(g.Id)).ToListAsync();
        if (gradeLines.Count != gradeIds.Count)
        {
            throw new UserFriendlyException("Choose harvest grades of this farm for every line.");
        }

        var harvestIds = gradeLines.Select(g => g.HarvestId).Distinct().ToList();
        var harvests = await _harvests.GetAll().Include(h => h.Disposals).Include(h => h.GradeLines).Where(h => harvestIds.Contains(h.Id)).ToDictionaryAsync(h => h.Id);
        var exceptSale = isNew ? (Guid?)null : sale.Id;
        var soldElsewhere = await SoldByGradeLineAsync(harvests.Values.SelectMany(h => h.GradeLines).Select(g => g.Id).ToList(), exceptSale);
        var diffPct = await _standards.GetAsync(FarmThresholds.BuyerWeightDiffPct);

        foreach (var l in lines)
        {
            var grade = gradeLines.Single(g => g.Id == l.HarvestGradeLineId);
            var harvest = harvests[grade.HarvestId];
            if (input.SaleDate < FarmClock.LocalDate(harvest.HarvestedAtUtc, zone))
            {
                throw new UserFriendlyException($"The sale date cannot be before the harvest date of grade {grade.SizeGrade?.Label} ({FarmClock.LocalDate(harvest.HarvestedAtUtc, zone):yyyy-MM-dd}).");
            }

            var unsold = grade.Kg - (soldElsewhere.TryGetValue(grade.Id, out var s) ? s : 0m);
            if (l.Kg > unsold)
            {
                throw new UserFriendlyException($"Grade {grade.SizeGrade?.Label}: {l.Kg:0.###} kg is more than the {unsold:0.###} kg unsold.");
            }

            if (l.PricePerKg == 0)
            {
                warnings.Add(new WarningDto { Kind = "Price", Message = $"Grade {grade.SizeGrade?.Label} is priced at ₱0." });
            }

            if (l.BuyerAcceptedKg.HasValue && l.Kg > 0 && Math.Abs(l.BuyerAcceptedKg.Value - l.Kg) / l.Kg * 100m > diffPct)
            {
                warnings.Add(new WarningDto
                {
                    Kind = "BuyerWeight",
                    Message = $"Grade {grade.SizeGrade?.Label}: the buyer accepted {l.BuyerAcceptedKg:0.###} kg of {l.Kg:0.###} kg, more than {diffPct:0.#}% different. Check both weights.",
                });
            }
        }

        foreach (var h in harvests.Values)
        {
            var soldHere = lines.Where(l => h.GradeLines.Any(g => g.Id == l.HarvestGradeLineId)).Sum(l => l.Kg);
            var soldOther = h.GradeLines.Sum(g => soldElsewhere.TryGetValue(g.Id, out var kg) ? kg : 0m);
            var notSold = h.Disposals.Sum(d => d.Kg);
            if (soldHere + soldOther + notSold > h.TotalKg)
            {
                throw new UserFriendlyException($"The harvest of {FarmClock.LocalDate(h.HarvestedAtUtc, zone):yyyy-MM-dd} would have {soldHere + soldOther:0.###} kg sold and {notSold:0.###} kg not sold, more than its {h.TotalKg:0.###} kg.");
            }
        }

        sale.BuyerId = buyer.Id;
        sale.SaleDate = input.SaleDate;
        sale.PaymentTerms = Clean(input.PaymentTerms);
        sale.DueDate = input.DueDate;
        sale.Transporter = Clean(input.Transporter);
        sale.DriverName = Clean(input.DriverName);
        sale.PlateNo = Clean(input.PlateNo);
        sale.LoadedAtUtc = input.LoadedAt.HasValue ? FarmClock.ToUtc(input.LoadedAt.Value) : null;
        sale.Destination = Clean(input.Destination);
        sale.LocalTransportPermitNo = Clean(input.LocalTransportPermitNo);
        sale.TemperatureNote = Clean(input.TemperatureNote);

        foreach (var existing in sale.Lines.ToList())
        {
            sale.Lines.Remove(existing);
            await _lines.DeleteAsync(existing);
        }

        foreach (var l in lines)
        {
            sale.Lines.Add(new SaleLine { Id = Guid.NewGuid(), SaleId = sale.Id, HarvestGradeLineId = l.HarvestGradeLineId, Kg = l.Kg, BuyerAcceptedKg = l.BuyerAcceptedKg, PricePerKg = l.PricePerKg });
        }

        return warnings;
    }

    private async Task<SaleDto> WithWarningsAsync(Guid id, List<WarningDto> warnings)
    {
        var dto = await Get(id);
        dto.Warnings.AddRange(warnings);
        return dto;
    }

    private async Task<Dictionary<Guid, decimal>> SoldByGradeLineAsync(List<Guid> gradeLineIds, Guid? exceptSaleId)
    {
        return await (from l in _lines.GetAll()
                      join s in _sales.GetAll() on l.SaleId equals s.Id
                      where gradeLineIds.Contains(l.HarvestGradeLineId) && s.Id != exceptSaleId
                      group l by l.HarvestGradeLineId into g
                      select new { g.Key, Kg = g.Sum(x => x.Kg) }).ToDictionaryAsync(x => x.Key, x => x.Kg);
    }

    private async Task<List<SaleDto>> MapAsync(List<Sale> sales, bool detail)
    {
        var profile = await Farm.GetProfileAsync();
        var today = FarmClock.Today(await Farm.GetTimeZoneAsync());
        var names = await GetUserNamesAsync(sales.Select(s => s.CreatorUserId).Concat(sales.SelectMany(s => s.Payments).Select(p => p.CreatorUserId)));
        var harvestIds = sales.SelectMany(s => s.Lines).Select(l => l.HarvestGradeLine?.HarvestId ?? Guid.Empty).Distinct().ToList();
        var harvests = await _harvests.GetAll().AsNoTracking().Include(h => h.Batch).ThenInclude(b => b.Pond).Include(h => h.Batch).ThenInclude(b => b.Cycle).ThenInclude(c => c.FryDelivery)
            .Where(h => harvestIds.Contains(h.Id)).ToDictionaryAsync(h => h.Id);
        string bfar = null;
        if (detail)
        {
            var docs = await _documents.GetAll().AsNoTracking().Include(d => d.Preset).Where(d => !d.IsArchived && d.Preset != null && d.Preset.Name == BfarPreset).ToListAsync();
            bfar = docs.Where(d => d.IsCurrent(today)).OrderByDescending(d => d.IssueDate).Select(d => d.DocumentNumber).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
        }

        return sales.Select(s =>
        {
            var lines = s.Lines.Select(l =>
            {
                var h = l.HarvestGradeLine != null && harvests.TryGetValue(l.HarvestGradeLine.HarvestId, out var hv) ? hv : null;
                return new SaleLineDto
                {
                    Id = l.Id, HarvestGradeLineId = l.HarvestGradeLineId, HarvestId = h?.Id ?? Guid.Empty, HarvestedAtUtc = h?.HarvestedAtUtc ?? default, BatchId = h?.BatchId ?? Guid.Empty,
                    BatchCode = h?.Batch?.BatchCode, PondCode = h?.Batch?.Pond?.PondCode, CycleCode = h?.Batch?.Cycle?.CycleCode, SizeLabel = l.HarvestGradeLine?.SizeGrade?.Label,
                    Kg = l.Kg, BuyerAcceptedKg = l.BuyerAcceptedKg, WeightDifferenceKg = l.BuyerAcceptedKg.HasValue ? l.BuyerAcceptedKg - l.Kg : null,
                    PricePerKg = l.PricePerKg, Amount = l.Amount,
                };
            }).OrderBy(l => l.HarvestedAtUtc).ThenBy(l => l.SizeLabel).ToList();
            var sfscNos = detail
                ? string.Join(", ", s.Lines
                    .Select(l => l.HarvestGradeLine != null && harvests.TryGetValue(l.HarvestGradeLine.HarvestId, out var hx) ? hx.Batch?.Cycle?.FryDelivery?.SfscNo : null)
                    .Where(n => !string.IsNullOrWhiteSpace(n)).Distinct())
                : null;
            return new SaleDto
            {
                Id = s.Id, DispatchReceiptNo = s.DispatchReceiptNo, BuyerId = s.BuyerId, BuyerName = s.Buyer?.Name, BuyerContact = s.Buyer?.ContactPerson,
                BuyerPhone = s.Buyer?.Phone, BuyerAddress = s.Buyer?.Address, SaleDate = s.SaleDate, PaymentTerms = s.PaymentTerms, DueDate = s.DueDate,
                PaymentStatus = s.Status, IsOverdue = s.IsOverdue(today), Total = s.Total, Paid = s.Paid, Balance = Math.Max(0, s.Total - s.Paid), Overpaid = Math.Max(0, s.Paid - s.Total),
                TotalKg = lines.Sum(l => l.Kg), TotalBuyerKg = lines.Any(l => l.BuyerAcceptedKg.HasValue) ? lines.Sum(l => l.BuyerAcceptedKg ?? l.Kg) : null,
                Transporter = s.Transporter, DriverName = s.DriverName, PlateNo = s.PlateNo, LoadedAtUtc = s.LoadedAtUtc, Destination = s.Destination,
                LocalTransportPermitNo = s.LocalTransportPermitNo, TemperatureNote = s.TemperatureNote,
                Lines = lines,
                Payments = s.Payments.OrderBy(p => p.PaidOn).ThenBy(p => p.CreationTime)
                    .Select(p => new SalePaymentDto { Id = p.Id, PaidOn = p.PaidOn, Amount = p.Amount, Method = p.Method, Note = p.Note, RecordedBy = NameOf(names, p.CreatorUserId) }).ToList(),
                Batches = string.Join(", ", lines.Select(l => $"{l.PondCode} · {l.BatchCode}").Distinct()),
                RecordedBy = NameOf(names, s.CreatorUserId), CreationTime = s.CreationTime,
                FarmName = profile.FarmName, FarmAddress = profile.FormatAddress(),
                FarmContact = string.Join(" · ", new[] { profile.ContactName, profile.ContactPhone, profile.ContactEmail }.Where(x => !string.IsNullOrWhiteSpace(x))),
                BfarRegistrationNo = bfar,
                SfscNos = string.IsNullOrEmpty(sfscNos) ? null : sfscNos,
            };
        }).ToList();
    }
}
