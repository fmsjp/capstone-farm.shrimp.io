using Abp.Dependency;
using Abp.EntityFrameworkCore;
using Capstone.Health;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Capstone.EntityFrameworkCore;

public class DatabaseMetricsProvider : IDatabaseMetrics, ITransientDependency
{
    private readonly IDbContextProvider<CapstoneDbContext> _contextProvider;

    public DatabaseMetricsProvider(IDbContextProvider<CapstoneDbContext> contextProvider)
    {
        _contextProvider = contextProvider;
    }

    private class MetricsRow
    {
        public double? DataSizeGb { get; set; }
        public double? FreeDiskGb { get; set; }
        public double? FreeDiskPct { get; set; }
    }

    private const string Sql = @"
SELECT
  CAST((SELECT SUM(CAST(size AS bigint)) FROM sys.database_files WHERE type = 0) * 8.0 / 1048576 AS float) AS DataSizeGb,
  CAST(MIN(vs.available_bytes) / 1073741824.0 AS float) AS FreeDiskGb,
  CAST(MIN(CAST(vs.available_bytes AS float) / NULLIF(vs.total_bytes, 0)) * 100 AS float) AS FreeDiskPct
FROM sys.database_files f
CROSS APPLY sys.dm_os_volume_stats(DB_ID(), f.file_id) vs";

    public async Task<DatabaseMetrics> GetAsync()
    {
        try
        {
            var context = await _contextProvider.GetDbContextAsync();
            if (!context.Database.IsSqlServer())
            {
                return new DatabaseMetrics();
            }

            var row = (await context.Database.SqlQueryRaw<MetricsRow>(Sql).ToListAsync()).FirstOrDefault();
            return row == null ? new DatabaseMetrics() : new DatabaseMetrics { DataSizeGb = row.DataSizeGb, FreeDiskGb = row.FreeDiskGb, FreeDiskPct = row.FreeDiskPct };
        }
        catch (Exception)
        {
            return new DatabaseMetrics();
        }
    }
}
