using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Northstar.Agent.Actions;
using Northstar.Agent.Host;

namespace Host.Tests;

/// <summary>
/// Real HTTP through the real SDK pipeline: an Activity is posted to /api/messages with
/// deliveryMode=expectReplies, and the reply activities come back in the response body.
/// </summary>
public sealed class HostFlowTests : IDisposable
{
    private readonly List<WebApplicationFactory<Program>> _factories = [];

    public void Dispose()
    {
        foreach (var factory in _factories)
        {
            factory.Dispose();
        }
    }

    private HttpClient CreateClient(
        string? defaultCustomer = "cust-alice",
        Action<IServiceCollection>? services = null,
        Dictionary<string, string?>? extraSettings = null)
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Host:AllowAnonymous", "true");
            // Empty overrides the Development default so the "unmapped user" path is real.
            b.UseSetting("Callers:DefaultCustomerId", defaultCustomer ?? string.Empty);
            foreach (var (key, value) in extraSettings ?? [])
            {
                b.UseSetting(key, value);
            }

            if (services is not null)
            {
                b.ConfigureTestServices(services);
            }
        });
        _factories.Add(factory);
        return factory.CreateClient();
    }

    private static object Activity(string type, string id, string? text = null, string userId = "user-1", string conversationId = "conv-1", object[]? membersAdded = null) => new
    {
        type,
        id,
        text,
        channelId = "webchat",
        serviceUrl = "http://localhost:9/",
        deliveryMode = "expectReplies",
        from = new { id = userId },
        recipient = new { id = "agent" },
        conversation = new { id = conversationId },
        membersAdded,
    };

    private static async Task<string[]> PostAsync(HttpClient client, object activity)
    {
        var response = await client.PostAsJsonAsync("/api/messages", activity);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("activities").EnumerateArray()
            .Select(a => a.GetProperty("text").GetString() ?? string.Empty)
            .ToArray();
    }

    [Fact]
    public async Task Health_endpoint_answers()
    {
        var response = await CreateClient().GetAsync(new Uri("/health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Message_activity_in_reply_activity_out()
    {
        var replies = await PostAsync(CreateClient(), Activity("message", "m1", "Where is order NS-1042?"));

        var reply = Assert.Single(replies);
        Assert.Equal("Order NS-1042 has shipped with FictionalExpress (tracking FX-000-1042).", reply);
    }

    [Fact]
    public async Task New_member_gets_a_welcome_and_the_agent_joining_does_not()
    {
        var client = CreateClient();

        var forUser = await PostAsync(client, Activity("conversationUpdate", "u1", membersAdded: [new { id = "user-1" }]));
        var forAgent = await PostAsync(client, Activity("conversationUpdate", "u2", membersAdded: [new { id = "agent" }]));

        Assert.Contains("Welcome to Northstar Outfitters support", Assert.Single(forUser), StringComparison.Ordinal);
        Assert.Empty(forAgent);
    }

    [Fact]
    public async Task Follow_up_works_across_two_posts_in_the_same_conversation()
    {
        var client = CreateClient();

        await PostAsync(client, Activity("message", "m1", "Where is NS-1043?"));
        var replies = await PostAsync(client, Activity("message", "m2", "Can I return it?"));

        Assert.Contains("NS-1043 can be returned until", Assert.Single(replies), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unmapped_channel_user_cannot_look_up_orders()
    {
        var client = CreateClient(defaultCustomer: null);

        var replies = await PostAsync(client, Activity("message", "m1", "Where is NS-1042?"));

        Assert.Equal(NorthstarAgent.UnverifiedCallerMessage, Assert.Single(replies));
    }

    [Fact]
    public async Task Mapped_channel_user_only_sees_their_own_orders()
    {
        var client = CreateClient(
            defaultCustomer: null,
            extraSettings: new() { ["Callers:Map:aad-bob"] = "cust-bob" });

        var own = await PostAsync(client, Activity("message", "m1", "Where is NS-2001?", userId: "aad-bob"));
        var foreign = await PostAsync(client, Activity("message", "m2", "Where is NS-1042?", userId: "aad-bob"));

        Assert.Contains("NS-2001 has shipped", Assert.Single(own), StringComparison.Ordinal);
        Assert.Contains("couldn't find that order", Assert.Single(foreign), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Order_backend_outage_becomes_a_safe_message_not_an_http_error()
    {
        var client = CreateClient(services: s =>
        {
            s.RemoveAll<IOrderBackend>();
            s.AddSingleton<IOrderBackend, ExplodingBackend>();
        });

        var replies = await PostAsync(client, Activity("message", "m1", "Where is NS-1042?"));

        var reply = Assert.Single(replies);
        Assert.Contains("something went wrong on our side", reply, StringComparison.Ordinal);
        Assert.DoesNotContain("connection string", reply, StringComparison.Ordinal);
    }

    [Fact]
    public void Host_refuses_to_start_without_explicit_anonymous_opt_in()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Production");
            b.UseSetting("Host:AllowAnonymous", "false");
        });

        var ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("no token validation", ex.ToString(), StringComparison.Ordinal);
    }

    private sealed class ExplodingBackend : IOrderBackend
    {
        public Task<Order?> FindOrderAsync(string orderId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("connection string leaked");

        public Task<IReadOnlyList<Order>> ListOrdersForCustomerAsync(string customerId, int maxCount, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("connection string leaked");
    }
}
