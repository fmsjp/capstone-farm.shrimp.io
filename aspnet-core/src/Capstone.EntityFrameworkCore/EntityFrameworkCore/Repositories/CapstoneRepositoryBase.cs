using Abp.Domain.Entities;
using Abp.Domain.Repositories;
using Abp.EntityFrameworkCore;
using Abp.EntityFrameworkCore.Repositories;

namespace Capstone.EntityFrameworkCore.Repositories;

public abstract class CapstoneRepositoryBase<TEntity, TPrimaryKey> : EfCoreRepositoryBase<CapstoneDbContext, TEntity, TPrimaryKey>
    where TEntity : class, IEntity<TPrimaryKey>
{
    protected CapstoneRepositoryBase(IDbContextProvider<CapstoneDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

}

public abstract class CapstoneRepositoryBase<TEntity> : CapstoneRepositoryBase<TEntity, int>, IRepository<TEntity>
    where TEntity : class, IEntity<int>
{
    protected CapstoneRepositoryBase(IDbContextProvider<CapstoneDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

}
