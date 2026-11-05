using Abp.UI;

namespace Capstone.Domain;

public static class FpmsErrors
{
    public static UserFriendlyException Rule(string message, string details = null) => new(message, details);

    public static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new UserFriendlyException(message);
        }
    }

    public const string ReadOnlyClosed = "This batch is Closed. Its records are read-only.";
    public const string ConcurrencyConflict = "This record was changed by someone else. Refresh the page and try again.";
}
