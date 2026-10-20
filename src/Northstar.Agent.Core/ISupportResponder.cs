namespace Northstar.Agent.Core;

/// <summary>Channel-agnostic input. <see cref="CustomerId"/> comes from authenticated identity mapping in the host.</summary>
public sealed record ConversationRequest(string ConversationId, string CustomerId, string? Text);

public enum ReplyRoute
{
    Welcome,
    Greeting,
    Help,
    OrderStatus,
    ListOrders,
    ReturnInfo,
    NeedOrderId,
    NotUnderstood,
    InvalidInput,
    NotFound,
    NotEligible,
    TooLong,
    Timeout,
    Failure,
}

public sealed record SupportReply(string Text, ReplyRoute Route);

/// <summary>What the transport handler depends on. Transport, reasoning and order lookup are tested separately.</summary>
public interface ISupportResponder
{
    string Welcome();

    Task<SupportReply> RespondAsync(ConversationRequest request, CancellationToken cancellationToken);
}

public sealed class SupportResponderOptions
{
    public const string SectionName = "Support";

    /// <summary>Upper bound for one turn (model + actions + state).</summary>
    public TimeSpan TurnTimeout { get; set; } = TimeSpan.FromSeconds(10);

    public int MaxMessageLength { get; set; } = 1000;
}
