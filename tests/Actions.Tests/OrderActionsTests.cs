using Microsoft.Extensions.Time.Testing;
using Northstar.Agent.Actions;
using Northstar.Agent.Orders.Fake;

namespace Actions.Tests;

public class OrderActionsTests
{
    private static (OrderActions Actions, CountingBackend Backend, FakeTimeProvider Clock) Create(IOrderBackend? backend = null)
    {
        var clock = new FakeTimeProvider(TestData.Now);
        var counting = new CountingBackend(backend ?? new InMemoryOrderBackend(TestData.Orders));
        return (new OrderActions(counting, clock), counting, clock);
    }

    [Theory]
    [InlineData("NS-1042", "NS-1042")]
    [InlineData("  ns-1042 ", "NS-1042")]
    [InlineData("NS-123456", "NS-123456")]
    public void OrderIdFormat_accepts_and_normalizes(string raw, string expected)
    {
        Assert.True(OrderIdFormat.TryNormalize(raw, out var id));
        Assert.Equal(expected, id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1042")]
    [InlineData("NS-12")]
    [InlineData("NS-1234567")]
    [InlineData("NS-1042; DROP TABLE orders")]
    [InlineData("NS-1042\nNS-2001")]
    [InlineData("../NS-1042")]
    public void OrderIdFormat_rejects_everything_else(string? raw) =>
        Assert.False(OrderIdFormat.TryNormalize(raw, out _));

    [Fact]
    public async Task GetOrderStatus_returns_a_limited_view_for_the_owner()
    {
        var (actions, _, _) = Create();

        var result = await actions.GetOrderStatusAsync(TestData.Alice, "ns-1042", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("NS-1042", result.Value!.OrderId);
        Assert.Equal(OrderStatus.Shipped, result.Value.Status);
        Assert.Equal("FX-000-1042", result.Value.TrackingNumber);
    }

    [Fact]
    public async Task GetOrderStatus_refuses_an_order_owned_by_someone_else()
    {
        var (actions, _, _) = Create();

        // NS-2001 exists, but belongs to Bob.
        var result = await actions.GetOrderStatusAsync(TestData.Alice, "NS-2001", CancellationToken.None);

        Assert.Equal(ActionOutcome.NotAuthorized, result.Outcome);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task RequestReturnInfo_refuses_an_order_owned_by_someone_else()
    {
        var (actions, _, _) = Create();

        var result = await actions.RequestReturnInfoAsync(TestData.Bob, "NS-1043", CancellationToken.None);

        Assert.Equal(ActionOutcome.NotAuthorized, result.Outcome);
        Assert.Null(result.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("NS-1042' OR '1'='1")]
    [InlineData("hello")]
    public async Task Invalid_order_ids_never_reach_the_backend(string orderId)
    {
        var (actions, backend, _) = Create();

        var result = await actions.GetOrderStatusAsync(TestData.Alice, orderId, CancellationToken.None);

        Assert.Equal(ActionOutcome.InvalidInput, result.Outcome);
        Assert.Equal(0, backend.Calls);
    }

    [Fact]
    public async Task Unknown_order_is_not_found()
    {
        var (actions, _, _) = Create();

        var result = await actions.GetOrderStatusAsync(TestData.Alice, "NS-9999", CancellationToken.None);

        Assert.Equal(ActionOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task Missing_caller_identity_is_refused_before_any_lookup()
    {
        var (actions, backend, _) = Create();

        var status = await actions.GetOrderStatusAsync(new CallerIdentity(""), "NS-1042", CancellationToken.None);
        var list = await actions.ListRecentOrdersAsync(new CallerIdentity(" "), CancellationToken.None);

        Assert.Equal(ActionOutcome.NotAuthorized, status.Outcome);
        Assert.Equal(ActionOutcome.NotAuthorized, list.Outcome);
        Assert.Equal(0, backend.Calls);
    }

    [Fact]
    public async Task ListRecentOrders_returns_only_the_callers_orders_newest_first_and_capped()
    {
        var (actions, _, _) = Create();

        var result = await actions.ListRecentOrdersAsync(TestData.Alice, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["NS-1044", "NS-1042", "NS-1043", "NS-1001"], result.Value!.Select(o => o.OrderId));
    }

    [Fact]
    public async Task ListRecentOrders_filters_again_even_if_the_backend_leaks_other_customers()
    {
        var (actions, _, _) = Create(new LeakyBackend(TestData.Orders));

        var result = await actions.ListRecentOrdersAsync(TestData.Bob, CancellationToken.None);

        Assert.Equal(["NS-2001"], result.Value!.Select(o => o.OrderId));
    }

    [Fact]
    public async Task ListRecentOrders_is_capped_at_the_limit()
    {
        var many = Enumerable.Range(1, 12)
            .Select(i => new Order($"NS-{1000 + i}", OrderFixtures.AliceCustomerId, OrderStatus.Shipped, TestData.Now.AddDays(-i), null, null, null, []))
            .ToList();
        var (actions, _, _) = Create(new InMemoryOrderBackend(many));

        var result = await actions.ListRecentOrdersAsync(TestData.Alice, CancellationToken.None);

        Assert.Equal(OrderActions.RecentOrderLimit, result.Value!.Count);
    }

    [Fact]
    public async Task RequestReturnInfo_is_available_inside_the_return_window()
    {
        var (actions, _, _) = Create();

        var result = await actions.RequestReturnInfoAsync(TestData.Alice, "NS-1043", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TestData.Now.AddDays(-8) + OrderActions.ReturnWindow, result.Value!.ReturnByDate);
    }

    [Fact]
    public async Task RequestReturnInfo_is_refused_after_the_window_closes()
    {
        var (actions, _, clock) = Create();
        clock.Advance(TimeSpan.FromDays(60));

        var result = await actions.RequestReturnInfoAsync(TestData.Alice, "NS-1043", CancellationToken.None);

        Assert.Equal(ActionOutcome.NotEligible, result.Outcome);
    }

    [Theory]
    [InlineData("NS-1042")] // shipped, not delivered yet
    [InlineData("NS-1044")] // still processing
    [InlineData("NS-1001")] // delivered 85 days ago
    public async Task RequestReturnInfo_is_refused_when_not_eligible(string orderId)
    {
        var (actions, _, _) = Create();

        var result = await actions.RequestReturnInfoAsync(TestData.Alice, orderId, CancellationToken.None);

        Assert.Equal(ActionOutcome.NotEligible, result.Outcome);
    }

    [Fact]
    public void Backend_port_exposes_no_write_methods()
    {
        var names = typeof(IOrderBackend).GetMethods().Select(m => m.Name).ToArray();

        Assert.All(names, n => Assert.True(n.StartsWith("Find", StringComparison.Ordinal) || n.StartsWith("List", StringComparison.Ordinal), n));
    }
}
