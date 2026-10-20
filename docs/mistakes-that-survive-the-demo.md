# Mistakes that survive the demo

Mirrors the list in the post, with pointers into this repo. "Partly" means the repo shows the guardrail but the full fix needs work outside a sample.

| Mistake | What the repo does | Where |
|---|---|---|
| Calling the echo sample an AI agent | README states the model is a rule-based fake; host tests are named as host/routing tests | `README.md`, `tests/Host.Tests/HostFlowTests.cs` |
| Keeping in-memory storage in production | Default is in-memory for local use only; a test shows the loss on restart; SQLite is the persistent option (single node only) | `docs/state-choices.md`, `tests/Core.Tests/StateStoreTests.cs` |
| Putting raw user text into an improvised prompt string | Partly. There is no prompt: the model interface returns a structured `Intent`, and the order id it yields is re-validated by the action. A real model integration still needs its own prompt-injection tests. | `src/Northstar.Agent.Model/IModelClient.cs`, `OrderIdFormat`, `OrderActionsTests` (injection-style ids), `FakeModelClientTests.Does_not_obey_instructions_embedded_in_user_text` |
| Giving tools broad credentials | The backend port is read-only and the action returns a projection without customer id or payment data. Real credential scoping is deployment work and is not demonstrated. | `IOrderBackend`, `OrderActionsTests.Backend_port_exposes_no_write_methods` |
| Assuming a local testing tool represents production | Anonymous mode only works behind an explicit flag; host refuses to start otherwise | `Program.cs`, `HostFlowTests.Host_refuses_to_start_without_explicit_anonymous_opt_in`, `docs/channel-setup.md` |
| Skipping channel-specific tests | **Not covered.** Only the shared activity path is tested; no channel was tested. | `docs/channel-setup.md` |
| Not defining failure behavior | Timeout, backend failure, model failure and state outage each have a defined reply and a test | `tests/Core.Tests/ErrorHandlingTests.cs` |
| Logging secrets or content | Logs carry route, duration, conversation id | `ErrorHandlingTests.Logs_record_the_decision_but_not_the_message_text` |
| Letting users read each other's data | Owner check; "not yours" and "doesn't exist" are indistinguishable to the customer | `OrderActionsTests`, `ConversationTests.Someone_elses_order_looks_exactly_like_an_unknown_order` |
