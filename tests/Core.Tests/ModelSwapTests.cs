using System.Text.RegularExpressions;
using Northstar.Agent.Core;
using Northstar.Agent.Model;

namespace Core.Tests;

/// <summary>
/// The host tests must not care which model sits behind <see cref="IModelClient"/>. The same conversation
/// is run against two independent implementations and must produce the same routes and replies.
/// </summary>
public class ModelSwapTests
{
    private static readonly Regex OrderId = new(@"NS-\d+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    public static TheoryData<string> Providers => new() { "rule-based", "scripted" };

    private static IModelClient Create(string provider) => provider switch
    {
        "rule-based" => new FakeModelClient(),
        _ => new ScriptedModelClient((text, context) =>
        {
            var match = OrderId.Match(text);
            var id = match.Success ? match.Value : context.LastOrderId;
            return text.Contains("return", StringComparison.OrdinalIgnoreCase)
                ? new Intent(IntentKind.ReturnInfo, id)
                : new Intent(IntentKind.OrderStatus, id);
        }),
    };

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Order_status_and_follow_up_work_with_any_provider(string provider)
    {
        var chat = new Chat(model: Create(provider));

        var status = await chat.SayAsync("Where is NS-1043?");
        var followUp = await chat.SayAsync("Can I return it?");

        Assert.Equal(ReplyRoute.OrderStatus, status.Route);
        Assert.Equal("Order NS-1043 was delivered (carrier FictionalExpress).", status.Text);
        Assert.Equal(ReplyRoute.ReturnInfo, followUp.Route);
        Assert.Contains("NS-1043 can be returned until", followUp.Text, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Ownership_is_enforced_whatever_the_provider_asks_for(string provider)
    {
        var reply = await new Chat(model: Create(provider)).SayAsync("Where is NS-2001?");

        Assert.Equal(ReplyRoute.NotFound, reply.Route);
        Assert.Equal(SupportResponder.NotFoundMessage, reply.Text);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task Invalid_order_ids_are_rejected_whatever_the_provider_asks_for(string provider)
    {
        var reply = await new Chat(model: Create(provider)).SayAsync("Where is NS-99999999?");

        Assert.Equal(ReplyRoute.InvalidInput, reply.Route);
    }
}
