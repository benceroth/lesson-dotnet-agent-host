using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Northstar.Agent.Core;

namespace Northstar.Agent.Host;

/// <summary>
/// Channel edge only: routes activities, resolves who is calling, and hands plain text to
/// <see cref="ISupportResponder"/>. No model, order or storage code lives here.
/// </summary>
public sealed class NorthstarAgent : AgentApplication
{
    public const string UnverifiedCallerMessage = "I can't verify your account on this channel yet, so I can't look up orders.";

    private readonly ISupportResponder _responder;
    private readonly ICallerResolver _callers;

    public NorthstarAgent(AgentApplicationOptions options, ISupportResponder responder, ICallerResolver callers)
        : base(options)
    {
        _responder = responder;
        _callers = callers;
        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeAsync);
        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);
    }

    private async Task WelcomeAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        foreach (var member in turnContext.Activity.MembersAdded ?? [])
        {
            // The conversation update also fires for the agent itself joining.
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text(_responder.Welcome()), cancellationToken);
            }
        }
    }

    private async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var activity = turnContext.Activity;
        var customerId = _callers.Resolve(activity.From?.Id);
        if (customerId is null)
        {
            await turnContext.SendActivityAsync(MessageFactory.Text(UnverifiedCallerMessage), cancellationToken);
            return;
        }

        var request = new ConversationRequest(activity.Conversation?.Id ?? "unknown", customerId, activity.Text);
        var reply = await _responder.RespondAsync(request, cancellationToken);
        await turnContext.SendActivityAsync(MessageFactory.Text(reply.Text), cancellationToken);
    }
}
