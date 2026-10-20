using Northstar.Agent.Core;
using Northstar.Agent.State;

namespace Core.Tests;

/// <summary>Failure behavior is a product decision: the customer gets a safe message, the log gets the detail.</summary>
public class ErrorHandlingTests
{
    [Fact]
    public async Task Backend_failure_produces_a_safe_message_without_leaking_details()
    {
        var chat = new Chat(backend: new ThrowingBackend(new InvalidOperationException("Server=db01;Password=hunter2")));

        var reply = await chat.SayAsync("Where is NS-1042?");

        Assert.Equal(ReplyRoute.Failure, reply.Route);
        Assert.Equal(SupportResponder.FailureMessage, reply.Text);
        Assert.DoesNotContain("hunter2", reply.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Model_failure_produces_a_safe_message()
    {
        var reply = await new Chat(model: new ThrowingModel()).SayAsync("Where is NS-1042?");

        Assert.Equal(ReplyRoute.Failure, reply.Route);
        Assert.DoesNotContain("secret-detail", reply.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Hung_backend_times_out_with_a_retry_message()
    {
        var chat = new Chat(backend: new HangingBackend(), timeout: TimeSpan.FromMilliseconds(100));

        var reply = await chat.SayAsync("Where is NS-1042?");

        Assert.Equal(ReplyRoute.Timeout, reply.Route);
        Assert.Equal(SupportResponder.TimeoutMessage, reply.Text);
    }

    [Fact]
    public async Task Caller_cancellation_is_not_disguised_as_a_timeout()
    {
        var chat = new Chat(backend: new HangingBackend());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => chat.SayAsync("Where is NS-1042?", cts.Token));
    }

    [Fact]
    public async Task State_outage_does_not_stop_the_answer()
    {
        var reply = await new Chat(state: new ThrowingStateStore()).SayAsync("Where is NS-1042?");

        Assert.Equal(ReplyRoute.OrderStatus, reply.Route);
    }

    [Fact]
    public async Task State_outage_costs_follow_up_memory_and_the_customer_is_asked_again()
    {
        var chat = new Chat(state: new ThrowingStateStore());

        await chat.SayAsync("Where is NS-1043?");
        var reply = await chat.SayAsync("Can I return it?");

        Assert.Equal(ReplyRoute.NeedOrderId, reply.Route);
    }

    [Fact]
    public async Task Logs_record_the_decision_but_not_the_message_text()
    {
        var chat = new Chat();

        await chat.SayAsync("Where is NS-1042? my phone is 555-0100");

        var logged = string.Join('\n', chat.Log.Messages);
        Assert.Contains("route=OrderStatus", logged, StringComparison.Ordinal);
        Assert.DoesNotContain("555-0100", logged, StringComparison.Ordinal);
        Assert.DoesNotContain("NS-1042", logged, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Failure_logs_keep_the_exception_for_operators()
    {
        var chat = new Chat(backend: new ThrowingBackend(new InvalidOperationException("boom-for-operators")));

        await chat.SayAsync("Where is NS-1042?");

        Assert.Contains("boom-for-operators", string.Join('\n', chat.Log.Messages), StringComparison.Ordinal);
    }
}
