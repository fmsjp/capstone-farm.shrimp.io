using Abp.Application.Services.Dto;
using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.UI;
using Capstone.Authorization;
using Capstone.Common;
using Capstone.Production.Dto;
using Capstone.Domain;
using Capstone.Production;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Capstone.Resources;

public class FryDeliveryEditDto
{
    public Guid Id { get; set; }
    public Guid HatcheryId { get; set; }

    [Required, StringLength(FryDelivery.MaxBatchNumberLength)]
    public string HatcheryBatchNumber { get; set; }

    public DateOnly DeliveryDate { get; set; }

    [Range(1, 60, ErrorMessage = "PL age must be between 1 and 60 days.")]
    public int? PlAgeDays { get; set; }

    [Range(1, FryDelivery.MaxPl, ErrorMessage = "PL received must be between 1 and 10,000,000.")]
    public long PlReceived { get; set; }

    [Range(0, 1000000000)]
    public decimal? CostPhp { get; set; }

    [StringLength(FryDelivery.MaxNotesLength)]
    public string Notes { get; set; }

    [StringLength(FryDelivery.MaxPermitLength)]
    public string PlHealthCertificateNo { get; set; }

    [StringLength(FryDelivery.MaxPermitLength)]
    public string TransportPermitNo { get; set; }

    [StringLength(FryDelivery.MaxPermitLength)]
    public string SfscNo { get; set; }

    public FryDeliveryStatus InitialStatus { get; set; } = FryDeliveryStatus.Ordered;

    public string RowVersion { get; set; }
}

public class FryDeliveryDto
{
    public Guid Id { get; set; }
    public Guid HatcheryId { get; set; }
    public string HatcheryName { get; set; }
    public string HatcheryShortCode { get; set; }
    public string HatcheryBatchNumber { get; set; }
    public DateOnly DeliveryDate { get; set; }
    public int? PlAgeDays { get; set; }
    public long PlReceived { get; set; }
    public decimal? CostPhp { get; set; }
    public string Notes { get; set; }
    public string PlHealthCertificateNo { get; set; }
    public string TransportPermitNo { get; set; }
    public string SfscNo { get; set; }
    public FryDeliveryStatus Status { get; set; }

    public bool IsStockingFinished { get; set; }

    public DateTime? StockingFinishedAtUtc { get; set; }

    public long PlNotStocked { get; set; }
    public DateTime StatusChangedAtUtc { get; set; }

    public bool IsStocked { get; set; }

    public Guid? CycleId { get; set; }
    public string CycleCode { get; set; }
    public long PlStocked { get; set; }
    public long PlLeft { get; set; }
    public string RowVersion { get; set; }
}

public class FryDeliveryDetailDto : FryDeliveryDto
{
    public List<FryDeliveryStatusChangeDto> History { get; set; } = new();
    public List<ShortfallDto> Shortfalls { get; set; } = new();
    public string StockingFinishedBy { get; set; }
    public bool CanEdit { get; set; }
    public bool CanUpdateStatus { get; set; }

    public bool CanFinishStocking { get; set; }
}

public class ShortfallDto
{
    public ShortfallReason Reason { get; set; }
    public long Count { get; set; }
    public string Note { get; set; }
}

public class ShortfallInput
{
    public ShortfallReason Reason { get; set; }

    [Range(1, FryDelivery.MaxPl)]
    public long Count { get; set; }

    [StringLength(FryDeliveryShortfall.MaxNoteLength)]
    public string Note { get; set; }
}

public class FinishStockingInput
{
    public Guid DeliveryId { get; set; }

    public List<ShortfallInput> Shortfalls { get; set; } = new();

    public string RowVersion { get; set; }
}

public class OpenDeliveryDto
{
    public Guid Id { get; set; }
    public string HatcheryBatchNumber { get; set; }
    public string HatcheryName { get; set; }
    public DateOnly DeliveryDate { get; set; }
    public long PlReceived { get; set; }
    public long PlStocked { get; set; }
    public Guid? CycleId { get; set; }
    public string CycleCode { get; set; }
}

