using Microsoft.Agents.Builder;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Northstar.Agent.Host;

var builder = WebApplication.CreateBuilder(args);

// Fail closed: this sample ships no token validation, so it only starts when anonymous access is explicitly allowed
// (appsettings.Development.json does that for local testing). See docs/channel-setup.md.
if (!builder.Configuration.GetValue<bool>("Host:AllowAnonymous"))
{
    throw new InvalidOperationException(
        "Host:AllowAnonymous is false and this sample has no token validation wired up. " +
        "Run in Development for local testing, or add Entra ID token validation first (docs/channel-setup.md).");
}

builder.Services.AddHttpClient();
builder.AddAgentApplicationOptions();
builder.AddAgent<NorthstarAgent>();
// SDK turn state (not our conversation state; see docs/state-choices.md).
builder.Services.AddSingleton<IStorage, MemoryStorage>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapPost("/api/messages", async (HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
{
    await adapter.ProcessAsync(request, response, agent, cancellationToken);
});

app.Run();

public partial class Program;
