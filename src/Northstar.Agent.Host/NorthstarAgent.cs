using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;

namespace Northstar.Agent.Host;

/// <summary>
/// Stage 1: the channel edge only. Welcome new members and echo messages. The echo is a test seam,
/// not the finished agent: it proves the hosting and routing path and nothing else.
/// </summary>
public sealed class NorthstarAgent : AgentApplication
{
    public NorthstarAgent(AgentApplicationOptions options)
        : base(options)
    {
        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeAsync);
        OnActivity(ActivityTypes.Message, EchoAsync, rank: RouteRank.Last);
    }

    private static async Task WelcomeAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        foreach (var member in turnContext.Activity.MembersAdded ?? [])
        {
            // The conversation update also fires for the agent itself joining.
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text("Welcome to Northstar Outfitters support!"), cancellationToken);
            }
        }
    }

    private static async Task EchoAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        await turnContext.SendActivityAsync(MessageFactory.Text("You said: " + turnContext.Activity.Text), cancellationToken);
    }
}
