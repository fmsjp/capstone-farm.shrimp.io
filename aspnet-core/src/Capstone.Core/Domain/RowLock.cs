using System;
using System.Threading.Tasks;

namespace Capstone.Domain;

public enum RowLockTarget { FryDelivery = 1, HarvestGradeLine = 2 }

public interface IRowLocker
{
    Task LockAsync(RowLockTarget target, Guid id);
}
