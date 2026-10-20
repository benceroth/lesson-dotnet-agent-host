namespace Northstar.Agent.Actions;

public enum ActionOutcome
{
    Success,
    InvalidInput,
    NotFound,

    /// <summary>The order exists but belongs to someone else. Callers must present this exactly like <see cref="NotFound"/>.</summary>
    NotAuthorized,

    /// <summary>The business rule says no (for example, return window closed).</summary>
    NotEligible,
}

public sealed record ActionResult<T>(ActionOutcome Outcome, T? Value = default, string? Detail = null)
{
    public bool IsSuccess => Outcome == ActionOutcome.Success;

    public static ActionResult<T> Ok(T value) => new(ActionOutcome.Success, value);

    public static ActionResult<T> Fail(ActionOutcome outcome, string? detail = null) => new(outcome, default, detail);
}

/// <summary>Limited projection of an order: no customer id, no address, no payment data.</summary>
public sealed record OrderStatusView(string OrderId, OrderStatus Status, string? Carrier, string? TrackingNumber, DateTimeOffset PlacedAt);

public sealed record OrderSummary(string OrderId, OrderStatus Status, DateTimeOffset PlacedAt);

public sealed record ReturnInfo(string OrderId, DateTimeOffset ReturnByDate, string Instructions);
