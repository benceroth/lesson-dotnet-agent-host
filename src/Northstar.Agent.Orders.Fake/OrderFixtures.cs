using Northstar.Agent.Actions;

namespace Northstar.Agent.Orders.Fake;

public static class OrderFixtures
{
    public const string AliceCustomerId = "cust-alice";
    public const string BobCustomerId = "cust-bob";

    /// <summary>Fictional Northstar Outfitters data. Dates are relative to <paramref name="now"/> so the return window stays meaningful.</summary>
    public static IReadOnlyList<Order> Create(DateTimeOffset now) =>
    [
        new("NS-1042", AliceCustomerId, OrderStatus.Shipped, now.AddDays(-3), null, "FictionalExpress", "FX-000-1042",
            [new OrderLine("TENT-2P", "Two-person tent", 1)]),
        new("NS-1043", AliceCustomerId, OrderStatus.Delivered, now.AddDays(-12), now.AddDays(-8), "FictionalExpress", "FX-000-1043",
            [new OrderLine("BOOT-42", "Trail boots, size 42", 1)]),
        new("NS-1044", AliceCustomerId, OrderStatus.Processing, now.AddDays(-1), null, null, null,
            [new OrderLine("STOVE-1", "Camp stove", 1)]),
        new("NS-1001", AliceCustomerId, OrderStatus.Delivered, now.AddDays(-90), now.AddDays(-85), "FictionalExpress", "FX-000-1001",
            [new OrderLine("PACK-30", "30L daypack", 1)]),
        new("NS-2001", BobCustomerId, OrderStatus.Shipped, now.AddDays(-2), null, "FictionalExpress", "FX-000-2001",
            [new OrderLine("BAG-0C", "Sleeping bag, 0C", 1)]),
    ];
}