public class FryDeliveryStatusChangeDto
{
    public FryDeliveryStatus Status { get; set; }
    public DateTime ChangedAtUtc { get; set; }
    public string ChangedBy { get; set; }
    public string Note { get; set; }
}

public class UpdateFryDeliveryStatusInput
{
    public Guid DeliveryId { get; set; }
    public FryDeliveryStatus Status { get; set; }

    public DateTime? ChangedAt { get; set; }

    [StringLength(FryDeliveryStatusChange.MaxNoteLength)]
    public string Note { get; set; }
}

public class GetFryDeliveriesInput : PagedInput
{
    public Guid? HatcheryId { get; set; }
    public FryDeliveryStatus? Status { get; set; }

    public bool AvailableForStocking { get; set; }
}

public class SaveFryDeliveryResult
{
    public Guid Id { get; set; }
    public List<WarningDto> Warnings { get; set; } = new();
}

[AbpAuthorize(PermissionNames.FryDeliveries)]
public class FryDeliveryAppService : CapstoneAppServiceBase
{
    private readonly IRepository<FryDelivery, Guid> _deliveries;
    private readonly IRepository<FryDeliveryStatusChange, Guid> _changes;
    private readonly IRepository<HatcherySource, Guid> _hatcheries;
    private readonly IRepository<ProductionCycle, Guid> _cycles;
    private readonly IRepository<PondBatch, Guid> _batches;
    private readonly IRepository<FryDeliveryShortfall, Guid> _shortfalls;
    private readonly ReferenceData.FarmStandardsProvider _standards;

    public FryDeliveryAppService(IRepository<FryDelivery, Guid> deliveries, IRepository<FryDeliveryStatusChange, Guid> changes,
        IRepository<HatcherySource, Guid> hatcheries, IRepository<ProductionCycle, Guid> cycles, IRepository<PondBatch, Guid> batches,
        IRepository<FryDeliveryShortfall, Guid> shortfalls, ReferenceData.FarmStandardsProvider standards)
    {
        _shortfalls = shortfalls;
        _standards = standards;
        _deliveries = deliveries;
        _changes = changes;
        _hatcheries = hatcheries;
        _cycles = cycles;
        _batches = batches;
    }

    private static readonly string[] DeliverySortKeys = { "deliveryDate", "hatcheryName", "hatcheryBatchNumber", "plReceived", "status" };

    public virtual async Task<PagedResultDto<FryDeliveryDto>> GetAll(GetFryDeliveriesInput input)
    {
        var list = await FilteredAsync(input);
        var sort = SortSpec.Parse(input.Sorting, DeliverySortKeys, "deliveryDate", true);
        IOrderedEnumerable<FryDeliveryDto> ordered = sort.Key switch
        {
            "hatcheryName" => sort.Apply(list, d => d.HatcheryName ?? string.Empty),
            "hatcheryBatchNumber" => sort.Apply(list, d => d.HatcheryBatchNumber ?? string.Empty),
            "plReceived" => sort.Apply(list, d => d.PlReceived),
            "status" => sort.Apply(list, d => (int)d.Status),
            _ => sort.Apply(list, d => d.DeliveryDate),
        };
        return ordered.ThenBy(d => d.HatcheryBatchNumber).ToList().ToPage(input);
    }

    internal async Task<List<FryDeliveryDto>> ListAllAsync(GetFryDeliveriesInput input) =>
        (await FilteredAsync(input)).OrderByDescending(d => d.DeliveryDate).ThenBy(d => d.HatcheryBatchNumber).ToList();

    private async Task<List<FryDeliveryDto>> FilteredAsync(GetFryDeliveriesInput input)
    {
        var deliveries = await _deliveries.GetAll().AsNoTracking().Include(d => d.Hatchery)
            .Where(d => !input.HatcheryId.HasValue || d.HatcheryId == input.HatcheryId)
            .OrderByDescending(d => d.DeliveryDate).ThenBy(d => d.HatcheryBatchNumber).ToListAsync();
        var list = await MapAsync(deliveries);
        return list
            .Where(d => SearchText.Matches(input.Search, d.HatcheryBatchNumber, d.HatcheryName, d.HatcheryShortCode, d.PlHealthCertificateNo, d.SfscNo))
            .Where(d => !input.Status.HasValue || d.Status == input.Status)
            .Where(d => !input.AvailableForStocking || (d.Status == FryDeliveryStatus.Received && !d.IsStocked && !d.IsStockingFinished))
            .ToList();
    }

