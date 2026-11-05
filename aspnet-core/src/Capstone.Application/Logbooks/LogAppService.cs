using Abp.Application.Services.Dto;
using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Abp.Runtime.Session;
using Abp.UI;
using Capstone.Alerts;
using Capstone.Authorization;
using Capstone.Common;
using Capstone.Domain;
using Capstone.Inventory;
using Capstone.Logbooks.Dto;
using Capstone.Production;
using Capstone.Production.Dto;
using Capstone.ReferenceData;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Capstone.Logbooks;

[AbpAuthorize(PermissionNames.Logs)]
public class LogAppService : CapstoneAppServiceBase
{
    private readonly IRepository<ProductionLog, Guid> _logs;
    private readonly IRepository<PondBatch, Guid> _batches;
    private readonly IRepository<PondPreparationDetail, Guid> _prep;
    private readonly IRepository<GrowthSamplingDetail, Guid> _growth;
    private readonly IRepository<ChemicalApplicationDetail, Guid> _chemical;
    private readonly IRepository<MortalityDetail, Guid> _mortality;
    private readonly IRepository<PostProductionDetail, Guid> _postProduction;
    private readonly IRepository<WaterQualityMeasurement, Guid> _measurements;
    private readonly IRepository<StockMovement, Guid> _movements;
    private readonly IRepository<InventoryLot, Guid> _lots;
    private readonly InventoryManager _inventory;
    private readonly WaterQualityEvaluator _waterQuality;
    private readonly BatchDataLoader _loader;
    private readonly DailyLogStatusService _daily;
    private readonly FarmStandardsProvider _standards;

    public LogAppService(
        IRepository<ProductionLog, Guid> logs,
        IRepository<PondBatch, Guid> batches,
        IRepository<PondPreparationDetail, Guid> prep,
        IRepository<GrowthSamplingDetail, Guid> growth,
        IRepository<ChemicalApplicationDetail, Guid> chemical,
        IRepository<MortalityDetail, Guid> mortality,
        IRepository<PostProductionDetail, Guid> postProduction,
        IRepository<WaterQualityMeasurement, Guid> measurements,
        IRepository<StockMovement, Guid> movements,
        IRepository<InventoryLot, Guid> lots,
        InventoryManager inventory,
        WaterQualityEvaluator waterQuality,
        BatchDataLoader loader,
        DailyLogStatusService daily,
        FarmStandardsProvider standards)
    {
        _standards = standards;
        _logs = logs;
        _batches = batches;
        _prep = prep;
        _growth = growth;
        _chemical = chemical;
        _mortality = mortality;
        _postProduction = postProduction;
        _measurements = measurements;
        _movements = movements;
        _lots = lots;
        _inventory = inventory;
        _waterQuality = waterQuality;
        _loader = loader;
        _daily = daily;
    }

    public virtual async Task<List<ParameterDto>> GetParameters()
    {
        var parameters = await _waterQuality.GetParametersAsync();
        return parameters.Values.OrderBy(p => p.SortOrder).Select(MapParameter).ToList();
    }

    internal static ParameterDto MapParameter(ParameterDefinition p) => new()
    {
        Code = p.Id, Name = p.Name, UnitCode = p.UnitCode, PhysicalMin = p.PhysicalMin, PhysicalMax = p.PhysicalMax,
        OptimalMin = p.OptimalMin, OptimalMinExclusive = p.OptimalMinExclusive, OptimalMax = p.OptimalMax, OptimalMaxExclusive = p.OptimalMaxExclusive,
        OptimalRange = p.DescribeOptimalRange(), GuidanceFrequency = p.GuidanceFrequency,
        LowRecommendation = p.LowRecommendation, HighRecommendation = p.HighRecommendation, RequiredAtStocking = p.RequiredAtStocking,
        SortOrder = p.SortOrder, IsActive = p.IsActive,
    };

    private static readonly string[] LogSortKeys = { "observedAt", "logType", "recordedAt" };

    public virtual async Task<PagedResultDto<LogDto>> GetList(GetLogsInput input)
    {
        using (VoidedFilter(input))
        {
            return await GetListCoreAsync(input);
        }
    }

    public virtual async Task<List<StatusCountDto>> GetTypeCounts(GetLogsInput input)
    {
        using (VoidedFilter(input))
        {
            input.LogType = null;
            var query = await FilteredLogsAsync(input);
            return await query.GroupBy(l => l.LogType).Select(g => new StatusCountDto { Status = (int)g.Key, Count = g.Count() }).ToListAsync();
        }
    }

