using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Northstar.Agent.Actions;
using Northstar.Agent.Core;
using Northstar.Agent.Model;
using Northstar.Agent.Orders.Fake;
using Northstar.Agent.State;

namespace Northstar.Agent.Host;

public static class NorthstarServiceCollectionExtensions
{
    /// <summary>Everything except the SDK and channel wiring, so tests can compose the same graph.</summary>
    public static IServiceCollection AddNorthstarAgentServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SupportResponderOptions>(configuration.GetSection(SupportResponderOptions.SectionName));
        services.Configure<CallerOptions>(configuration.GetSection(CallerOptions.SectionName));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IOrderBackend>(sp => new InMemoryOrderBackend(OrderFixtures.Create(sp.GetRequiredService<TimeProvider>().GetUtcNow())));
        services.AddSingleton<IOrderActions, OrderActions>();
        services.AddSingleton<IModelClient, FakeModelClient>();
        services.AddSingleton<ICallerResolver, ConfiguredCallerResolver>();
        services.AddSingleton<ISupportResponder, SupportResponder>();

        var provider = configuration["State:Provider"] ?? "InMemory";
        switch (provider.ToUpperInvariant())
        {
            case "INMEMORY":
                services.AddSingleton<IStateStore, InMemoryStateStore>();
                break;
            case "SQLITE":
                var path = configuration["State:SqlitePath"] ?? "northstar-state.db";
                services.AddSingleton<IStateStore>(_ => new SqliteStateStore(path));
                break;
            default:
                throw new InvalidOperationException($"Unknown State:Provider '{provider}'. Use InMemory or Sqlite.");
        }

        return services;
    }
}
