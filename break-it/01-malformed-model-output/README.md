# 01 - Malformed model output

**Failure:** the model returns an intent kind the host has never heard of (`99`), and, separately, asks for an order lookup without an order id.

**Expected behaviour:** no tool runs and the order backend is never called. The customer gets the "I can help with order status..." reply (`NotUnderstood`) or a request for the order number (`NeedOrderId`).

**Where it is handled:** `SupportResponder.DispatchAsync` (the `default` branch and the `NeedOrderId` check). The model returns an `Intent` that is treated as an untrusted suggestion, never as a command.

**Break the fix:** make the `default` branch map an unknown kind to `OrderStatus`; the runner fails because the route changes or the backend is called.

```powershell
dotnet run --project break-it -- 01-malformed-model-output
```
