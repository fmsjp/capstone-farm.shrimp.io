using System;
using System.Collections.Generic;

namespace Capstone.Domain;

public static class Units
{
    public static readonly IReadOnlyList<string> InventoryUnits = new[] { "kg", "g", "L", "mL", "pcs" };

    public static bool IsValidInventoryUnit(string unit) =>
        unit != null && ((IList<string>)InventoryUnits).Contains(unit);

    public static decimal? Convert(decimal quantity, string fromUnit, string toUnit)
    {
        if (string.Equals(fromUnit, toUnit, StringComparison.Ordinal))
        {
            return quantity;
        }

        return (fromUnit, toUnit) switch
        {
            ("kg", "g") => quantity * 1000m,
            ("g", "kg") => quantity / 1000m,
            ("L", "mL") => quantity * 1000m,
            ("mL", "L") => quantity / 1000m,
            _ => null,
        };
    }
}
