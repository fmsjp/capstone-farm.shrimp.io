using Capstone.Configuration;
using Capstone.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Capstone.EntityFrameworkCore;

public class CapstoneDbContextFactory : IDesignTimeDbContextFactory<CapstoneDbContext>
{
    public CapstoneDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<CapstoneDbContext>();

        var configuration = AppConfigurations.Get(WebContentDirectoryFinder.CalculateContentRootFolder(), addUserSecrets: true);

        CapstoneDbContextConfigurer.Configure(builder, configuration.GetConnectionString(CapstoneConsts.ConnectionStringName));

        return new CapstoneDbContext(builder.Options);
    }
}
