using Abp.Domain.Entities;
using Abp.Domain.Entities.Auditing;
using System;

namespace Capstone.Domain;

public abstract class FarmEntity : FullAuditedEntity<Guid>, IMustHaveTenant
{
    public int TenantId { get; set; }
}

public abstract class FarmChildEntity : Entity<Guid>, IMustHaveTenant
{
    public int TenantId { get; set; }
}
