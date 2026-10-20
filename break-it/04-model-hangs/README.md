# 04 - The model hangs

**Failure:** the model never answers (the runner uses a 300 ms turn timeout to keep it quick).

**Expected behaviour:** the turn is cut off and the customer gets the fixed timeout message (`Timeout`), not a hung request or an HTTP error.

**Where it is handled:** `SupportResponder.RespondAsync` links a `TurnTimeout` (`Support:TurnTimeout`, 10 seconds by default) to the cancellation token passed to the model and the tools.

**Break the fix:** remove the `timeout.CancelAfter(...)` line; the runner never returns.

```powershell
dotnet run --project break-it -- 04-model-hangs
```
