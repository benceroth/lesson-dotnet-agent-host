# 02 - Out-of-range argument

**Failure:** the model asks for `NS-99999999` (too many digits) and for `NS-1042' OR '1'='1` (an injected string).

**Expected behaviour:** both come back as `InvalidInput` ("Order ids look like NS-1042."). The order backend is never called, so a bad argument cannot reach a query.

**Where it is handled:** `OrderIdFormat.TryNormalize` (only `NS-` plus 4 to 6 digits passes), called by `OrderActions` before any backend call.

**Break the fix:** loosen the pattern in `OrderIdFormat` to `.*`; the runner fails because the backend is now called with the bad id.

```powershell
dotnet run --project break-it -- 02-out-of-range-argument
```
