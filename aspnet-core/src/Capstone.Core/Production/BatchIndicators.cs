using System;
using System.Collections.Generic;
using System.Linq;

namespace Capstone.Production;

public class BatchFacts
{
    public long PlCountStocked { get; set; }
    public decimal PondAreaM2 { get; set; }
    public DateOnly? StockingDate { get; set; }
    public decimal CumulativeFeedKg { get; set; }
    public long CumulativeMortality { get; set; }

    public List<MortalityFact> Mortalities { get; set; } = new();

    public List<GrowthSampleFact> GrowthSamples { get; set; } = new();
    public List<HarvestFact> Harvests { get; set; } = new();
}

public class GrowthSampleFact
{
    public DateOnly Date { get; set; }
    public DateTime ObservedAtUtc { get; set; }
    public decimal AbwG { get; set; }
    public decimal? EstimatedSurvivalPct { get; set; }
}

public class MortalityFact
{
    public DateTime ObservedAtUtc { get; set; }
    public long Count { get; set; }
}

public class HarvestFact
{
    public DateOnly Date { get; set; }
    public DateTime HarvestedAtUtc { get; set; }

    public bool IsFinal { get; set; }

    public decimal TotalKg { get; set; }
    public decimal? AbwG { get; set; }
    public long? FinalCount { get; set; }

    public List<(decimal Kg, decimal? MidCountPerKg)> GradeLines { get; set; } = new();

    public static HarvestFact From(Harvests.Harvest h, DateOnly date) => new()
    {
        HarvestedAtUtc = h.HarvestedAtUtc,
        Date = date,
        IsFinal = h.HarvestType == Harvests.HarvestType.Total,
        TotalKg = h.TotalKg,
        AbwG = h.AbwG,
        FinalCount = h.FinalCount,
        GradeLines = h.GradeLines.Select(g => (g.Kg, g.SizeGrade?.MidCountPerKg)).ToList(),
    };
}

public class BatchIndicators
{
    public decimal? StockingDensityPerM2 { get; set; }
    public int? Doc { get; set; }
    public decimal CumulativeFeedKg { get; set; }
    public decimal? LatestAbwG { get; set; }
    public decimal? CurrentAbwG { get; set; }
    public long CumulativeMortality { get; set; }
    public decimal HarvestedPieces { get; set; }
    public decimal TotalHarvestKg { get; set; }
    public decimal? EstimatedPopulation { get; set; }
    public decimal? EstimatedBiomassKg { get; set; }
    public decimal? FcrToDate { get; set; }
    public decimal? FinalFcr { get; set; }
    public decimal? SurvivalRatePct { get; set; }
    public decimal? EstimatedSurvivalPct { get; set; }

    public string PopulationBasis { get; set; }

    public int? CultureDays { get; set; }
    public decimal? FinalAbwG { get; set; }
}

public static class IndicatorCalculator
{
    public static decimal? StockingDensity(long? plCountStocked, decimal areaM2) =>
        plCountStocked.HasValue && areaM2 > 0 ? Round(plCountStocked.Value / areaM2, 2) : null;

    public static int? Doc(DateOnly? stockingDate, DateOnly onDate) =>
        stockingDate.HasValue ? onDate.DayNumber - stockingDate.Value.DayNumber : null;

    public static decimal Abw(decimal totalSampleWeightG, int sampleCount) =>
        sampleCount > 0 ? Round(totalSampleWeightG / sampleCount, 3) : 0m;

    public static decimal? GrowthRate(decimal previousAbw, DateOnly previousDate, decimal abw, DateOnly date)
    {
        var days = date.DayNumber - previousDate.DayNumber;
        return days > 0 ? Round((abw - previousAbw) / days, 3) : null;
    }

    public static decimal HarvestedPieces(HarvestFact harvest, decimal? fallbackAbwG)
    {
        if (harvest.FinalCount.HasValue)
        {
            return harvest.FinalCount.Value;
        }

        var graded = harvest.GradeLines.Where(g => g.MidCountPerKg.HasValue).Sum(g => g.Kg * g.MidCountPerKg!.Value);
        var ungradedKg = harvest.GradeLines.Where(g => !g.MidCountPerKg.HasValue).Sum(g => g.Kg)
                         + Math.Max(0m, harvest.TotalKg - harvest.GradeLines.Sum(g => g.Kg));
        var abw = harvest.AbwG ?? fallbackAbwG;
        var ungraded = ungradedKg > 0 && abw.HasValue && abw.Value > 0 ? ungradedKg * 1000m / abw.Value : 0m;
        return graded + ungraded;
    }

