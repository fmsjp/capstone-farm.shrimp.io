using System;

namespace Capstone.Domain;

public static class DuplicateKey
{
    public static bool Is(Exception exception)
    {
        for (var e = exception; e != null; e = e.InnerException)
        {
            if (e.GetType().Name == "SqlException" && e.GetType().GetProperty("Number")?.GetValue(e) is int number &&
                (number == 2601 || number == 2627))
            {
                return true;
            }
        }

        return false;
    }
}