    public virtual async Task<List<StatusCountDto>> GetStatusCounts(GetFryDeliveriesInput input)
    {
        input.Status = null;
        return (await FilteredAsync(input)).GroupBy(d => d.Status).Select(g => new StatusCountDto { Status = (int)g.Key, Count = g.Count() }).ToList();
    }

    public virtual async Task<FryDeliveryDetailDto> Get(Guid id)
    {
        var d = await _deliveries.GetAll().AsNoTracking().Include(x => x.Hatchery).FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new UserFriendlyException("Fry delivery not found.");
        var dto = new FryDeliveryDetailDto();
        Copy((await MapAsync(new List<FryDelivery> { d })).Single(), dto);
        var changes = await _changes.GetAll().AsNoTracking().Where(c => c.DeliveryId == id).OrderByDescending(c => c.ChangedAtUtc).ThenByDescending(c => c.CreationTime).ToListAsync();
        var names = await GetUserNamesAsync(changes.Select(c => c.CreatorUserId));
        dto.History = changes.Select(c => new FryDeliveryStatusChangeDto { Status = c.Status, ChangedAtUtc = c.ChangedAtUtc, ChangedBy = NameOf(names, c.CreatorUserId), Note = c.Note }).ToList();
        dto.Shortfalls = await _shortfalls.GetAll().AsNoTracking().Where(s => s.DeliveryId == id).OrderBy(s => s.Reason)
            .Select(s => new ShortfallDto { Reason = s.Reason, Count = s.Count, Note = s.Note }).ToListAsync();
        var finisher = await GetUserNamesAsync(new[] { d.StockingFinishedByUserId });
        dto.StockingFinishedBy = NameOf(finisher, d.StockingFinishedByUserId);
        dto.CanEdit = await IsGrantedAsync(PermissionNames.FryDeliveries_Manage);
        dto.CanUpdateStatus = !dto.IsStocked && !dto.IsStockingFinished && await IsGrantedAsync(PermissionNames.FryDeliveries_UpdateStatus);
        dto.CanFinishStocking = dto.IsStocked && !dto.IsStockingFinished && dto.CanEdit;
        return dto;
    }

    [AbpAuthorize(PermissionNames.FryDeliveries_Manage)]
    public virtual async Task FinishStocking(FinishStockingInput input)
    {
        var delivery = await _deliveries.GetAsync(input.DeliveryId);
        Production.Concurrency.Check(delivery.RowVersion, input.RowVersion);
        if (delivery.IsStockingFinished)
        {
            throw new UserFriendlyException("Stocking from this delivery is already finished.");
        }

        var cycle = await _cycles.GetAll().AsNoTracking().FirstOrDefaultAsync(c => c.FryDeliveryId == delivery.Id)
                    ?? throw new UserFriendlyException("Nothing has been stocked from this delivery yet. Stock at least one pond first.");
        var lines = input.Shortfalls ?? new List<ShortfallInput>();
        foreach (var line in lines)
        {
            if (!Enum.IsDefined(line.Reason))
            {
                throw new UserFriendlyException("Choose a reason for each count of PL not stocked.");
            }

            if (line.Count <= 0)
            {
                throw new UserFriendlyException("Each count of PL not stocked must be greater than zero.");
            }
        }

        if (lines.Select(l => l.Reason).Distinct().Count() != lines.Count)
        {
            throw new UserFriendlyException("Enter each reason once.");
        }

        var stocked = await _batches.GetAll().Where(b => b.CycleId == cycle.Id).SumAsync(b => b.PlCountStocked ?? 0);
        var notStocked = lines.Sum(l => l.Count);
        if (stocked + notStocked != delivery.PlReceived)
        {
            throw new UserFriendlyException(
                $"Stocked {stocked:N0} + not stocked {notStocked:N0} = {stocked + notStocked:N0} PL, but {delivery.PlReceived:N0} PL were received. "
                + $"Record {delivery.PlReceived - stocked:N0} PL as not stocked, split by reason.");
        }

        foreach (var line in lines)
        {
            await _shortfalls.InsertAsync(new FryDeliveryShortfall
            {
                DeliveryId = delivery.Id, Reason = line.Reason, Count = line.Count, Note = string.IsNullOrWhiteSpace(line.Note) ? null : line.Note.Trim(),
            });
        }

        delivery.StockingFinishedAtUtc = DateTime.UtcNow;
        delivery.StockingFinishedByUserId = AbpSession.UserId;
    }

