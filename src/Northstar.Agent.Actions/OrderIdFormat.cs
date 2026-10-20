using System.Text.RegularExpressions;

namespace Northstar.Agent.Actions;

public static partial class OrderIdFormat
{
    [GeneratedRegex(@"^NS-\d{4,6}$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();

    /// <summary>Normalizes (trim, upper-case) and validates. Anything that is not exactly NS-#### to NS-###### is rejected.</summary>
    public static bool TryNormalize(string? raw, out string orderId)
    {
        orderId = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var candidate = raw.Trim().ToUpperInvariant();
        if (!Pattern().IsMatch(candidate))
        {
            return false;
        }

        orderId = candidate;
        return true;
    }
}
