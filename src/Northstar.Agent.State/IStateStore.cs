namespace Northstar.Agent.State;

/// <summary>
/// The only things the agent remembers between turns. Order status is deliberately NOT here:
/// it is fetched from the order backend each time.
/// </summary>
public sealed record ConversationState(string? LastOrderId = null, int TurnCount = 0)
{
    public static ConversationState Empty { get; } = new();
}

public interface IStateStore
{
    Task<ConversationState> GetAsync(string conversationId, CancellationToken cancellationToken);

    Task SaveAsync(string conversationId, ConversationState state, CancellationToken cancellationToken);
}