    public virtual async Task<List<OpenDeliveryDto>> GetOpenWithPlLeft()
    {
        var today = await Farm.GetTodayAsync();
        var deliveries = await _deliveries.GetAll().AsNoTracking().Include(d => d.Hatchery)
            .Where(d => d.StockingFinishedAtUtc == null && d.DeliveryDate < today).OrderBy(d => d.DeliveryDate).ToListAsync();
        var mapped = await MapAsync(deliveries);
        return mapped.Where(d => (d.IsStocked || d.Status == FryDeliveryStatus.Received) && d.PlStocked < d.PlReceived).Select(d => new OpenDeliveryDto
        {
            Id = d.Id, HatcheryBatchNumber = d.HatcheryBatchNumber, HatcheryName = d.HatcheryName, DeliveryDate = d.DeliveryDate,
            PlReceived = d.PlReceived, PlStocked = d.PlStocked, CycleId = d.CycleId, CycleCode = d.CycleCode,
        }).ToList();
    }

    [AbpAuthorize(PermissionNames.FryDeliveries_Manage)]
    public virtual async Task<SaveFryDeliveryResult> Create(FryDeliveryEditDto input)
    {
        var hatchery = await _hatcheries.FirstOrDefaultAsync(h => h.Id == input.HatcheryId) ?? throw new UserFriendlyException("Choose a hatchery.");
        if (!hatchery.IsActive)
        {
            throw new UserFriendlyException($"The hatchery '{hatchery.Name}' is inactive.");
        }

        if (!Enum.IsDefined(input.InitialStatus))
        {
            throw new UserFriendlyException("Choose the delivery status.");
        }

        await ValidateAsync(input, null);
        var delivery = new FryDelivery { HatcheryId = hatchery.Id };
        Apply(delivery, input);
        var id = await _deliveries.InsertAndGetIdAsync(delivery);
        await _changes.InsertAsync(new FryDeliveryStatusChange { DeliveryId = id, Status = input.InitialStatus, ChangedAtUtc = DateTime.UtcNow, Note = "Recorded" });
        return new SaveFryDeliveryResult { Id = id, Warnings = await WarningsAsync(input) };
    }

    [AbpAuthorize(PermissionNames.FryDeliveries_Manage)]
    public virtual async Task<SaveFryDeliveryResult> Update(FryDeliveryEditDto input)
    {
        var delivery = await _deliveries.GetAsync(input.Id);
        Production.Concurrency.Check(delivery.RowVersion, input.RowVersion);
        var cycle = await _cycles.GetAll().AsNoTracking().FirstOrDefaultAsync(c => c.FryDeliveryId == delivery.Id);
        if (input.HatcheryId != delivery.HatcheryId)
        {
            if (cycle != null)
            {
                throw new UserFriendlyException("The hatchery cannot change once a production cycle uses this delivery.");
            }

            var hatchery = await _hatcheries.FirstOrDefaultAsync(h => h.Id == input.HatcheryId) ?? throw new UserFriendlyException("Choose a hatchery.");
            delivery.HatcheryId = hatchery.Id;
        }

        if (cycle != null)
        {
            var stocked = await _batches.GetAll().Where(b => b.CycleId == cycle.Id).SumAsync(b => b.PlCountStocked ?? 0);
            if (input.PlReceived < stocked)
            {
                throw new UserFriendlyException($"PL received cannot be less than the {stocked:N0} PL already stocked from this delivery.");
            }
        }

        if (delivery.IsStockingFinished && input.PlReceived != delivery.PlReceived)
        {
            throw new UserFriendlyException("PL received cannot change after Finish stocking; the delivery's counts already add up.");
        }

        await ValidateAsync(input, delivery.Id);
        Apply(delivery, input);
        return new SaveFryDeliveryResult { Id = delivery.Id, Warnings = await WarningsAsync(input) };
    }

