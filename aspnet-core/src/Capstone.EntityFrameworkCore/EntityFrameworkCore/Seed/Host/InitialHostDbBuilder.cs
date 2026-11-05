namespace Capstone.EntityFrameworkCore.Seed.Host;

public class InitialHostDbBuilder
{
    private readonly CapstoneDbContext _context;

    public InitialHostDbBuilder(CapstoneDbContext context)
    {
        _context = context;
    }

    public void Create()
    {
        new DefaultEditionCreator(_context).Create();
        new DefaultLanguagesCreator(_context).Create();
        new HostRoleAndUserCreator(_context).Create();
        new DefaultSettingsCreator(_context).Create();
        new ParameterDefinitionCreator(_context).Create();
        new DensityBandCreator(_context).Create();

        _context.SaveChanges();
    }
}
