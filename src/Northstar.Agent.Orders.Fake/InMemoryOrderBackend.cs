using Northstar.Agent.Actions;

namespace Northstar.Agent.Orders.Fake;

public sealed class InMemoryOrderBackend(IEnumerable<Order> orders) : IOrderBackend
{
    private readonly IReadOnlyList<Order> _orders = orders.ToList();

    public Task<Order?> FindOrderAsync(string orderId, CancellationToken cancellationToken) =>
        Task.FromResult(_orders.FirstOrDefault(o => o.OrderId == orderId));

    public Task<IReadOnlyList<Order>> ListOrdersForCustomerAsync(string customerId, int maxCount, CancellationToken cancellationToken)
    {
        IReadOnlyList<Order> result = _orders
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.PlacedAt)
            .Take(maxCount)
            .ToList();
        return Task.FromResult(result);
    }
}
