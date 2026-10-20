using System.Text.RegularExpressions;

namespace Northstar.Agent.Model;

/// <summary>Deterministic, rule-based stand-in for a model. Same input, same intent, no network, no key.</summary>
public sealed partial class FakeModelClient : IModelClient
{
    [GeneratedRegex(@"\bNS-\d+\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OrderIdPattern();

    [GeneratedRegex(@"\b(hi|hello|hey|good (morning|afternoon|evening))\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GreetingPattern();

    [GeneratedRegex(@"\b(return|refund|send (it )?back|exchange)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReturnPattern();

    [GeneratedRegex(@"\b(recent|my orders|list (my )?orders|order history|all (my )?orders)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ListPattern();

    [GeneratedRegex(@"\b(where|status|track|tracking|shipped|delivery|arrive|what about)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex StatusPattern();

    [GeneratedRegex(@"\b(help|what can you do)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HelpPattern();

    public Task<Intent> InterpretAsync(string userText, ModelContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Task.FromResult(Interpret(userText ?? string.Empty, context));
    }

    private static Intent Interpret(string text, ModelContext context)
    {
        var idMatch = OrderIdPattern().Match(text);
        var explicitId = idMatch.Success ? idMatch.Value : null;

        if (ReturnPattern().IsMatch(text))
        {
            return new Intent(IntentKind.ReturnInfo, explicitId ?? context.LastOrderId);
        }

        if (ListPattern().IsMatch(text))
        {
            return new Intent(IntentKind.ListOrders);
        }

        if (explicitId is not null || StatusPattern().IsMatch(text))
        {
            return new Intent(IntentKind.OrderStatus, explicitId ?? context.LastOrderId);
        }

        if (HelpPattern().IsMatch(text))
        {
            return new Intent(IntentKind.Help);
        }

        if (GreetingPattern().IsMatch(text))
        {
            return new Intent(IntentKind.Greeting);
        }

        return new Intent(IntentKind.Unknown);
    }
}
