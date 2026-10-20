# Channel setup

What each channel needs. **Status: none of the real channels (Teams, Microsoft 365 Copilot, Azure Bot web chat) was tested for this repo.** What was exercised: HTTP activities posted directly to `/api/messages` (automated tests and `scripts/smoke.ps1`), in anonymous mode. The `Playground` instructions follow the official quickstart and were not run for this write-up.

Treat everything below as a checklist to verify, not as a verified recipe. Always follow the current [Agents SDK documentation](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/).

## Local (anonymous)

- `dotnet run --project src/Northstar.Agent.Host` listens on `http://localhost:3978`; the endpoint is `POST /api/messages`.
- `appsettings.Development.json` sets `Host:AllowAnonymous=true` and maps unknown users to `cust-alice`.
- The SDK logs a warning about "No connections map found" in this mode. That is expected without configuration.

## What a deployed agent needs (unverified here)

- A public HTTPS endpoint that Azure Bot Service can reach, routed to `/api/messages`.
- An Azure Bot / Entra app registration, giving a client id, a tenant id, and a credential.
- SDK configuration in the shape the SDK's own samples use (see `appsettings.example.json`): `TokenValidation` (audiences, tenant), `Connections` (a service connection with authority, client id, secret or certificate/managed identity) and `ConnectionsMap`. Secrets via user secrets, environment variables or Key Vault.
- **Token validation in this host.** Not implemented. The SDK exposes `AddAgentDefaults`, `AddAgentAuthorization`, `UseAgents` and `MapDefaultAgentEndpoints` in the 1.9.205 package, which looks like the intended route; it is not wired here and was not tried. Until it is, the host throws at startup unless `Host:AllowAnonymous` is true.

## Per channel

| Channel | What to expect | Verified here? |
|---|---|---|
| Web chat (Azure Bot) | Add the Web Chat channel to the Azure Bot resource pointing to your endpoint | No |
| Microsoft Teams | A Teams app manifest referencing the bot id; Teams user ids differ from web chat ids, so update `Callers:Map` | No |
| Microsoft 365 Copilot | Published as a custom engine agent; check current availability, licensing and admin steps in Microsoft docs | No |

## Why per-channel tests still matter

The shared host does not make channels identical: cards, authentication, message length limits and interaction patterns vary. The activity tests here prove routing, not channel behavior.

## Identity

`ICallerResolver` maps the channel user id (`activity.From.Id`) to a customer id using `Callers:Map`. For real deployments replace it with proper account linking (for example, Entra user to customer record), and leave `Callers:DefaultCustomerId` empty.
