using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.UI;
using Capstone.Authorization;
using Capstone.Common;
using Capstone.Sales;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Capstone.Resources;

public class BuyerDto
{
    public Guid Id { get; set; }

    public BuyerType BuyerType { get; set; } = BuyerType.Company;

    [Required, StringLength(Buyer.MaxNameLength)]
    public string Name { get; set; }

    [StringLength(Buyer.MaxShortLength)]
    public string ContactPerson { get; set; }

    [StringLength(50)]
    public string Phone { get; set; }

    [StringLength(Buyer.MaxAddressLength)]
    public string Address { get; set; }

    public bool IsActive { get; set; } = true;

    public bool PrivacyNoticeAcknowledged { get; set; }

    public DateTime? PrivacyNoticeAcknowledgedAtUtc { get; set; }

    public int SaleCount { get; set; }
    public decimal TotalSales { get; set; }
}

public class BuyerDetailDto : BuyerDto
{
    public List<BuyerSaleDto> Sales { get; set; } = new();
}

public class BuyerSaleDto
{
    public Guid Id { get; set; }
    public DateOnly SaleDate { get; set; }
    public string DispatchReceiptNo { get; set; }
    public decimal TotalKg { get; set; }
    public decimal TotalAmount { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public bool IsOverdue { get; set; }
}

[AbpAuthorize(PermissionNames.Buyers)]
public class BuyerAppService : CapstoneAppServiceBase
{
    private readonly IRepository<Buyer, Guid> _buyers;
    private readonly IRepository<Sale, Guid> _sales;

    public BuyerAppService(IRepository<Buyer, Guid> buyers, IRepository<Sale, Guid> sales)
    {
        _buyers = buyers;
        _sales = sales;
    }

    public virtual async Task<List<BuyerDto>> GetAll()
    {
        var sales = await _sales.GetAll().AsNoTracking().Include(s => s.Lines).ToListAsync();
        var totals = sales.GroupBy(s => s.BuyerId).Select(g => new { g.Key, N = g.Count(), Total = g.Sum(s => s.Total) }).ToList();
        var list = await _buyers.GetAll().AsNoTracking().OrderBy(b => b.Name).ToListAsync();
        return list.Select(b =>
        {
            var t = totals.FirstOrDefault(x => x.Key == b.Id);
            return new BuyerDto
            {
                Id = b.Id, BuyerType = b.BuyerType, Name = b.Name, ContactPerson = b.ContactPerson, Phone = b.Phone, Address = b.Address, IsActive = b.IsActive, PrivacyNoticeAcknowledgedAtUtc = b.PrivacyNoticeAcknowledgedAtUtc, PrivacyNoticeAcknowledged = b.PrivacyNoticeAcknowledgedAtUtc != null,
                SaleCount = t?.N ?? 0, TotalSales = t?.Total ?? 0,
            };
        }).ToList();
    }

    public virtual async Task<BuyerDetailDto> Get(Guid id)
    {
        var b = await _buyers.GetAsync(id);
        var sales = await _sales.GetAll().AsNoTracking().Include(s => s.Lines).Include(s => s.Payments).Where(s => s.BuyerId == id).OrderByDescending(s => s.SaleDate).ToListAsync();
        var today = await Farm.GetTodayAsync();
        return new BuyerDetailDto
        {
            Id = b.Id, BuyerType = b.BuyerType, Name = b.Name, ContactPerson = b.ContactPerson, Phone = b.Phone, Address = b.Address, IsActive = b.IsActive, PrivacyNoticeAcknowledgedAtUtc = b.PrivacyNoticeAcknowledgedAtUtc, PrivacyNoticeAcknowledged = b.PrivacyNoticeAcknowledgedAtUtc != null,
            SaleCount = sales.Count, TotalSales = sales.Sum(s => s.Total),
            Sales = sales.Select(s => new BuyerSaleDto
            {
                Id = s.Id, SaleDate = s.SaleDate, DispatchReceiptNo = s.DispatchReceiptNo, TotalKg = s.Lines.Sum(l => l.Kg), TotalAmount = s.Total, PaymentStatus = s.Status, IsOverdue = s.IsOverdue(today),
            }).ToList(),
        };
    }

    public virtual async Task<List<LookupDto>> GetLookup()
    {
        return await _buyers.GetAll().AsNoTracking().OrderBy(b => b.Name)
            .Select(b => new LookupDto { Id = b.Id, Name = b.Name, IsActive = b.IsActive }).ToListAsync();
    }

    public virtual async Task<Guid> Save(BuyerDto input)
    {
        var name = input.Name.Trim();
        if (await _buyers.GetAll().AnyAsync(b => b.Name == name && b.Id != input.Id))
        {
            throw new UserFriendlyException($"A buyer named '{name}' already exists.");
        }

        if (!Enum.IsDefined(input.BuyerType))
        {
            throw new UserFriendlyException("Choose Individual or Company.");
        }

        var b = input.Id == Guid.Empty ? new Buyer() : await _buyers.GetAsync(input.Id);
        if (input.BuyerType == BuyerType.Individual && !input.PrivacyNoticeAcknowledged && b.PrivacyNoticeAcknowledgedAtUtc == null)
        {
            throw new UserFriendlyException("Confirm that the buyer was told their name and contact details are kept for the farm's sales records (Data Privacy Act).");
        }

        b.BuyerType = input.BuyerType;
        b.Name = name;
        b.ContactPerson = input.BuyerType == BuyerType.Company ? input.ContactPerson : null;
        if (input.BuyerType == BuyerType.Individual)
        {
            b.PrivacyNoticeAcknowledgedAtUtc ??= DateTime.UtcNow;
        }
        else
        {
            b.PrivacyNoticeAcknowledgedAtUtc = null;
        }

        b.Phone = input.Phone;
        b.Address = input.Address;
        b.IsActive = input.IsActive;
        return input.Id == Guid.Empty ? await _buyers.InsertAndGetIdAsync(b) : b.Id;
    }

    public virtual async Task SetActive(SetActiveInput input)
    {
        var b = await _buyers.GetAsync(input.Id);
        b.IsActive = input.IsActive;
    }
}