    private IDisposable VoidedFilter(GetLogsInput input) =>
        input.IncludeVoided ? CurrentUnitOfWork.DisableFilter(AbpDataFilters.SoftDelete) : new NoFilter();

    private sealed class NoFilter : IDisposable
    {
        public void Dispose()
        {
        }
    }

    private async Task<PagedResultDto<LogDto>> GetListCoreAsync(GetLogsInput input)
    {
        var query = await FilteredLogsAsync(input);
        var total = await query.CountAsync();
        var sort = SortSpec.Parse(input.Sorting, LogSortKeys, "observedAt", true);
        var ordered = sort.Key switch
        {
            "logType" => sort.Apply(query, l => l.LogType).ThenByDescending(l => l.ObservedAtUtc),
            "recordedAt" => sort.Apply(query, l => l.CreationTime),
            _ => sort.Apply(query, l => l.ObservedAtUtc).ThenByDescending(l => l.CreationTime),
        };
        var ids = await ordered.Skip(input.SkipCount).Take(input.MaxResultCount).Select(l => l.Id).ToListAsync();
        return new PagedResultDto<LogDto>(total, await BuildDtosAsync(ids));
    }

    internal async Task<List<LogDto>> ListAllAsync(GetLogsInput input)
    {
        var query = await FilteredLogsAsync(input);
        var ids = await query.OrderByDescending(l => l.ObservedAtUtc).ThenByDescending(l => l.CreationTime).Select(l => l.Id).ToListAsync();
        return await BuildDtosAsync(ids);
    }

    private async Task<IQueryable<ProductionLog>> FilteredLogsAsync(GetLogsInput input)
    {
        var zone = await Farm.GetTimeZoneAsync();
        var query = _logs.GetAll().AsNoTracking()
            .Where(l => !input.BatchId.HasValue || l.BatchId == input.BatchId)
            .Where(l => !input.LogType.HasValue || l.LogType == input.LogType);
        if (input.From.HasValue)
        {
            var from = FarmClock.StartOfDayUtc(input.From.Value, zone);
            query = query.Where(l => l.ObservedAtUtc >= from);
        }

        if (input.To.HasValue)
        {
            var to = FarmClock.StartOfDayUtc(input.To.Value.AddDays(1), zone);
            query = query.Where(l => l.ObservedAtUtc < to);
        }

        var search = input.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            var people = UserRepository.GetAll().Where(u => (u.Name + " " + u.Surname).Contains(search) || u.UserName.Contains(search)).Select(u => (long?)u.Id);
            query = query.Where(l => l.Notes.Contains(search) || l.Batch.BatchCode.Contains(search) || l.Batch.Pond.PondCode.Contains(search)
                                     || people.Contains(l.CreatorUserId));
        }

        if (input.OutOfRangeOnly)
        {
            var flagged = _measurements.GetAll().Where(m => m.IsOutOfRange).Select(m => m.LogId);
            query = query.Where(l => flagged.Contains(l.Id));
        }