    public static BatchIndicators Compute(BatchFacts f, DateOnly today)
    {
        var result = new BatchIndicators
        {
            StockingDensityPerM2 = StockingDensity(f.PlCountStocked > 0 ? f.PlCountStocked : null, f.PondAreaM2),
            Doc = Doc(f.StockingDate, today),
            CumulativeFeedKg = f.CumulativeFeedKg,
            CumulativeMortality = f.CumulativeMortality,
        };

        var latestSample = f.GrowthSamples.OrderBy(s => s.ObservedAtUtc).LastOrDefault();
        result.LatestAbwG = latestSample?.AbwG;

        var harvests = f.Harvests.OrderBy(h => h.HarvestedAtUtc).ToList();
        result.TotalHarvestKg = harvests.Sum(h => h.TotalKg);
        result.HarvestedPieces = Round(harvests.Sum(h => HarvestedPieces(h, result.LatestAbwG)), 0);

        var latestHarvestWithAbw = harvests.LastOrDefault(h => h.AbwG.HasValue);
        result.CurrentAbwG = latestHarvestWithAbw != null && (latestSample == null || latestHarvestWithAbw.HarvestedAtUtc >= latestSample.ObservedAtUtc)
            ? latestHarvestWithAbw.AbwG
            : result.LatestAbwG;

        if (f.PlCountStocked > 0)
        {
            var estimate = f.GrowthSamples.Where(s => s.EstimatedSurvivalPct.HasValue).OrderBy(s => s.ObservedAtUtc).LastOrDefault();
            decimal population;
            if (estimate != null)
            {
                var after = estimate.ObservedAtUtc;
                var mortalityAfter = f.Mortalities.Where(m => m.ObservedAtUtc > after).Sum(m => m.Count);
                var harvestedAfter = harvests.Where(h => h.HarvestedAtUtc > after).Sum(h => HarvestedPieces(h, result.LatestAbwG));
                population = f.PlCountStocked * estimate.EstimatedSurvivalPct.Value / 100m - mortalityAfter - harvestedAfter;
                result.PopulationBasis = "Estimate";
            }
            else
            {
                population = f.PlCountStocked - f.CumulativeMortality - result.HarvestedPieces;
                result.PopulationBasis = "Mortality";
            }

            result.EstimatedPopulation = Math.Max(0m, Round(population, 0));
            result.EstimatedSurvivalPct = Round((result.EstimatedPopulation.Value + result.HarvestedPieces) / f.PlCountStocked * 100m, 2);
            result.SurvivalRatePct = result.HarvestedPieces > 0 ? Round(result.HarvestedPieces / f.PlCountStocked * 100m, 2) : null;
        }

        if (result.EstimatedPopulation.HasValue && result.LatestAbwG.HasValue)
        {
            result.EstimatedBiomassKg = Round(result.EstimatedPopulation.Value * result.LatestAbwG.Value / 1000m, 3);
        }

        var denominator = (result.EstimatedBiomassKg ?? 0m) + result.TotalHarvestKg;
        result.FcrToDate = denominator > 0 && f.CumulativeFeedKg > 0 ? Round(f.CumulativeFeedKg / denominator, 3) : null;
        result.FinalFcr = result.TotalHarvestKg > 0 ? Round(f.CumulativeFeedKg / result.TotalHarvestKg, 3) : null;

        var lastHarvest = harvests.LastOrDefault();
        if (lastHarvest != null && f.StockingDate.HasValue)
        {
            result.CultureDays = lastHarvest.Date.DayNumber - f.StockingDate.Value.DayNumber;
        }

        var finalHarvest = harvests.LastOrDefault(h => h.IsFinal && h.AbwG.HasValue) ?? latestHarvestWithAbw;
        result.FinalAbwG = finalHarvest?.AbwG ?? result.LatestAbwG;

        return result;
    }

    public static decimal Round(decimal value, int decimals) => Math.Round(value, decimals, MidpointRounding.AwayFromZero);
}
