using System.Collections.Generic;

namespace Capstone.ReferenceData;

public static class DefaultRecommendations
{
    public static readonly IReadOnlyDictionary<string, (string Low, string High)> ByParameter = new Dictionary<string, (string, string)>
    {
        [ParameterCodes.DO] = (
            "Run more aerators now, reduce or skip the next feeding, and check DO again within 1 hour. If it stays low, exchange part of the water.",
            "Very high DO usually means a heavy plankton bloom. Watch for a sudden drop at night, keep aerators ready, and consider a partial water exchange."),
        [ParameterCodes.Temperature] = (
            "Reduce feeding (shrimp eat less in cool water), keep aerators running, and raise the water level if the pond is shallow.",
            "Raise the water level or exchange water early in the morning, keep aerators running, and reduce feeding during the hottest hours."),
        [ParameterCodes.PH] = (
            "Apply agricultural lime as advised, check alkalinity, and test pH again the next morning.",
            "Do not add lime. Reduce feeding, consider a partial water exchange, check for a heavy plankton bloom, and test again early in the morning."),
        [ParameterCodes.Salinity] = (
            "Reduce freshwater inflow, add seawater if available, and watch the shrimp for stress and soft shells.",
            "Add freshwater gradually (about 2–3 ppt per day at most) and watch the shrimp for stress."),
        [ParameterCodes.Tan] = (
            null,
            "Reduce or skip feeding, increase aeration, exchange part of the water, and consider a probiotic. Test again within 24 hours."),
        [ParameterCodes.Nitrite] = (
            null,
            "Reduce feeding, increase aeration, exchange part of the water, and test again within 24 hours."),
        [ParameterCodes.Alkalinity] = (
            "Apply agricultural lime or sodium bicarbonate as advised and test again in 2–3 days.",
            null),
    };
}
