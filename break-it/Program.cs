using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Northstar.Agent.Actions;
using Northstar.Agent.Core;
using Northstar.Agent.Model;
using Northstar.Agent.Orders.Fake;
using Northstar.Agent.State;

// Deliberate failures, one per folder. Usage: dotnet run --project break-it [-- <folder-name>|all]
// Exit code is 0 only if every selected failure is handled the way its README says.
var scenarios = new (string Name, Func<Task<bool>> Run)[]
{
    ("01-malformed-model-output", MalformedModelOutputAsync),
    ("02-out-of-range-argument", OutOfRangeArgumentAsync),
    ("03-state-lost-on-restart", StateLostOnRestartAsync),
    ("04-model-hangs", ModelHangsAsync),
};

var wanted = args.Length > 0 && args[0] != "all" ? args[0] : null;
var selected = scenarios.Where(s => wanted is null || s.Name == wanted).ToList();
if (selected.Count == 0)
{
    Console.WriteLine("Unknown scenario. Choose one of: all, " + string.Join(", ", scenarios.Select(s => s.Name)));
    return 2;
}

var failed = 0;
foreach (var (name, run) in selected)
{
    Console.WriteLine("== " + name);
    var ok = await run();
    Console.WriteLine(ok ? "PASS: handled as documented" : "FAIL: not handled as documented");
    Console.WriteLine();
    failed += ok ? 0 : 1;
}

return failed == 0 ? 0 : 1;

static Chat NewChat(IModelClient model, IOrderBackend? backend = null, IStateStore? state = null, TimeSpan? timeout = null)
{
    var clock = new FixedClock(new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.Zero));
    var counting = new CountingBackend(backend ?? new InMemoryOrderBackend(OrderFixtures.Create(clock.GetUtcNow())));
    var responder = new SupportResponder(
        model,
        new OrderActions(counting, clock),
        state ?? new InMemoryStateStore(),
        Options.Create(new SupportResponderOptions { TurnTimeout = timeout ?? TimeSpan.FromSeconds(5) }),
        NullLogger<SupportResponder>.Instance);
    return new Chat(responder, counting);
}

static async Task<bool> MalformedModelOutputAsync()
{
    var chat = NewChat(new ScriptedModelClient(
        new Intent((IntentKind)99, "NS-1042"),
        new Intent(IntentKind.OrderStatus, null)));

    var unknownKind = await chat.SayAsync("anything");
    var missingId = await chat.SayAsync("anything");

    Console.WriteLine($"model said kind=99      -> route {unknownKind.Route}, backend calls so far: {chat.Backend.Calls}");
    Console.WriteLine($"model said no order id  -> route {missingId.Route}, backend calls so far: {chat.Backend.Calls}");
    return unknownKind.Route == ReplyRoute.NotUnderstood && missingId.Route == ReplyRoute.NeedOrderId && chat.Backend.Calls == 0;
}

static async Task<bool> OutOfRangeArgumentAsync()
{
    var chat = NewChat(new ScriptedModelClient((_, _) => new Intent(IntentKind.OrderStatus, "NS-99999999")));
    var tooLong = await chat.SayAsync("anything");

    chat = NewChat(new ScriptedModelClient((_, _) => new Intent(IntentKind.OrderStatus, "NS-1042' OR '1'='1")));
    var injected = await chat.SayAsync("anything");

    Console.WriteLine($"orderId=NS-99999999     -> route {tooLong.Route}");
    Console.WriteLine($"orderId=NS-1042' OR ... -> route {injected.Route}, backend calls: {chat.Backend.Calls}");
    return tooLong.Route == ReplyRoute.InvalidInput && injected.Route == ReplyRoute.InvalidInput && chat.Backend.Calls == 0;
}

static async Task<bool> StateLostOnRestartAsync()
{
    var model = new FakeModelClient();
    var dbPath = Path.Combine(AppContext.BaseDirectory, $"break-it-{Guid.NewGuid():N}.db");
    try
    {
        await NewChat(model, state: new InMemoryStateStore()).SayAsync("Where is NS-1043?");
        var lost = await NewChat(model, state: new InMemoryStateStore()).SayAsync("Can I return it?");

        await NewChat(model, state: new SqliteStateStore(dbPath)).SayAsync("Where is NS-1043?");
        var kept = await NewChat(model, state: new SqliteStateStore(dbPath)).SayAsync("Can I return it?");

        Console.WriteLine($"in-memory after restart -> route {lost.Route} (the follow-up has nothing to refer to)");
        Console.WriteLine($"sqlite after restart    -> route {kept.Route}");
        return lost.Route == ReplyRoute.NeedOrderId && kept.Route == ReplyRoute.ReturnInfo;
    }
    finally
    {
        foreach (var file in Directory.GetFiles(AppContext.BaseDirectory, Path.GetFileName(dbPath) + "*"))
        {
            File.Delete(file);
        }
    }
}

static async Task<bool> ModelHangsAsync()
{
    var chat = NewChat(new HangingModel(), timeout: TimeSpan.FromMilliseconds(300));
    var started = DateTimeOffset.UtcNow;
    var reply = await chat.SayAsync("Where is NS-1042?");
    var elapsed = DateTimeOffset.UtcNow - started;

    Console.WriteLine($"model never answers     -> route {reply.Route} after {elapsed.TotalMilliseconds:F0} ms");
    return reply.Route == ReplyRoute.Timeout && reply.Text == SupportResponder.TimeoutMessage;
}

internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal sealed class Chat(SupportResponder responder, CountingBackend backend)
{
    public CountingBackend Backend { get; } = backend;

    public Task<SupportReply> SayAsync(string text) =>
        responder.RespondAsync(new ConversationRequest("break-it", OrderFixtures.AliceCustomerId, text), CancellationToken.None);
}

internal sealed class CountingBackend(IOrderBackend inner) : IOrderBackend
{
    public int Calls { get; private set; }

    public Task<Order?> FindOrderAsync(string orderId, CancellationToken cancellationToken)
    {
        Calls++;
        return inner.FindOrderAsync(orderId, cancellationToken);
    }

    public Task<IReadOnlyList<Order>> ListOrdersForCustomerAsync(string customerId, int maxCount, CancellationToken cancellationToken)
    {
        Calls++;
        return inner.ListOrdersForCustomerAsync(customerId, maxCount, cancellationToken);
    }
}

internal sealed class HangingModel : IModelClient
{
    public async Task<Intent> InterpretAsync(string userText, ModelContext context, CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return new Intent(IntentKind.Unknown);
    }
}
