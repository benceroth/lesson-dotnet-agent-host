# Next iteration

Mirrors the post's list. Items marked **done** exist in this repo; the rest are suggestions, not implemented.

- **Application service before the first model call: done.** `ISupportResponder` sits between the handler and everything else.
- **One read-only tool first: done** (`GetOrderStatus`, plus two more read-only actions). Observing what a *real* model asks for needs a real model: **not done**.
- **Authoritative facts outside conversation history: done.** `ConversationState` holds an id and a counter only.
- **Log decisions, not secrets: partly done.** Route, duration and conversation id are logged; tool name, result category and a proper correlation id are not.
- **Failure behavior: done** for timeout, backend, model and state errors. Channel-level errors (for example the SDK failing to send) are not specially handled.
- **Review package updates deliberately:** pin versions (they are pinned in the csproj files), read release notes, rerun channel tests when upgrading.

## Suggested next steps

1. Implement `IModelClient` over Azure OpenAI or Semantic Kernel that returns the same `Intent`, with its own tests for prompt injection and malformed output. Keep the fake as the default so `dotnet test` stays offline.
2. Wire token validation and try the SDK's `AddAgentAuthorization` / `MapDefaultAgentEndpoints` pattern, then remove the fail-closed flag from the default path.
3. Add a persistent shared store (Cosmos DB or Blob) for multi-instance hosting.
4. Test a real channel end to end (Teams first) and record what differs.
5. Add a harness around the loop (evaluation sets, regression runs): see the AtlasDesk harness companion repo ([`lesson-agent-harness`](https://github.com/benceroth/lesson-agent-harness)).
