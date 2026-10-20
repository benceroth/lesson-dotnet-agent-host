using Northstar.Agent.Core;
using Northstar.Agent.State;

namespace Core.Tests;

public class StateStoreTests
{
    [Fact]
    public async Task InMemory_roundtrips()
    {
        var store = new InMemoryStateStore();

        await store.SaveAsync("c1", new ConversationState("NS-1042", 3), CancellationToken.None);

        Assert.Equal(new ConversationState("NS-1042", 3), await store.GetAsync("c1", CancellationToken.None));
        Assert.Equal(ConversationState.Empty, await store.GetAsync("other", CancellationToken.None));
    }

    [Fact]
    public async Task InMemory_conversation_is_lost_when_the_process_restarts()
    {
        var beforeRestart = new Chat(state: new InMemoryStateStore());
        await beforeRestart.SayAsync("Where is NS-1043?");

        // A restart means a new process, which means a new empty store.
        var afterRestart = new Chat(state: new InMemoryStateStore());
        var reply = await afterRestart.SayAsync("Can I return it?");

        Assert.Equal(ReplyRoute.NeedOrderId, reply.Route);
    }

    [Fact]
    public async Task Stored_state_holds_no_order_status()
    {
        var store = new InMemoryStateStore();
        var chat = new Chat(state: store);

        await chat.SayAsync("Where is NS-1042?");

        var stored = await store.GetAsync("conv-1", CancellationToken.None);
        Assert.Equal(new ConversationState("NS-1042", 1), stored);
        Assert.Equal(["LastOrderId", "TurnCount"], typeof(ConversationState).GetProperties().Where(p => p.Name != "EqualityContract" && p.Name != "Empty").Select(p => p.Name).Order());
    }
}
