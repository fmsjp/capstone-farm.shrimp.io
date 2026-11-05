using Microsoft.EntityFrameworkCore;
using System.Data.Common;

namespace Capstone.EntityFrameworkCore;

public static class CapstoneDbContextConfigurer
{
    private static void Sql(Microsoft.EntityFrameworkCore.Infrastructure.SqlServerDbContextOptionsBuilder sql)
    {
        sql.TranslateParameterizedCollectionsToConstants();
    }

    public static void Configure(DbContextOptionsBuilder<CapstoneDbContext> builder, string connectionString)
    {
        builder.UseSqlServer(connectionString, Sql);
    }

    public static void Configure(DbContextOptionsBuilder<CapstoneDbContext> builder, DbConnection connection)
    {
        builder.UseSqlServer(connection, Sql);
    }
}
