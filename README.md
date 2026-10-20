# lesson-dotnet-agent-host

Stage 1 of the companion code for *Build Your First .NET Agent Without Hiding the Hard Parts*: an ASP.NET Core host with the Microsoft 365 Agents SDK, a welcome handler, an echo handler and one `/api/messages` endpoint.

```powershell
dotnet run --project src/Northstar.Agent.Host     # http://localhost:3978
./scripts/smoke.ps1                               # in another terminal
```

The echo proves hosting and routing. It is not an assistant. Later stages add the application service, the fake model, narrow actions and state; the full README is on `main`.

<sub>[Bence Roth](https://broth.hu/)</sub>
