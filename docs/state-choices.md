# State choices

What the agent remembers (`ConversationState`): the last order id the customer legitimately asked about, and a turn count. **Not** order status, not message text. Current business facts always come from the order backend.

| Store | Survives restart | Shared across instances | Good for | Status here |
|---|---|---|---|---|
| `InMemoryStateStore` | No | No | Local development, tests | Implemented, default |
| `SqliteStateStore` | Yes | No (a file on one node) | Single-instance demos, small internal tools | Implemented, tested |
| Cosmos DB / Blob Storage (`Microsoft.Agents.Storage.CosmosDb`, `.Blobs`) | Yes | Yes | Multi-instance deployments | **Documented only**: those packages exist on NuGet at 1.9.205, but are not used or tested here |

## What is lost on restart

- In-memory: all conversation memory. After a restart "Can I return it?" gets "Which order is this about?" (`StateStoreTests.InMemory_conversation_is_lost_when_the_process_restarts`).
- SQLite: nothing; the same follow-up still works (`Sqlite_conversation_survives_a_restart`, also through the host in `HostFlowTests.Sqlite_state_provider_can_be_selected_by_configuration`).

## Two kinds of state

The SDK has its own storage abstraction (`IStorage`) for turn/conversation/user state used by `AgentApplication`. This sample registers `MemoryStorage` for it because the SDK needs one, but keeps its own facts in `IStateStore`. If you start using SDK turn state for real, move it to a persistent `IStorage` as well.

## Decide before deploying

- What must persist between turns, and for how long (retention).
- Privacy and data governance of whatever is stored. Here: an order id and a counter.
- Behavior when the store is down: this sample answers anyway, without follow-up memory (`ErrorHandlingTests.State_outage_*`). Your policy may differ.
