using Northstar.Agent.Core;

namespace Core.Tests;

/// <summary>Scripted conversations: user text in, expected route and text out. No model, network or secrets.</summary>
public class ConversationTests
{
    [Fact]
    public async Task Order_status_question_is_answered_from_the_backend()
    {
        var reply = await new Chat().SayAsync("Where is order NS-1042?");

        Assert.Equal(ReplyRoute.OrderStatus, reply.Route);
        Assert.Equal("Order NS-1042 has shipped with FictionalExpress (tracking FX-000-1042).", reply.Text);
    }

    [Fact]
    public async Task Follow_up_resolves_to_the_order_discussed_before()
    {
        var chat = new Chat();

        await chat.SayAsync("Where is order NS-1043?");
        var reply = await chat.SayAsync("Can I return it?");

        Assert.Equal(ReplyRoute.ReturnInfo, reply.Route);
        Assert.Contains("NS-1043 can be returned until 2026-11-11", reply.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Follow_up_without_any_earlier_order_asks_which_order()
    {
        var reply = await new Chat().SayAsync("Can I return it?");

        Assert.Equal(ReplyRoute.NeedOrderId, reply.Route);
    }

    [Fact]
    public async Task Recent_orders_lists_only_the_customers_own_orders()
    {
        var reply = await new Chat().SayAsync("Show my recent orders");

        Assert.Equal(ReplyRoute.ListOrders, reply.Route);
        Assert.Contains("NS-1042", reply.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("NS-2001", reply.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Someone_elses_order_looks_exactly_like_an_unknown_order()
    {
        var chat = new Chat();

        var foreign = await chat.SayAsync("Where is NS-2001?"); // Bob's order
        var missing = await chat.SayAsync("Where is NS-9999?"); // does not exist

        Assert.Equal(ReplyRoute.NotFound, foreign.Route);
        Assert.Equal(missing.Text, foreign.Text);
        Assert.DoesNotContain("FictionalExpress", foreign.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_refused_order_is_not_remembered_for_follow_ups()
    {
        var chat = new Chat();

        await chat.SayAsync("Where is NS-2001?");
        var reply = await chat.SayAsync("Can I return it?");

        Assert.Equal(ReplyRoute.NeedOrderId, reply.Route);
    }

    [Fact]
    public async Task Conversations_do_not_share_memory()
    {
        var shared = new Northstar.Agent.State.InMemoryStateStore();
        var first = new Chat(state: shared) { ConversationId = "a" };
        var second = new Chat(state: shared) { ConversationId = "b" };

        await first.SayAsync("Where is NS-1043?");
        var reply = await second.SayAsync("Can I return it?");

        Assert.Equal(ReplyRoute.NeedOrderId, reply.Route);
    }

    [Fact]
    public async Task Return_window_rule_is_explained_not_hidden()
    {
        var reply = await new Chat().SayAsync("I want to return NS-1001");

        Assert.Equal(ReplyRoute.NotEligible, reply.Route);
        Assert.Contains("return window", reply.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Malformed_order_ids_are_rejected_by_the_action_not_the_model()
    {
        // "NS-12" is picked up by the model but fails the action's own validation.
        var reply = await new Chat().SayAsync("Where is NS-12?");

        Assert.Equal(ReplyRoute.InvalidInput, reply.Route);
    }

    [Theory]
    [InlineData("hello", ReplyRoute.Greeting)]
    [InlineData("help", ReplyRoute.Help)]
    [InlineData("tell me a joke", ReplyRoute.NotUnderstood)]
    [InlineData("   ", ReplyRoute.NotUnderstood)]
    [InlineData(null, ReplyRoute.NotUnderstood)]
    public async Task Small_talk_and_empty_input_get_a_helpful_reply(string? text, ReplyRoute route)
    {
        var reply = await new Chat().SayAsync(text!);

        Assert.Equal(route, reply.Route);
        Assert.False(string.IsNullOrWhiteSpace(reply.Text));
    }

    [Fact]
    public async Task Overlong_messages_are_rejected_without_calling_anything()
    {
        var reply = await new Chat(backend: new ThrowingBackend(new InvalidOperationException("must not be called")))
            .SayAsync("Where is NS-1042? " + new string('x', 5000));

        Assert.Equal(ReplyRoute.TooLong, reply.Route);
    }
}
