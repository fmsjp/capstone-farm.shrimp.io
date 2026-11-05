using Capstone.Domain;
using System;

namespace Capstone.Production;

internal static class Concurrency
{
    public static string Encode(byte[] value) => value == null || value.Length == 0 ? null : Convert.ToBase64String(value);

    public static void Check(byte[] current, string fromClient)
    {
        if (!string.IsNullOrEmpty(fromClient) && current != null && current.Length > 0 && Convert.ToBase64String(current) != fromClient)
        {
            throw FpmsErrors.Rule(FpmsErrors.ConcurrencyConflict);
        }
    }
}
