using Capstone.Domain;

namespace Capstone.Harvests;

public class SizeGrade : FarmEntity
{
    public const int MaxLabelLength = 20;
    public const int MaxCount = 1000;

    public string Label { get; set; }
    public int? MinCountPerKg { get; set; }
    public int? MaxCountPerKg { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public const string UngradedLabel = "Ungraded / mixed";

    public bool IsUngraded => !MinCountPerKg.HasValue && !MaxCountPerKg.HasValue;

    public decimal? MidCountPerKg => MinCountPerKg.HasValue && MaxCountPerKg.HasValue ? (MinCountPerKg.Value + MaxCountPerKg.Value) / 2m
        : MinCountPerKg ?? MaxCountPerKg;
}
