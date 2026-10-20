# 03 - State lost on restart

**Failure:** a customer asks about NS-1043, the process restarts, then they ask "Can I return it?".

**Expected behaviour:** with the default in-memory store the follow-up has nothing to refer to, so the agent asks which order (`NeedOrderId`). That is the failure, shown on purpose. With the SQLite store the same two turns work (`ReturnInfo`).

**Fix:** set `State:Provider` to `Sqlite`, or use a shared store such as Cosmos DB or Blob storage once you run more than one instance. See `docs/state-choices.md`.

**Break the fix:** swap `SqliteStateStore` for `InMemoryStateStore` in the runner; it fails.

```powershell
dotnet run --project break-it -- 03-state-lost-on-restart
```
