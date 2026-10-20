using Northstar.Agent.Actions;
using Northstar.Agent.Orders.Fake;

namespace Actions.Tests;

public sealed class CountingBackend(IOrderBackend inner) : IOrderBackend
{
    public int Calls { get; private set; }

    public Task<Order?> FindOrderAsync(string orderId, CancellationToken cancellationToken)
    {
        Calls++;
        return inner.FindOrderAsync(orderId, cancellationToken);
    }

    public Task<IReadOnlyList<Order>> ListOrdersForCustomerAsync(string customerId, int maxCount, CancellationToken cancellationToken)
    {
        Calls++;
        return inner.ListOrdersForCustomerAsync(customerId, maxCount, cancellationToken);
    }
}

/// <summary>A backend that ignores the customer filter, to prove the action does not rely on it.</summary>
public sealed class LeakyBackend(IReadOnlyList<Order> all) : IOrderBackend
{
    public Task<Order?> FindOrderAsync(string orderId, CancellationToken cancellationToken) =>
        Task.FromResult(all.FirstOrDefault(o => o.OrderId == orderId));

    public Task<IReadOnlyList<Order>> ListOrdersForCustomerAsync(string customerId, int maxCount, CancellationToken cancellationToken) =>
        Task.FromResult(all);
}

public static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    public static IReadOnlyList<Order> Orders => OrderFixtures.Create(Now);

    public static CallerIdentity Alice => new(OrderFixtures.AliceCustomerId);

    public static CallerIdentity Bob => new(OrderFixtures.BobCustomerId);
}
