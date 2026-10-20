# lesson-dotnet-agent-host

Stage 2 of the companion code for *Build Your First .NET Agent Without Hiding the Hard Parts*: the echo handler now hands plain text to `ISupportResponder`, which runs one flow (fake model, narrow owner-checked order actions, conversation state) and is tested without a model, a network or a secret.

```powershell
dotnet test
dotnet run --project src/Northstar.Agent.Host     # http://localhost:3978
./scripts/smoke.ps1                               # in another terminal
```

State here is in-memory only. Later stages add persistent state; the full README is on `main`.

<sub>[Bence Roth](https://broth.hu/)</sub>
