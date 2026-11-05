using Abp.Application.Services.Dto;
using Abp.Auditing;
using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.EntityHistory;
using Capstone.Authorization;
using Capstone.Domain;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Capstone.ActivityLogs;

public class ActivityLogInput : Capstone.Common.PagedInput
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public long? UserId { get; set; }

    public string RecordType { get; set; }
}

public class ActionLogDto
{
    public DateTime ExecutionTime { get; set; }
    public string UserName { get; set; }
    public string Area { get; set; }
    public string Action { get; set; }
    public int DurationMs { get; set; }
    public bool Failed { get; set; }
    public string ClientIpAddress { get; set; }
}

public class DataChangeDto
{
    public DateTime ChangeTime { get; set; }
    public string UserName { get; set; }
    public string RecordType { get; set; }
    public string ChangeType { get; set; }
    public string RecordId { get; set; }
    public List<string> Changes { get; set; } = new();
}

[AbpAuthorize(PermissionNames.ActivityLog)]
public class ActivityLogAppService : CapstoneAppServiceBase
{
    private readonly IRepository<AuditLog, long> _auditLogs;
    private readonly IRepository<EntityChangeSet, long> _changeSets;
    private readonly IRepository<EntityChange, long> _changes;
    private readonly IRepository<EntityPropertyChange, long> _propertyChanges;

    public ActivityLogAppService(IRepository<AuditLog, long> auditLogs, IRepository<EntityChangeSet, long> changeSets,
        IRepository<EntityChange, long> changes, IRepository<EntityPropertyChange, long> propertyChanges)
    {
        _auditLogs = auditLogs;
        _changeSets = changeSets;
        _changes = changes;
        _propertyChanges = propertyChanges;
    }

    private async Task<(DateTime? From, DateTime? To)> RangeAsync(ActivityLogInput input)
    {
        var zone = await Farm.GetTimeZoneAsync();
        return (input.From.HasValue ? FarmClock.StartOfDayUtc(input.From.Value, zone) : null,
            input.To.HasValue ? FarmClock.StartOfDayUtc(input.To.Value.AddDays(1), zone) : null);
    }

    public virtual async Task<PagedResultDto<ActionLogDto>> GetActions(ActivityLogInput input)
    {
        var (fromUtc, toUtc) = await RangeAsync(input);
        var query = _auditLogs.GetAll().AsNoTracking()
            .Where(a => a.UserId != null)
            .Where(a => !input.UserId.HasValue || a.UserId == input.UserId)
            .Where(a => !fromUtc.HasValue || a.ExecutionTime >= fromUtc)
            .Where(a => !toUtc.HasValue || a.ExecutionTime < toUtc)
            .Where(a => !a.MethodName.StartsWith("Get"));
        var total = await query.CountAsync();
        var rows = await query.OrderByDescending(a => a.ExecutionTime).Skip(input.SkipCount).Take(input.MaxResultCount)
            .Select(a => new { a.ExecutionTime, a.UserId, a.ServiceName, a.MethodName, a.ExecutionDuration, a.Exception, a.ClientIpAddress })
            .ToListAsync();
        var names = await GetUserNamesAsync(rows.Select(a => a.UserId));
        return new PagedResultDto<ActionLogDto>(total, rows.Select(a => new ActionLogDto
        {
            ExecutionTime = a.ExecutionTime, UserName = NameOf(names, a.UserId), Area = Short(a.ServiceName).Replace("AppService", string.Empty),
            Action = a.MethodName, DurationMs = a.ExecutionDuration, Failed = !string.IsNullOrEmpty(a.Exception), ClientIpAddress = a.ClientIpAddress,
        }).ToList());
    }

    public virtual async Task<PagedResultDto<DataChangeDto>> GetDataChanges(ActivityLogInput input)
    {
        var (fromUtc, toUtc) = await RangeAsync(input);
        var query = from c in _changes.GetAll().AsNoTracking()
                    join s in _changeSets.GetAll().AsNoTracking() on c.EntityChangeSetId equals s.Id
                    where (!input.UserId.HasValue || s.UserId == input.UserId)
                          && (!fromUtc.HasValue || c.ChangeTime >= fromUtc)
                          && (!toUtc.HasValue || c.ChangeTime < toUtc)
                          && (input.RecordType == null || c.EntityTypeFullName.EndsWith("." + input.RecordType))
                    orderby c.ChangeTime descending
                    select new { c.Id, c.ChangeTime, c.ChangeType, c.EntityTypeFullName, c.EntityId, s.UserId };
        var total = await query.CountAsync();
        var changes = await query.Skip(input.SkipCount).Take(input.MaxResultCount).ToListAsync();
        var changeIds = changes.Select(c => c.Id).ToList();
        var props = await _propertyChanges.GetAll().AsNoTracking().Where(p => changeIds.Contains(p.EntityChangeId))
            .Select(p => new { p.EntityChangeId, p.PropertyName, p.NewValue }).ToListAsync();
        var names = await GetUserNamesAsync(changes.Select(c => c.UserId));
        return new PagedResultDto<DataChangeDto>(total, changes.Select(c => new DataChangeDto
        {
            ChangeTime = c.ChangeTime, UserName = NameOf(names, c.UserId), RecordType = Short(c.EntityTypeFullName), ChangeType = c.ChangeType.ToString(), RecordId = c.EntityId,
            Changes = props.Where(p => p.EntityChangeId == c.Id).Select(p => $"{p.PropertyName} = {Trim(p.NewValue)}").ToList(),
        }).ToList());
    }

    public virtual async Task<List<string>> GetRecordTypes()
    {
        var types = await _changes.GetAll().Select(c => c.EntityTypeFullName).Distinct().ToListAsync();
        return types.Select(Short).Distinct().OrderBy(x => x).ToList();
    }

    private static string Short(string fullName) => string.IsNullOrEmpty(fullName) ? fullName : fullName[(fullName.LastIndexOf('.') + 1)..];

    private static string Trim(string value) => value == null ? "(empty)" : value.Length > 80 ? value[..80] + "…" : value.Trim('"');
}
