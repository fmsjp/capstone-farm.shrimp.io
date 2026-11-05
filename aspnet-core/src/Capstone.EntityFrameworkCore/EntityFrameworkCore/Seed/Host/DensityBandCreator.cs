using Capstone.Ponds;
using Capstone.ReferenceData;
using System.Linq;

namespace Capstone.EntityFrameworkCore.Seed.Host;

public class DensityBandCreator
{
    public const string Source = "PNS/BAFS 197:2025 Annex C";

    private readonly CapstoneDbContext _context;

    public DensityBandCreator(CapstoneDbContext context)
    {
        _context = context;
    }

    public void Create()
    {
        foreach (var construction in new[] { PondConstructionType.Earthen, PondConstructionType.Concrete, PondConstructionType.HdpeLined })
        {
            Add(FarmingSystem.Extensive, construction, 1m, 30m);
            Add(FarmingSystem.SemiIntensive, construction, 40m, 60m);
            if (construction == PondConstructionType.Earthen)
            {
                Add(FarmingSystem.Intensive, construction, 70m, 90m);
            }
            else
            {
                Add(FarmingSystem.Intensive, construction, 100m, 140m);
            }
        }

        _context.SaveChanges();
    }

    private void Add(FarmingSystem system, PondConstructionType construction, decimal? min, decimal? max)
    {
        if (_context.DensityBands.Any(b => b.FarmingSystem == system && b.ConstructionType == construction))
        {
            return;
        }

        _context.DensityBands.Add(new DensityBand { FarmingSystem = system, ConstructionType = construction, MinPlM2 = min, MaxPlM2 = max, Source = Source });
    }
}
