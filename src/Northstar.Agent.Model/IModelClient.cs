namespace Northstar.Agent.Model;

public enum IntentKind
{
    Greeting,
    Help,
    OrderStatus,
    ListOrders,
    ReturnInfo,
    Unknown,
}

/// <summary>
/// What the model thinks the user wants. <see cref="OrderId"/> is a raw, untrusted string:
/// the action layer validates it, whatever produced it.
/// </summary>
public sealed record Intent(IntentKind Kind, string? OrderId = null);

/// <summary>Conversation facts the model may use to resolve follow-ups. It receives no business data.</summary>
public sealed record ModelContext(string? LastOrderId);

/// <summary>Swap point for a real model. A real implementation must still only return an <see cref="Intent"/>.</summary>
public interface IModelClient
{
    Task<Intent> InterpretAsync(string userText, ModelContext context, CancellationToken cancellationToken);
}