    [AbpAuthorize(PermissionNames.FryDeliveries_UpdateStatus)]
    public virtual async Task UpdateStatus(UpdateFryDeliveryStatusInput input)
    {
        var delivery = await _deliveries.GetAsync(input.DeliveryId);
        if (!Enum.IsDefined(input.Status))
        {
            throw new UserFriendlyException("Choose the new status.");
        }

        if (await _cycles.GetAll().AnyAsync(c => c.FryDeliveryId == delivery.Id))
        {
            throw new UserFriendlyException("This delivery is already stocked into a production cycle; its status can no longer change.");
        }

        var current = await CurrentStatusAsync(delivery.Id);
        if (current == input.Status)
        {
            throw new UserFriendlyException($"The delivery is already {Label(input.Status)}.");
        }

        var changedAt = input.ChangedAt.HasValue ? FarmClock.ToUtc(input.ChangedAt.Value) : DateTime.UtcNow;
        if (changedAt > DateTime.UtcNow.AddMinutes(5))
        {
            throw new UserFriendlyException("The status change cannot be in the future.");
        }

        await _changes.InsertAsync(new FryDeliveryStatusChange
        {
            DeliveryId = delivery.Id, Status = input.Status, ChangedAtUtc = changedAt, Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim(),
        });
    }

    public static string Label(FryDeliveryStatus status) => status switch
    {
        FryDeliveryStatus.InTransit => "In transit",
        _ => status.ToString(),
    };

    internal async Task<FryDeliveryStatus?> CurrentStatusAsync(Guid deliveryId) =>
        await _changes.GetAll().Where(c => c.DeliveryId == deliveryId)
            .OrderByDescending(c => c.ChangedAtUtc).ThenByDescending(c => c.CreationTime)
            .Select(c => (FryDeliveryStatus?)c.Status).FirstOrDefaultAsync();

    private async Task ValidateAsync(FryDeliveryEditDto input, Guid? exceptId)
    {
        var number = input.HatcheryBatchNumber.Trim();
        if (await _deliveries.GetAll().AnyAsync(d => d.HatcheryId == input.HatcheryId && d.HatcheryBatchNumber == number && d.Id != exceptId))
        {
            throw new UserFriendlyException($"Batch number '{number}' is already recorded for this hatchery.");
        }

        if (input.DeliveryDate > (await Farm.GetTodayAsync()).AddDays(60))
        {
            throw new UserFriendlyException("The delivery date is too far in the future.");
        }
    }

    private async Task<List<WarningDto>> WarningsAsync(FryDeliveryEditDto input)
    {
        var list = new List<WarningDto>();
        var min = await _standards.GetAsync(ReferenceData.FarmThresholds.PlAgeMin);
        var max = await _standards.GetAsync(ReferenceData.FarmThresholds.PlAgeMax);
        if (input.PlAgeDays.HasValue && (input.PlAgeDays < min || input.PlAgeDays > max))
        {
            list.Add(new WarningDto { Kind = "PlAge", Message = $"PL{input.PlAgeDays} is outside the farm's PL{min:0}–PL{max:0} range for stocking (Farm Standards)." });
        }

        return list;
    }

