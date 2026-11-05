using Capstone.Domain;
using Capstone.Harvests;
using Capstone.Resources;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Capstone.Sales;

public enum PaymentStatus { Unpaid = 1, Partial = 2, Paid = 3 }

public class Sale : FarmEntity
{
    public const int MaxTextLength = 200;
    public const int MaxShortLength = 100;
    public const int MaxReceiptNoLength = 30;

    public Guid BuyerId { get; set; }
    public Buyer Buyer { get; set; }
    public DateOnly SaleDate { get; set; }

    public string DispatchReceiptNo { get; set; }

    public string PaymentTerms { get; set; }

    public DateOnly? DueDate { get; set; }

    public string Transporter { get; set; }
    public string DriverName { get; set; }
    public string PlateNo { get; set; }
    public DateTime? LoadedAtUtc { get; set; }
    public string Destination { get; set; }
    public string LocalTransportPermitNo { get; set; }
    public string TemperatureNote { get; set; }

    public string VoidReason { get; set; }

    public List<SaleLine> Lines { get; set; } = new();
    public List<SalePayment> Payments { get; set; } = new();

    public decimal Total => Lines.Sum(l => l.Amount);
    public decimal Paid => Payments.Sum(p => p.Amount);

    public PaymentStatus Status => Paid <= 0 ? PaymentStatus.Unpaid : Paid >= Total ? PaymentStatus.Paid : PaymentStatus.Partial;

    public bool IsOverdue(DateOnly today) => DueDate.HasValue && DueDate.Value < today && Status != PaymentStatus.Paid;
}

public class SaleLine : FarmChildEntity
{
    public Guid SaleId { get; set; }
    public Guid HarvestGradeLineId { get; set; }
    public HarvestGradeLine HarvestGradeLine { get; set; }
    public decimal Kg { get; set; }
    public decimal? BuyerAcceptedKg { get; set; }
    public decimal PricePerKg { get; set; }

    public decimal Amount => Math.Round((BuyerAcceptedKg ?? Kg) * PricePerKg, 2, MidpointRounding.AwayFromZero);
}

public class SalePayment : FarmEntity
{
    public const int MaxTextLength = 200;

    public Guid SaleId { get; set; }
    public DateOnly PaidOn { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; }
    public string Note { get; set; }
}
