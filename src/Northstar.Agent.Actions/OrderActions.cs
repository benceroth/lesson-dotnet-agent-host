namespace Northstar.Agent.Actions;

public interface IOrderActions
{
    Task<ActionResult<OrderStatusView>> GetOrderStatusAsync(CallerIdentity caller, string orderId, CancellationToken cancellationToken);

    Task<ActionResult<IReadOnlyList<OrderSummary>>> ListRecentOrdersAsync(CallerIdentity caller, CancellationToken cancellationToken);

    Task<ActionResult<ReturnInfo>> RequestReturnInfoAsync(CallerIdentity caller, string orderId, CancellationToken cancellationToken);
}

/// <summary>
/// The only way the agent touches order data. Each action validates input, checks that the caller
/// owns the order, and returns a limited projection. Nothing here is probabilistic.
/// </summary>
public sealed class OrderActions(IOrderBackend backend, TimeProvider clock) : IOrderActions
{
    public const int RecentOrderLimit = 5;
    public static readonly TimeSpan ReturnWindow = TimeSpan.FromDays(30);

    public async Task<ActionResult<OrderStatusView>> GetOrderStatusAsync(CallerIdentity caller, string orderId, CancellationToken cancellationToken)
    {
        var loaded = await LoadOwnedOrderAsync<OrderStatusView>(caller, orderId, cancellationToken);
        if (loaded.Failure is { } failure)
        {
            return failure;
        }

        var o = loaded.Order!;
        return ActionResult<OrderStatusView>.Ok(new OrderStatusView(o.OrderId, o.Status, o.Carrier, o.TrackingNumber, o.PlacedAt));
    }

    public async Task<ActionResult<IReadOnlyList<OrderSummary>>> ListRecentOrdersAsync(CallerIdentity caller, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(caller?.CustomerId))
        {
            return ActionResult<IReadOnlyList<OrderSummary>>.Fail(ActionOutcome.NotAuthorized);
        }

        var orders = await backend.ListOrdersForCustomerAsync(caller.CustomerId, RecentOrderLimit, cancellationToken);

        // Defence in depth: never trust the backend filter alone.
        IReadOnlyList<OrderSummary> summaries = orders
            .Where(o => o.CustomerId == caller.CustomerId)
            .OrderByDescending(o => o.PlacedAt)
            .Take(RecentOrderLimit)
            .Select(o => new OrderSummary(o.OrderId, o.Status, o.PlacedAt))
            .ToList();
        return ActionResult<IReadOnlyList<OrderSummary>>.Ok(summaries);
    }

    public async Task<ActionResult<ReturnInfo>> RequestReturnInfoAsync(CallerIdentity caller, string orderId, CancellationToken cancellationToken)
    {
        var loaded = await LoadOwnedOrderAsync<ReturnInfo>(caller, orderId, cancellationToken);
        if (loaded.Failure is { } failure)
        {
            return failure;
        }

        var o = loaded.Order!;
        if (o.Status != OrderStatus.Delivered || o.DeliveredAt is null)
        {
            return ActionResult<ReturnInfo>.Fail(ActionOutcome.NotEligible, "Only delivered orders can be returned.");
        }

        var returnBy = o.DeliveredAt.Value + ReturnWindow;
        if (clock.GetUtcNow() > returnBy)
        {
            return ActionResult<ReturnInfo>.Fail(ActionOutcome.NotEligible, "The 30-day return window has closed.");
        }

        return ActionResult<ReturnInfo>.Ok(new ReturnInfo(o.OrderId, returnBy, "Use the prepaid label in your order confirmation email."));
    }

    private async Task<(Order? Order, ActionResult<T>? Failure)> LoadOwnedOrderAsync<T>(CallerIdentity caller, string orderId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(caller?.CustomerId))
        {
            return (null, ActionResult<T>.Fail(ActionOutcome.NotAuthorized));
        }

        if (!OrderIdFormat.TryNormalize(orderId, out var normalized))
        {
            return (null, ActionResult<T>.Fail(ActionOutcome.InvalidInput, "Order ids look like NS-1042."));
        }

        var order = await backend.FindOrderAsync(normalized, cancellationToken);
        if (order is null)
        {
            return (null, ActionResult<T>.Fail(ActionOutcome.NotFound));
        }

        if (order.CustomerId != caller.CustomerId)
        {
            return (null, ActionResult<T>.Fail(ActionOutcome.NotAuthorized));
        }

        return (order, null);
    }
}
