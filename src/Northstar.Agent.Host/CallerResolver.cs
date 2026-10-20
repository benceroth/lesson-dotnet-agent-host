using Microsoft.Extensions.Options;

namespace Northstar.Agent.Host;

/// <summary>Maps the authenticated channel user to a Northstar customer id. Never reads message text.</summary>
public interface ICallerResolver
{
    string? Resolve(string? channelUserId);
}

public sealed class CallerOptions
{
    public const string SectionName = "Callers";

    /// <summary>Channel user id -> customer id.</summary>
    public Dictionary<string, string> Map { get; init; } = [];

    /// <summary>
    /// Demo shortcut for local testing, where the Playground user id is random. Leave empty anywhere real customers can reach the agent.
    /// </summary>
    public string? DefaultCustomerId { get; set; }
}

public sealed class ConfiguredCallerResolver(IOptions<CallerOptions> options) : ICallerResolver
{
    private readonly CallerOptions _options = options.Value;

    public string? Resolve(string? channelUserId)
    {
        if (!string.IsNullOrEmpty(channelUserId) && _options.Map.TryGetValue(channelUserId, out var customerId))
        {
            return customerId;
        }

        return string.IsNullOrWhiteSpace(_options.DefaultCustomerId) ? null : _options.DefaultCustomerId;
    }
}