    private static void Apply(FryDelivery d, FryDeliveryEditDto input)
    {
        d.HatcheryBatchNumber = input.HatcheryBatchNumber.Trim();
        d.DeliveryDate = input.DeliveryDate;
        d.PlAgeDays = input.PlAgeDays;
        d.PlReceived = input.PlReceived;
        d.CostPhp = input.CostPhp;
        d.Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim();
        d.PlHealthCertificateNo = string.IsNullOrWhiteSpace(input.PlHealthCertificateNo) ? null : input.PlHealthCertificateNo.Trim();
        d.TransportPermitNo = string.IsNullOrWhiteSpace(input.TransportPermitNo) ? null : input.TransportPermitNo.Trim();
        d.SfscNo = string.IsNullOrWhiteSpace(input.SfscNo) ? null : input.SfscNo.Trim();
    }

    private async Task<List<FryDeliveryDto>> MapAsync(List<FryDelivery> deliveries)
    {
        var ids = deliveries.Select(d => d.Id).ToList();
        var changes = await _changes.GetAll().AsNoTracking().Where(c => ids.Contains(c.DeliveryId))
            .Select(c => new { c.DeliveryId, c.Status, c.ChangedAtUtc, c.CreationTime }).ToListAsync();
        var cycles = await _cycles.GetAll().AsNoTracking().Where(c => ids.Contains(c.FryDeliveryId))
            .Select(c => new { c.Id, c.FryDeliveryId, c.CycleCode }).ToListAsync();
        var cycleIds = cycles.Select(c => c.Id).ToList();
        var stocked = await _batches.GetAll().Where(b => cycleIds.Contains(b.CycleId))
            .GroupBy(b => b.CycleId).Select(g => new { CycleId = g.Key, Pl = g.Sum(b => b.PlCountStocked ?? 0) }).ToListAsync();
        var shortfalls = await _shortfalls.GetAll().Where(s => ids.Contains(s.DeliveryId))
            .GroupBy(s => s.DeliveryId).Select(g => new { DeliveryId = g.Key, Pl = g.Sum(s => s.Count) }).ToListAsync();
        return deliveries.Select(d =>
        {
            var latest = changes.Where(c => c.DeliveryId == d.Id).OrderByDescending(c => c.ChangedAtUtc).ThenByDescending(c => c.CreationTime).FirstOrDefault();
            var cycle = cycles.FirstOrDefault(c => c.FryDeliveryId == d.Id);
            var pl = cycle == null ? 0 : stocked.FirstOrDefault(s => s.CycleId == cycle.Id)?.Pl ?? 0;
            return new FryDeliveryDto
            {
                Id = d.Id, HatcheryId = d.HatcheryId, HatcheryName = d.Hatchery?.Name, HatcheryShortCode = d.Hatchery?.ShortCode,
                HatcheryBatchNumber = d.HatcheryBatchNumber, DeliveryDate = d.DeliveryDate, PlAgeDays = d.PlAgeDays, PlReceived = d.PlReceived,
                CostPhp = d.CostPhp, Notes = d.Notes, Status = latest?.Status ?? FryDeliveryStatus.Ordered, StatusChangedAtUtc = latest?.ChangedAtUtc ?? d.CreationTime,
                PlHealthCertificateNo = d.PlHealthCertificateNo, TransportPermitNo = d.TransportPermitNo, SfscNo = d.SfscNo,
                IsStockingFinished = d.IsStockingFinished, StockingFinishedAtUtc = d.StockingFinishedAtUtc,
                PlNotStocked = shortfalls.FirstOrDefault(s => s.DeliveryId == d.Id)?.Pl ?? 0,
                IsStocked = cycle != null, CycleId = cycle?.Id, CycleCode = cycle?.CycleCode, PlStocked = pl,
                PlLeft = d.PlReceived - pl - (shortfalls.FirstOrDefault(s => s.DeliveryId == d.Id)?.Pl ?? 0),
                RowVersion = Production.Concurrency.Encode(d.RowVersion),
            };
        }).ToList();
    }

    private static void Copy(FryDeliveryDto from, FryDeliveryDto to)
    {
        foreach (var p in typeof(FryDeliveryDto).GetProperties())
        {
            p.SetValue(to, p.GetValue(from));
        }
    }
}
