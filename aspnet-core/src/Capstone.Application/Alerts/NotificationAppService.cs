using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.Runtime.Session;
using Capstone.Authorization;
using Capstone.Compliance;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Capstone.Alerts;

public class NotificationDto
{
    public Guid Id { get; set; }
    public FarmNotificationType Type { get; set; }
    public string Message { get; set; }
    public string RelatedRecordType { get; set; }
    public Guid? RelatedRecordId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public bool IsRead { get; set; }
}

public class GetNotificationsInput
{
    public bool UnreadOnly { get; set; }
    public int MaxResultCount { get; set; } = 50;
}

public class MarkReadInput
{
    public List<Guid> Ids { get; set; } = new();
}

[AbpAuthorize(PermissionNames.Dashboard)]
public class NotificationAppService : CapstoneAppServiceBase
{
    private readonly IRepository<FarmNotification, Guid> _notifications;
    private readonly IRepository<FarmNotificationRead, Guid> _reads;
    private readonly IRepository<ComplianceDocument, Guid> _documents;
    private readonly FarmAlertPublisher _publisher;

    public NotificationAppService(IRepository<FarmNotification, Guid> notifications, IRepository<FarmNotificationRead, Guid> reads,
        IRepository<ComplianceDocument, Guid> documents, FarmAlertPublisher publisher)
    {
        _notifications = notifications;
        _reads = reads;
        _documents = documents;
        _publisher = publisher;
    }

    public virtual async Task<List<NotificationDto>> GetMine(GetNotificationsInput input)
    {
        var isOwner = await IsGrantedAsync(PermissionNames.Dashboard_Full);
        if (isOwner)
        {
            await RefreshDocumentAlertsAsync();
        }

        var userId = AbpSession.GetUserId();
        var readIds = _reads.GetAll().Where(r => r.UserId == userId).Select(r => r.NotificationId);
        var query = _notifications.GetAll().AsNoTracking().Where(n => isOwner || !n.OwnersOnly);
        if (input.UnreadOnly)
        {
            query = query.Where(n => !readIds.Contains(n.Id));
        }

        var list = await query.OrderByDescending(n => n.CreatedAtUtc).Take(Math.Clamp(input.MaxResultCount, 1, 200)).ToListAsync();
        var ids = list.Select(n => n.Id).ToList();
        var read = await _reads.GetAll().Where(r => r.UserId == userId && ids.Contains(r.NotificationId)).Select(r => r.NotificationId).ToListAsync();
        return list.Select(n => new NotificationDto
        {
            Id = n.Id, Type = n.Type, Message = n.Message, RelatedRecordType = n.RelatedRecordType, RelatedRecordId = n.RelatedRecordId,
            CreatedAtUtc = n.CreatedAtUtc, IsRead = read.Contains(n.Id),
        }).ToList();
    }

    public virtual async Task<int> GetUnreadCount()
    {
        var isOwner = await IsGrantedAsync(PermissionNames.Dashboard_Full);
        if (isOwner)
        {
            await RefreshDocumentAlertsAsync();
        }

        var userId = AbpSession.GetUserId();
        var readIds = _reads.GetAll().Where(r => r.UserId == userId).Select(r => r.NotificationId);
        return await _notifications.GetAll().Where(n => (isOwner || !n.OwnersOnly) && !readIds.Contains(n.Id)).CountAsync();
    }

    public virtual async Task MarkRead(MarkReadInput input)
    {
        var userId = AbpSession.GetUserId();
        var existing = await _reads.GetAll().Where(r => r.UserId == userId && input.Ids.Contains(r.NotificationId)).Select(r => r.NotificationId).ToListAsync();
        var valid = await _notifications.GetAll().Where(n => input.Ids.Contains(n.Id)).Select(n => n.Id).ToListAsync();
        foreach (var id in valid.Except(existing))
        {
            await _reads.InsertAsync(new FarmNotificationRead { Id = Guid.NewGuid(), NotificationId = id, UserId = userId, ReadAtUtc = DateTime.UtcNow });
        }
    }

    public virtual async Task MarkAllRead()
    {
        var isOwner = await IsGrantedAsync(PermissionNames.Dashboard_Full);
        var userId = AbpSession.GetUserId();
        var readIds = _reads.GetAll().Where(r => r.UserId == userId).Select(r => r.NotificationId);
        var ids = await _notifications.GetAll().Where(n => (isOwner || !n.OwnersOnly) && !readIds.Contains(n.Id)).Select(n => n.Id).ToListAsync();
        await MarkRead(new MarkReadInput { Ids = ids });
    }

    internal async Task RefreshDocumentAlertsAsync()
    {
        var profile = await Farm.GetProfileAsync();
        var today = await Farm.GetTodayAsync();
        var horizon = today.AddDays(profile.DocumentExpiryAlertDays);
        var docs = await _documents.GetAll().AsNoTracking().Include(d => d.Category)
            .Where(d => !d.IsArchived && d.ExpiryDate != null && d.ExpiryDate <= horizon).ToListAsync();
        foreach (var d in docs)
        {
            var status = d.GetExpiryStatus(today, profile.DocumentExpiryAlertDays);
            if (status == ExpiryStatus.Expired)
            {
                await _publisher.PublishAsync(FarmNotificationType.DocumentExpired, "ComplianceDocument", d.Id,
                    $"Expired: {d.Category?.Name} \"{d.Title}\" expired on {d.ExpiryDate:yyyy-MM-dd}.", ownersOnly: true, oncePerRecord: true);
            }
            else if (status == ExpiryStatus.ExpiringSoon)
            {
                await _publisher.PublishAsync(FarmNotificationType.DocumentExpiring, "ComplianceDocument", d.Id,
                    $"Expiring soon: {d.Category?.Name} \"{d.Title}\" expires on {d.ExpiryDate:yyyy-MM-dd}.", ownersOnly: true, oncePerRecord: true);
            }
        }

        await CurrentUnitOfWork.SaveChangesAsync();
    }
}
