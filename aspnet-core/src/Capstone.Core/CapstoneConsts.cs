using Capstone.Debugging;

namespace Capstone;

public class CapstoneConsts
{
    public const string LocalizationSourceName = "Capstone";

    public const string ConnectionStringName = "Default";

    public const bool MultiTenancyEnabled = true;

    public static readonly string DefaultPassPhrase =
        DebugHelper.IsDebug ? "gsKxGZ012HLL3MI5" : "daa8dc911d6e4c04a4e21e5449908765";
}
