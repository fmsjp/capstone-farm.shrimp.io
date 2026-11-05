using Abp.Domain.Entities;
using System;
using System.Threading.Tasks;

namespace Capstone.Domain;

public enum NumberSequenceType { CycleCode = 1, DispatchReceipt = 2 }

public class NumberSequence : Entity<Guid>, IMustHaveTenant
{
    public const int MaxScopeLength = 30;

    public int TenantId { get; set; }
    public NumberSequenceType SequenceType { get; set; }

    public string Scope { get; set; }

    public int NextValue { get; set; } = 1;
}

public interface INumberSequenceGenerator
{
    Task<int> NextAsync(NumberSequenceType type, string scope);
}
