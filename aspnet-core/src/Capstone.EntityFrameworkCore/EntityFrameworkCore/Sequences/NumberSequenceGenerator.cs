using Abp.Dependency;
using Abp.Domain.Uow;
using Abp.EntityFrameworkCore;
using Abp.UI;
using Capstone.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Capstone.EntityFrameworkCore.Sequences;

public class NumberSequenceGenerator : INumberSequenceGenerator, ITransientDependency
{
    private const string MergeSql = @"
MERGE FpmsNumberSequences WITH (HOLDLOCK) AS t
USING (SELECT @tenantId AS TenantId, @type AS SequenceType, @scope AS Scope) AS s
    ON t.TenantId = s.TenantId AND t.SequenceType = s.SequenceType AND t.Scope = s.Scope
WHEN MATCHED THEN UPDATE SET NextValue = t.NextValue + 1
WHEN NOT MATCHED THEN INSERT (Id, TenantId, SequenceType, Scope, NextValue) VALUES (NEWID(), s.TenantId, s.SequenceType, s.Scope, 2)
OUTPUT ISNULL(deleted.NextValue, 1) AS [Value];";

    private readonly IDbContextProvider<CapstoneDbContext> _dbContextProvider;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public NumberSequenceGenerator(IDbContextProvider<CapstoneDbContext> dbContextProvider, IUnitOfWorkManager unitOfWorkManager)
    {
        _dbContextProvider = dbContextProvider;
        _unitOfWorkManager = unitOfWorkManager;
    }

    public async Task<int> NextAsync(NumberSequenceType type, string scope)
    {
        if (string.IsNullOrWhiteSpace(scope) || scope.Length > NumberSequence.MaxScopeLength)
        {
            throw new ArgumentException($"Scope must be 1–{NumberSequence.MaxScopeLength} characters.", nameof(scope));
        }

        var tenantId = _unitOfWorkManager.Current?.GetTenantId()
                       ?? throw new UserFriendlyException("Numbers can be generated only inside a farm account.");
        var context = await _dbContextProvider.GetDbContextAsync();

        if (context.Database.IsSqlServer())
        {
            var values = await context.Database.SqlQueryRaw<int>(MergeSql,
                new SqlParameter("@tenantId", tenantId),
                new SqlParameter("@type", (int)type),
                new SqlParameter("@scope", scope)).ToListAsync();
            return values.Single();
        }

        var row = await context.NumberSequences.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.SequenceType == type && x.Scope == scope);
        if (row == null)
        {
            row = new NumberSequence { Id = Guid.NewGuid(), TenantId = tenantId, SequenceType = type, Scope = scope, NextValue = 1 };
            context.NumberSequences.Add(row);
        }

        var value = row.NextValue;
        row.NextValue++;
        await context.SaveChangesAsync();
        return value;
    }
}
