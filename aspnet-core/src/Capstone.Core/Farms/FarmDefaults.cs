using System.Collections.Generic;

namespace Capstone.Farms;

public static class FarmDefaults
{
    public record DocumentPresetSeed(string Name, string DefaultIssuingAgency, bool IsRequired = false);

    public record DocumentCategorySeed(string Name, bool RequiresExpiryDate, IReadOnlyList<DocumentPresetSeed> Presets);

    public record PondNamePrefixSeed(string Name, string ShortCode);

    public static readonly IReadOnlyList<PondNamePrefixSeed> PondNamePrefixes = new[]
    {
        new PondNamePrefixSeed("Grow-out Pond", "GP"),
        new PondNamePrefixSeed("Nursery Pond", "NP"),
        new PondNamePrefixSeed("Reservoir", "R"),
    };

    public static readonly IReadOnlyList<DocumentCategorySeed> DocumentCategories = new[]
    {
        new DocumentCategorySeed("Business and Local Permits", true, new DocumentPresetSeed[]
        {
            new("Mayor's / Business Permit", "Municipal / City Business Permits and Licensing Office", IsRequired: true),
            new("Barangay Business Clearance", "Barangay"),
            new("DTI Business Name Registration", "Department of Trade and Industry (DTI)"),
        }),
        new DocumentCategorySeed("Fisheries (BFAR)", true, new DocumentPresetSeed[]
        {
            new("BFAR Fishpond / Aquafarm Registration", "BFAR Regional Office XII", IsRequired: true),
            new("BFAR Shrimp Grow-out Farm Certification (GAqP)", "BFAR Regional Office XII", IsRequired: true),
            new("Fish Health Certificate", "BFAR Fish Health Laboratory"),
        }),
        new DocumentCategorySeed("Environmental (DENR)", false, new DocumentPresetSeed[]
        {
            new("Environmental Compliance Certificate (ECC)", "DENR - EMB Regional Office XII", IsRequired: true),
            new("Certificate of Non-Coverage (CNC)", "DENR - EMB Regional Office XII"),
            new("Wastewater Discharge Permit", "DENR - EMB Regional Office XII"),
        }),
        new DocumentCategorySeed("Tax (BIR)", false, new DocumentPresetSeed[]
        {
            new("BIR Certificate of Registration (Form 2303)", "Bureau of Internal Revenue (BIR)"),
        }),
        new DocumentCategorySeed("Water Rights (NWRB)", false, new DocumentPresetSeed[]
        {
            new("Water Permit", "National Water Resources Board (NWRB)"),
        }),
        new DocumentCategorySeed("Land / Lease", true, new DocumentPresetSeed[]
        {
            new("Fishpond Lease Agreement (FLA)", "BFAR"),
        }),
        new DocumentCategorySeed("Health and Safety", true, new DocumentPresetSeed[]
        {
            new("Sanitary Permit", "Municipal / City Health Office"),
        }),
        new DocumentCategorySeed("Other", false, new DocumentPresetSeed[]
        {
            new("SEC Registration", "Securities and Exchange Commission (SEC)"),
            new("CDA Registration", "Cooperative Development Authority (CDA)"),
        }),
    };

    public record SizeGradeSeed(string Label, int MinCountPerKg, int MaxCountPerKg);

    public static readonly IReadOnlyList<SizeGradeSeed> SizeGrades = new[]
    {
        new SizeGradeSeed("30/40", 30, 40),
        new SizeGradeSeed("40/50", 40, 50),
        new SizeGradeSeed("50/60", 50, 60),
        new SizeGradeSeed("60/70", 60, 70),
        new SizeGradeSeed("70/80", 70, 80),
        new SizeGradeSeed("80/90", 80, 90),
        new SizeGradeSeed("90/100", 90, 100),
        new SizeGradeSeed("100/120", 100, 120),
    };
}
