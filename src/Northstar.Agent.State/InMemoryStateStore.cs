using System.Collections.Concurrent;

namespace Northstar.Agent.State;

/// <summary>Lost on restart and not shared between instances. Fine for local development and tests only.</summary>
public sealed class InMemoryStateStore : IStateStore
{
    private readonly ConcurrentDictionary<string, ConversationState> _states = new();

    public Task<ConversationState> GetAsync(string conversationId, CancellationToken cancellationToken) =>
        Task.FromResult(_states.GetValueOrDefault(conversationId, ConversationState.Empty));

    public Task SaveAsync(string conversationId, ConversationState state, CancellationToken cancellationToken)
    {
        _states[conversationId] = state;
        return Task.CompletedTask;
    }
}
