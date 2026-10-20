using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Northstar.Agent.Actions;
using Northstar.Agent.Core;
using Northstar.Agent.Model;
using Northstar.Agent.Orders.Fake;
using Northstar.Agent.State;

namespace Core.Tests;

public sealed class CapturingLogger<T> : ILogger<T>
{
    public List<string> Messages { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Messages.Add(formatter(state, exception));
        if (exception is not null)
        {
            Messages.Add(exception.ToString());
        }
    }
}

public sealed class ThrowingBackend(Exception error) : IOrderBackend
{
    public Task<Order?> FindOrderAsync(string orderId, CancellationToken cancellationToken) => throw error;

    public Task<IReadOnlyList<Order>> ListOrdersForCustomerAsync(string customerId, int maxCount, CancellationToken cancellationToken) => throw error;
}

/// <summary>Never answers until cancelled, like a hung dependency.</summary>
public sealed class HangingBackend : IOrderBackend
{
    public async Task<Order?> FindOrderAsync(string orderId, CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return null;
    }

    public async Task<IReadOnlyList<Order>> ListOrdersForCustomerAsync(string customerId, int maxCount, CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return [];
    }
}

public sealed class ThrowingStateStore : IStateStore
{
    public Task<ConversationState> GetAsync(string conversationId, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("state store is down");

    public Task SaveAsync(string conversationId, ConversationState state, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("state store is down");
}

public sealed class ThrowingModel : IModelClient
{
    public Task<Intent> InterpretAsync(string userText, ModelContext context, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("model endpoint returned 500 secret-detail");
}

public sealed class Chat
{
    public const string Alice = OrderFixtures.AliceCustomerId;
    public const string Bob = OrderFixtures.BobCustomerId;

    public static readonly DateTimeOffset Now = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    public CapturingLogger<SupportResponder> Log { get; } = new();

    public SupportResponder Responder { get; }

    public string ConversationId { get; init; } = "conv-1";

    public string CustomerId { get; init; } = Alice;

    public Chat(
        IOrderBackend? backend = null,
        IStateStore? state = null,
        IModelClient? model = null,
        TimeSpan? timeout = null)
    {
        var clock = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(Now);
        var actions = new OrderActions(backend ?? new InMemoryOrderBackend(OrderFixtures.Create(Now)), clock);
        var options = Options.Create(new SupportResponderOptions { TurnTimeout = timeout ?? TimeSpan.FromSeconds(5) });
        Responder = new SupportResponder(model ?? new FakeModelClient(), actions, state ?? new InMemoryStateStore(), options, Log);
    }

    public async Task<SupportReply> SayAsync(string text, CancellationToken cancellationToken = default) =>
        await Responder.RespondAsync(new ConversationRequest(ConversationId, CustomerId, text), cancellationToken);
}
