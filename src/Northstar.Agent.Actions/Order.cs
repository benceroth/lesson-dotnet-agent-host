namespace Northstar.Agent.Actions;

/// <summary>Who is asking. Always derived by the host from the authenticated channel identity, never from message text.</summary>
public sealed record CallerIdentity(string CustomerId);

public enum OrderStatus
{
    Processing,
    Shipped,
    Delivered,
    Cancelled,
}

public sealed record OrderLine(string Sku, string Name, int Quantity);

/// <summary>Full backend record. Actions never hand this to callers; they project it to a narrow result.</summary>
public sealed record Order(
    string OrderId,
    string CustomerId,
    OrderStatus Status,
    DateTimeOffset PlacedAt,
    DateTimeOffset? DeliveredAt,
    string? Carrier,
    string? TrackingNumber,
    IReadOnlyList<OrderLine> Lines);

/// <summary>Read-only port to the authoritative order system. Deliberately has no write methods.</summary>
public interface IOrderBackend
{
    Task<Order?> FindOrderAsync(string orderId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Order>> ListOrdersForCustomerAsync(string customerId, int maxCount, CancellationToken cancellationToken);
}