        return query;
    }

    private async Task<List<LogDto>> BuildDtosAsync(List<Guid> ids)
    {
        var zone = await Farm.GetTimeZoneAsync();
        var dtos = await LoadDtosAsync(ids);
        var growthBatchIds = dtos.Where(d => d.LogType == LogType.GrowthSampling).Select(d => d.BatchId).Distinct().ToList();
        if (growthBatchIds.Count > 0)
        {
            var samples = await (from l in _logs.GetAll().AsNoTracking()
                                 join g in _growth.GetAll().AsNoTracking() on l.Id equals g.Id
                                 where growthBatchIds.Contains(l.BatchId)
                                 select new { l.Id, l.BatchId, l.ObservedAtUtc, g.AbwG }).ToListAsync();
            foreach (var dto in dtos.Where(d => d.LogType == LogType.GrowthSampling))
            {
                var previous = samples.Where(x => x.BatchId == dto.BatchId && x.ObservedAtUtc < dto.ObservedAtUtc).OrderBy(x => x.ObservedAtUtc).LastOrDefault();
                if (previous != null && dto.AbwG.HasValue)
                {
                    dto.GrowthRateGPerDay = IndicatorCalculator.GrowthRate(previous.AbwG, FarmClock.LocalDate(previous.ObservedAtUtc, zone),
                        dto.AbwG.Value, FarmClock.LocalDate(dto.ObservedAtUtc, zone));
                }
            }
        }

        var position = ids.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
        return dtos.OrderBy(d => position.TryGetValue(d.Id, out var i) ? i : int.MaxValue).ToList();
    }

    public virtual async Task<LogDto> Get(Guid id)
    {
        var list = await LoadDtosAsync(new List<Guid> { id });
        return list.FirstOrDefault() ?? throw new UserFriendlyException("Log entry not found.");
    }

    [AbpAuthorize(PermissionNames.Logs_Create)]
    public virtual async Task<SaveLogResultDto> Create(SaveLogInput input)
    {
        if (input.ClientRequestId == Guid.Empty)
        {
            throw new UserFriendlyException("ClientRequestId is required.");
        }

        var existing = await _logs.GetAll().AsNoTracking().FirstOrDefaultAsync(l => l.ClientRequestId == input.ClientRequestId);
        if (existing != null)
        {
            return new SaveLogResultDto { Log = await Get(existing.Id), WasDuplicate = true };
        }

        var batch = await LoadBatchAsync(input.BatchId);
        if (!batch.AllowsNewLog(input.LogType))
        {
            throw new UserFriendlyException(batch.Status == BatchStatus.Closed
                ? FpmsErrors.ReadOnlyClosed
                : $"A {Describe(input.LogType)} log cannot be added while the batch is {batch.Status}.");
        }

        var observedUtc = await ValidateObservedAtAsync(input, batch);
        await PrevalidateAsync(input);
        var log = new ProductionLog
        {
            Id = Guid.NewGuid(),
            BatchId = batch.Id,
            PondId = batch.PondId,
            LogType = input.LogType,
            ObservedAtUtc = observedUtc,
            Notes = input.Notes?.Trim(),
            ClientRequestId = input.ClientRequestId,
        };
        await _logs.InsertAsync(log);
        await CurrentUnitOfWork.SaveChangesAsync();

        var warnings = await WriteDetailsAsync(log, batch, input, isUpdate: false);
        await CurrentUnitOfWork.SaveChangesAsync();
        return await BuildResultAsync(log.Id, batch.Id, warnings);
    }

    [AbpAuthorize(PermissionNames.Logs_Create)]
    public virtual async Task<SaveLogResultDto> Update(SaveLogInput input)
    {
        if (!input.Id.HasValue)
        {
            throw new UserFriendlyException("Log id is required.");
        }

        var log = await _logs.FirstOrDefaultAsync(l => l.Id == input.Id.Value) ?? throw new UserFriendlyException("Log entry not found.");
        if (log.LogType != input.LogType)
        {
            throw new UserFriendlyException("The log type cannot be changed.");
        }

        var batch = await LoadBatchAsync(log.BatchId);
        batch.EnsureNotClosed();
        if (log.CreatorUserId != AbpSession.UserId && !await CanEditAnyLogAsync())
        {
            throw new AbpAuthorizationException("You can only edit log entries you recorded.");
        }

        if (log.LogType is LogType.PondPreparation or LogType.Feeding or LogType.PostProduction)
        {
            throw new UserFriendlyException($"{Describe(log.LogType)} logs are historical and can no longer be edited.");
        }

        log.ObservedAtUtc = await ValidateObservedAtAsync(input, batch);
        await PrevalidateAsync(input);
        log.Notes = input.Notes?.Trim();

        await _inventory.ReverseUsageForLogAsync(log.Id, DateTime.UtcNow, $"Correction of {Describe(log.LogType)} log edited");
        await RemoveDetailsAsync(log);
        await CurrentUnitOfWork.SaveChangesAsync();

        var warnings = await WriteDetailsAsync(log, batch, input, isUpdate: true);
        await CurrentUnitOfWork.SaveChangesAsync();
        return await BuildResultAsync(log.Id, batch.Id, warnings);
    }

    [AbpAuthorize(PermissionNames.Logs_EditAny)]
    public virtual async Task Void(VoidInput input)
    {
        var log = await _logs.FirstOrDefaultAsync(l => l.Id == input.Id) ?? throw new UserFriendlyException("Log entry not found.");
        var batch = await LoadBatchAsync(log.BatchId);
        batch.EnsureNotClosed();
        await _inventory.ReverseUsageForLogAsync(log.Id, DateTime.UtcNow, $"Void of {Describe(log.LogType)} log: {input.Reason.Trim()}");
        log.VoidReason = input.Reason.Trim();
        await CurrentUnitOfWork.SaveChangesAsync();
        await _logs.DeleteAsync(log);
    }

    private static string Clean(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<PondBatch> LoadBatchAsync(Guid batchId)
    {
        return await _batches.GetAll().Include(b => b.Pond).FirstOrDefaultAsync(b => b.Id == batchId)
               ?? throw new UserFriendlyException("Batch not found.");
    }

    private async Task<DateTime> ValidateObservedAtAsync(SaveLogInput input, PondBatch batch)
    {
        if (input.ObservedAt == default)
        {
            throw new UserFriendlyException("Observation date and time is required.");
        }

        var utc = FarmClock.ToUtc(input.ObservedAt);
        if (utc > DateTime.UtcNow.AddMinutes(5))
        {
            throw new UserFriendlyException("The observation time cannot be in the future.");
        }

        var zone = await Farm.GetTimeZoneAsync();
        var day = FarmClock.LocalDate(utc, zone);
        var needsStocking = input.LogType is LogType.WaterQuality or LogType.GrowthSampling or LogType.Mortality or LogType.ChemicalApplication or LogType.GeneralNote;
        if (needsStocking && batch.StockingDate.HasValue && day < batch.StockingDate.Value)
        {
            throw new UserFriendlyException($"The observation date cannot be before the batch's stocking date ({batch.StockingDate:yyyy-MM-dd}).");
        }

        if (await _daily.LastLogDateAsync(batch, zone) is { } lastDay && day > lastDay)
        {
            throw new UserFriendlyException($"The batch had its Total harvest on {lastDay:yyyy-MM-dd}; entries cannot be dated after it.");
        }

        return utc;
    }

    private async Task PrevalidateAsync(SaveLogInput input)
    {
        switch (input.LogType)
        {
            case LogType.WaterQuality:
            case LogType.PreStockingWaterQuality:
                await _waterQuality.EvaluateAsync((input.Readings ?? new List<ReadingInput>()).Select(r => (r.ParameterCode, r.Value)),
                    requireStockingSet: input.LogType == LogType.PreStockingWaterQuality);
                break;
            case LogType.GrowthSampling:
                var g = input.Growth ?? throw new UserFriendlyException("Growth sampling details are required.");
                if (g.SampleCount <= 0 || g.TotalSampleWeightG <= 0)
                {
                    throw new UserFriendlyException("Sample size and total weight must be greater than zero.");
                }

                if (IndicatorCalculator.Abw(g.TotalSampleWeightG, g.SampleCount) > 60m)
                {
                    throw new UserFriendlyException("An ABW above 60 g is not possible for whiteleg shrimp. Check the sample count and weight.");
                }

                break;
            case LogType.ChemicalApplication:
                var c = input.ChemicalApplication ?? throw new UserFriendlyException("Chemical or supplement details are required.");
                var lines = c.Usages ?? new List<UsageLineInput>();
                if (lines.Count == 0 || lines.Any(u => u.Quantity <= 0))
                {
                    throw new UserFriendlyException("Choose the product lot and enter a quantity greater than zero.");
                }

                foreach (var usage in lines)
                {
                    var lot = await _inventory.GetLotWithItemAsync(usage.LotId);
                    if (lot.Item.Category == InventoryCategory.Feed)
                    {
                        throw new UserFriendlyException("Feed is recorded as a feeding session. Choose a supplement or chemical lot.");
                    }
                }

                if (c.Dosage is < 0)
                {
                    throw new UserFriendlyException("The dosage cannot be negative.");
                }

                if (c.WithdrawalDays is < 0 or > ChemicalApplicationDetail.MaxWithdrawalDays)
                {
                    throw new UserFriendlyException("Withdrawal days must be between 0 and 365.");
                }

                if (c.EndDate.HasValue && input.ObservedAt != default)
                {
                    var applied = FarmClock.LocalDate(FarmClock.ToUtc(input.ObservedAt), await Farm.GetTimeZoneAsync());
                    if (c.EndDate.Value < applied)
                    {
                        throw new UserFriendlyException("The treatment's end date cannot be before the application date.");
                    }
                }

                break;
            case LogType.Mortality:
                var m = input.Mortality ?? throw new UserFriendlyException("Mortality details are required.");
                if (m.Count < 0)
                {
                    throw new UserFriendlyException("Mortality count cannot be negative.");
                }

                if (!Enum.IsDefined(m.Cause))
                {
                    throw new UserFriendlyException("Choose the cause: Disease, Water quality or Others.");
                }

                if (m.Cause == MortalityCause.Disease && string.IsNullOrWhiteSpace(m.DiseaseName))
                {
                    throw new UserFriendlyException("Enter the disease name (for example WSSV or AHPND).");
                }

                if (m.Cause == MortalityCause.Others && string.IsNullOrWhiteSpace(m.OtherCause))
                {
                    throw new UserFriendlyException("Describe the other cause (for example predation by birds).");
                }

                break;
            case LogType.GeneralNote:
                if (string.IsNullOrWhiteSpace(input.Notes))
                {
                    throw new UserFriendlyException("Write the note.");
                }

                break;
            default:
                throw new UserFriendlyException("This log type can no longer be recorded.");
        }
    }

    private async Task RemoveDetailsAsync(ProductionLog log)
    {
        async Task Remove<T>(IRepository<T, Guid> repo) where T : LogDetail
        {
            var row = await repo.FirstOrDefaultAsync(log.Id);
            if (row != null)
            {
                await repo.DeleteAsync(row);
            }
        }

        await Remove(_growth);
        await Remove(_chemical);
        await Remove(_mortality);
        foreach (var m in await _measurements.GetAllListAsync(m => m.LogId == log.Id))
        {
            await _measurements.DeleteAsync(m);
        }
    }

    private async Task<List<WarningDto>> WriteDetailsAsync(ProductionLog log, PondBatch batch, SaveLogInput input, bool isUpdate)
    {
        var warnings = new List<WarningDto>();
        switch (input.LogType)
        {
            case LogType.WaterQuality:
            case LogType.PreStockingWaterQuality:
            {
                var readings = await _waterQuality.EvaluateAsync((input.Readings ?? new List<ReadingInput>()).Select(r => (r.ParameterCode, r.Value)),
                    requireStockingSet: input.LogType == LogType.PreStockingWaterQuality);
                foreach (var r in readings)
                {
                    await _measurements.InsertAsync(new WaterQualityMeasurement
                    {
                        Id = Guid.NewGuid(), LogId = log.Id, ParameterCode = r.Parameter.Id, Value = r.Value, UnitCode = r.Parameter.UnitCode, IsOutOfRange = r.IsOutOfRange,
                        Side = r.Parameter.SideOf(r.Value),
                    });
                    if (r.IsOutOfRange)
                    {
                        warnings.Add(new WarningDto
                        {
                            Kind = "WaterQuality",
                            Message = $"{r.Parameter.Name} {r.Value:0.###} {r.Parameter.UnitCode} is outside the optimal range ({r.Parameter.DescribeOptimalRange()}). Saved and flagged."
                                + (r.Parameter.RecommendationFor(r.Value) is { } advice ? $" Recommendation: {advice}" : string.Empty),
                        });
                    }
                }

                break;
            }

            case LogType.GrowthSampling:
            {
                var g = input.Growth ?? throw new UserFriendlyException("Growth sampling details are required.");
                if (g.SampleCount <= 0 || g.TotalSampleWeightG <= 0)
                {
                    throw new UserFriendlyException("Sample size and total weight must be greater than zero.");
                }

                var abw = IndicatorCalculator.Abw(g.TotalSampleWeightG, g.SampleCount);
                if (abw > 60m)
                {
                    throw new UserFriendlyException($"An ABW of {abw:0.##} g is not possible for whiteleg shrimp (60 g at most). Check the sample count and weight.");
                }

                var previous = await (from l in _logs.GetAll().AsNoTracking()
                                      join d in _growth.GetAll().AsNoTracking() on l.Id equals d.Id
                                      where l.BatchId == batch.Id && l.Id != log.Id && l.ObservedAtUtc < log.ObservedAtUtc
                                      orderby l.ObservedAtUtc descending
                                      select (decimal?)d.AbwG).FirstOrDefaultAsync();
                var dropPct = await _standards.GetAsync(FarmThresholds.AbwDropPct);
                if (previous.HasValue && previous.Value > 0 && abw < previous.Value * (1 - dropPct / 100m))
                {
                    warnings.Add(new WarningDto { Kind = "Growth", Message = $"ABW {abw:0.##} g is more than {dropPct:0.#}% below the previous sample ({previous:0.##} g). Check the sample." });
                }

                if (g.EstimatedSurvivalPct is < 0 or > 100)
                {
                    throw new UserFriendlyException("Estimated survival must be between 0 and 100%.");
                }

                if (g.EstimatedSurvivalPct.HasValue)
                {
                    var previousEstimate = await (from l in _logs.GetAll().AsNoTracking()
                                                  join d in _growth.GetAll().AsNoTracking() on l.Id equals d.Id
                                                  where l.BatchId == batch.Id && l.Id != log.Id && l.ObservedAtUtc < log.ObservedAtUtc && d.EstimatedSurvivalPct != null
                                                  orderby l.ObservedAtUtc descending
                                                  select d.EstimatedSurvivalPct).FirstOrDefaultAsync();
                    if (previousEstimate.HasValue && g.EstimatedSurvivalPct.Value > previousEstimate.Value)
                    {
                        warnings.Add(new WarningDto
                        {
                            Kind = "Growth",
                            Message = $"Estimated survival {g.EstimatedSurvivalPct:0.#}% is higher than the previous estimate ({previousEstimate:0.#}%). Check the estimate.",
                        });
                    }
                }

                await _growth.InsertAsync(new GrowthSamplingDetail
                {
                    Id = log.Id, SampleCount = g.SampleCount, TotalSampleWeightG = g.TotalSampleWeightG, AbwG = abw, EstimatedSurvivalPct = g.EstimatedSurvivalPct,
                });
                break;
            }

            case LogType.ChemicalApplication:
            {
                var c = input.ChemicalApplication ?? throw new UserFriendlyException("Chemical or supplement details are required.");
                var lines = (c.Usages ?? new List<UsageLineInput>()).ToList();
                if (lines.Count == 0 || lines.Any(u => u.Quantity <= 0))
                {
                    throw new UserFriendlyException("Choose the product lot and enter a quantity greater than zero.");
                }

                var day = FarmClock.LocalDate(log.ObservedAtUtc, await Farm.GetTimeZoneAsync());
                await _chemical.InsertAsync(new ChemicalApplicationDetail
                {
                    Id = log.Id, Reason = Clean(c.Reason), Dosage = c.Dosage, DosageUnit = Clean(c.DosageUnit), Method = Clean(c.Method), EndDate = c.EndDate,
                    WithdrawalDays = c.WithdrawalDays, PrescriptionRef = Clean(c.PrescriptionRef),
                });
                foreach (var usage in lines)
                {
                    var lot = await _inventory.GetLotWithItemAsync(usage.LotId);
                    if (lot.Item.Category == InventoryCategory.Feed)
                    {
                        throw new UserFriendlyException("Feed is recorded as a feeding session. Choose a supplement or chemical lot.");
                    }

                    AddStockWarning(warnings, await _inventory.RecordUsageAsync(lot, usage.Quantity, batch.Id, log.ObservedAtUtc, sourceLogId: log.Id, usedOn: day));
                }

                if (ChemicalApplicationDetail.SafeHarvestDate(day, c.EndDate, c.WithdrawalDays) is { } safe)
                {
                    warnings.Add(new WarningDto { Kind = "Withdrawal", Message = $"Earliest safe harvest date for this treatment: {safe:yyyy-MM-dd} ({c.WithdrawalDays} withdrawal days)." });
                }

                break;
            }

            case LogType.Mortality:
            {
                var m = input.Mortality ?? throw new UserFriendlyException("Mortality details are required.");
                if (m.Count < 0)
                {
                    throw new UserFriendlyException("Mortality count cannot be negative.");
                }

                if (!Enum.IsDefined(m.Cause))
                {
                    throw new UserFriendlyException("Choose the cause: Disease, Water quality or Others.");
                }

                if (m.Cause == MortalityCause.Disease && string.IsNullOrWhiteSpace(m.DiseaseName))
                {
                    throw new UserFriendlyException("Enter the disease name (for example WSSV or AHPND).");
                }

                if (m.Cause == MortalityCause.Others && string.IsNullOrWhiteSpace(m.OtherCause))
                {
                    throw new UserFriendlyException("Describe the other cause (for example predation by birds).");
                }

                var facts = await _loader.LoadAsync(batch.Id, await Farm.GetTimeZoneAsync());
                var population = IndicatorCalculator.Compute(facts, await Farm.GetTodayAsync()).EstimatedPopulation;
                if (isUpdate)
                {
                    population = null;
                }

                var high = false;
                var dailyPct = await _standards.GetAsync(FarmThresholds.DailyMortalityPct);
                if (population.HasValue && m.Count > population.Value)
                {
                    high = true;
                    warnings.Add(new WarningDto { Kind = "Mortality", Message = $"{m.Count:N0} dead is more than the estimated {population.Value:N0} shrimp left in the pond. Check this and earlier entries." });
                }
                else if (population.HasValue && population.Value > 0 && m.Count > population.Value * dailyPct / 100m)
                {
                    high = true;
                    warnings.Add(new WarningDto { Kind = "Mortality", Message = $"{m.Count:N0} dead is more than {dailyPct:0.##}% of the estimated population ({population.Value:N0}) in one entry." });
                }

                await _mortality.InsertAsync(new MortalityDetail
                {
                    Id = log.Id, Count = m.Count, Cause = m.Cause, IsHighMortality = high,
                    DiseaseName = m.Cause == MortalityCause.Disease ? m.DiseaseName.Trim() : null,
                    OtherCause = m.Cause == MortalityCause.Others ? m.OtherCause.Trim() : null,
                    Symptoms = m.Symptoms, ResponseAction = m.ResponseAction,
                });
                break;
            }

            case LogType.GeneralNote:
                if (string.IsNullOrWhiteSpace(input.Notes))
                {
                    throw new UserFriendlyException("Write the note.");
                }

                break;

            default:
                throw new UserFriendlyException("Unknown log type.");
        }

        return warnings;
    }

    private static void AddStockWarning(List<WarningDto> warnings, StockUsageResult usage)
    {
        if (!string.IsNullOrEmpty(usage.Warning))
        {
            warnings.Add(new WarningDto { Kind = "Stock", Message = usage.Warning });
        }
    }

    private async Task<SaveLogResultDto> BuildResultAsync(Guid logId, Guid batchId, List<WarningDto> warnings)
    {
        var facts = await _loader.LoadAsync(batchId, await Farm.GetTimeZoneAsync());
        return new SaveLogResultDto
        {
            Log = await Get(logId),
            Warnings = warnings,
            Indicators = IndicatorsDto.From(IndicatorCalculator.Compute(facts, await Farm.GetTodayAsync())),
        };
    }

    private async Task<List<LogDto>> LoadDtosAsync(List<Guid> ids)
    {
        if (ids.Count == 0)
        {
            return new List<LogDto>();
        }

        var mapZone = await Farm.GetTimeZoneAsync();
        var logs = await _logs.GetAll().AsNoTracking()
            .Include(l => l.Batch).ThenInclude(b => b.Pond)
            .Include(l => l.PondPreparation)
            .Include(l => l.GrowthSampling)
            .Include(l => l.ChemicalApplication)
            .Include(l => l.Mortality)
            .Include(l => l.PostProduction)
            .Include(l => l.Measurements)
            .Where(l => ids.Contains(l.Id))
            .AsSplitQuery()
            .ToListAsync();

        var usageRows = await _movements.GetAll().AsNoTracking().Include(m => m.Lot).ThenInclude(l => l.Item)
            .Where(m => m.SourceLogId != null && ids.Contains(m.SourceLogId.Value) && m.Type == StockMovementType.Usage)
            .ToListAsync();
        var reversed = await _movements.GetAll().AsNoTracking()
            .Where(m => m.SourceLogId != null && ids.Contains(m.SourceLogId.Value) && m.ReversesMovementId != null)
            .Select(m => m.ReversesMovementId.Value).ToListAsync();
        var parameters = await _waterQuality.GetParametersAsync();
        var names = await GetUserNamesAsync(logs.SelectMany(l => new[] { l.CreatorUserId, l.DeleterUserId }));
        var canEditAny = await CanEditAnyLogAsync();
        var canCreate = await IsGrantedAsync(PermissionNames.Logs_Create);
        var userId = AbpSession.UserId;
        bool Retired(LogType t) => t is LogType.PondPreparation or LogType.Feeding or LogType.PostProduction;

        return logs.Select(l => new LogDto
        {
            Id = l.Id,
            BatchId = l.BatchId,
            BatchCode = l.Batch?.BatchCode,
            PondCode = l.Batch?.Pond?.PondCode,
            LogType = l.LogType,
            ObservedAtUtc = l.ObservedAtUtc,
            Notes = l.Notes,
            ClientRequestId = l.ClientRequestId,
            RecorderUserId = l.CreatorUserId,
            RecordedBy = NameOf(names, l.CreatorUserId),
            CreationTime = l.CreationTime,
            LastModificationTime = l.LastModificationTime,
            CanEdit = !l.IsDeleted && canCreate && !Retired(l.LogType) && l.Batch?.Status != BatchStatus.Closed && (canEditAny || l.CreatorUserId == userId),
            CanVoid = !l.IsDeleted && canEditAny && l.Batch?.Status != BatchStatus.Closed,
            IsVoided = l.IsDeleted,
            VoidReason = l.IsDeleted ? l.VoidReason : null,
            VoidedBy = l.IsDeleted ? NameOf(names, l.DeleterUserId) : null,
            VoidedAtUtc = l.IsDeleted ? l.DeletionTime : null,
            IsBatchClosed = l.Batch?.Status == BatchStatus.Closed,
            HasOutOfRange = l.Measurements.Any(m => m.IsOutOfRange),
            PondPreparation = l.PondPreparation == null ? null : new PondPreparationInput
            {
                ActivityType = l.PondPreparation.ActivityType, DrainingDate = l.PondPreparation.DrainingDate, DryingDays = l.PondPreparation.DryingDays,
                FillingDate = l.PondPreparation.FillingDate, SeawaterPct = l.PondPreparation.SeawaterPct, FreshwaterPct = l.PondPreparation.FreshwaterPct,
                Usages = usageRows.Where(m => m.SourceLogId == l.Id && !reversed.Contains(m.Id))
                    .Select(m => new UsageLineInput { LotId = m.LotId, Quantity = -m.Quantity }).ToList(),
            },
            Measurements = l.Measurements.OrderBy(m => parameters.TryGetValue(m.ParameterCode, out var p) ? p.SortOrder : 99).Select(m => new MeasurementDto
            {
                ParameterCode = m.ParameterCode,
                ParameterName = parameters.TryGetValue(m.ParameterCode, out var p) ? p.Name : m.ParameterCode,
                Value = m.Value,
                UnitCode = m.UnitCode,
                IsOutOfRange = m.IsOutOfRange,
                Side = m.Side,
                OptimalRange = parameters.TryGetValue(m.ParameterCode, out var p2) ? p2.DescribeOptimalRange() : null,
                Recommendation = m.IsOutOfRange && parameters.TryGetValue(m.ParameterCode, out var p3) ? p3.RecommendationFor(m.Value) : null,
            }).ToList(),
            Growth = l.GrowthSampling == null ? null : new GrowthInput
            {
                SampleCount = l.GrowthSampling.SampleCount, TotalSampleWeightG = l.GrowthSampling.TotalSampleWeightG, EstimatedSurvivalPct = l.GrowthSampling.EstimatedSurvivalPct,
            },
            AbwG = l.GrowthSampling?.AbwG,
            EstimatedSurvivalPct = l.GrowthSampling?.EstimatedSurvivalPct,
            IsHighMortality = l.Mortality?.IsHighMortality ?? false,
            ChemicalApplication = l.ChemicalApplication == null ? null : new ChemicalApplicationInput
            {
                Reason = l.ChemicalApplication.Reason,
                Dosage = l.ChemicalApplication.Dosage, DosageUnit = l.ChemicalApplication.DosageUnit, Method = l.ChemicalApplication.Method,
                EndDate = l.ChemicalApplication.EndDate, WithdrawalDays = l.ChemicalApplication.WithdrawalDays, PrescriptionRef = l.ChemicalApplication.PrescriptionRef,
                Usages = usageRows.Where(m => m.SourceLogId == l.Id && !reversed.Contains(m.Id)).Select(m => new UsageLineInput { LotId = m.LotId, Quantity = -m.Quantity }).ToList(),
            },
            SafeHarvestDate = l.ChemicalApplication == null ? null
                : ChemicalApplicationDetail.SafeHarvestDate(FarmClock.LocalDate(l.ObservedAtUtc, mapZone), l.ChemicalApplication.EndDate, l.ChemicalApplication.WithdrawalDays),
            Mortality = l.Mortality == null ? null : new MortalityInput
            {
                Count = l.Mortality.Count, Cause = l.Mortality.Cause, DiseaseName = l.Mortality.DiseaseName, OtherCause = l.Mortality.OtherCause,
                Symptoms = l.Mortality.Symptoms, ResponseAction = l.Mortality.ResponseAction,
            },
            PostProduction = l.PostProduction == null ? null : new PostProductionInput { ActivityType = l.PostProduction.ActivityType },
            Usages = usageRows.Where(m => m.SourceLogId == l.Id && !reversed.Contains(m.Id)).Select(m => new UsageDto
            {
                LotId = m.LotId, Label = $"{m.Lot.Item.DisplayName} — lot {m.Lot.LotNumber}", Quantity = -m.Quantity, Unit = m.Lot.Item.Unit,
            }).ToList(),
        }).ToList();
    }

    public static string Describe(LogType type) => type switch
    {
        LogType.PondPreparation => "pond preparation",
        LogType.PreStockingWaterQuality => "pre-stocking water quality",
        LogType.Feeding => "feeding",
        LogType.WaterQuality => "water quality",
        LogType.GrowthSampling => "growth sampling",
        LogType.ChemicalApplication => "chemical and supplement application",
        LogType.Mortality => "mortality",
        LogType.PostProduction => "post-production",
        LogType.GeneralNote => "general note",
        _ => type.ToString(),
    };
}
