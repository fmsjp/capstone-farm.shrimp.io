using Abp.Domain.Repositories;
using Abp.Domain.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Capstone.Alerts;

public class FarmAlertPublisher : DomainService
{
    private readonly IRepository<FarmNotification, Guid> _notifications;

    public FarmAlertPublisher(IRepository<FarmNotification, Guid> notifications)
    {
        _notifications = notifications;
    }

    public async Task PublishAsync(FarmNotificationType type, string recordType, Guid? recordId, string message, bool ownersOnly = false, bool oncePerRecord = false)
    {
        var since = DateTime.UtcNow.AddHours(-20);
        var existing = _notifications.GetAll().Where(n => n.Type == type && n.RelatedRecordId == recordId);
        if (!oncePerRecord)
        {
            existing = existing.Where(n => n.CreatedAtUtc >= since);
        }

        if (recordId.HasValue && await existing.AnyAsync())
        {
            return;
        }

        await _notifications.InsertAsync(new FarmNotification
        {
            Id = Guid.NewGuid(),
            Type = type,
            RelatedRecordType = recordType,
            RelatedRecordId = recordId,
            Message = message.Length > FarmNotification.MaxMessageLength ? message[..FarmNotification.MaxMessageLength] : message,
            CreatedAtUtc = DateTime.UtcNow,
            OwnersOnly = ownersOnly,
        });
    }
}
