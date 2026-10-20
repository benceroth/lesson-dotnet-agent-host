using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Northstar.Agent.Actions;
using Northstar.Agent.Model;
using Northstar.Agent.State;

namespace Northstar.Agent.Core;

/// <summary>
/// One conversation flow for every channel: interpret (model) -> act (narrow actions) -> remember (state) -> reply.
/// Logs decisions (route, outcome, duration), never message content.
/// </summary>
public sealed class SupportResponder(
    IModelClient model,
    IOrderActions actions,
    IStateStore state,
    IOptions<SupportResponderOptions> options,
    ILogger<SupportResponder> logger) : ISupportResponder
{
    public const string TimeoutMessage = "Sorry, that is taking longer than expected. Please try again in a moment.";
    public const string FailureMessage = "Sorry, something went wrong on our side. Please try again shortly.";
    public const string NotFoundMessage = "I couldn't find that order on your account. Please check the order number (for example NS-1042).";

    private readonly SupportResponderOptions _options = options.Value;

    public string Welcome() =>
        "Welcome to Northstar Outfitters support! Ask me things like \"Where is order NS-1042?\", \"Show my recent orders\" or \"Can I return NS-1043?\".";

    public async Task<SupportReply> RespondAsync(ConversationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var stopwatch = Stopwatch.StartNew();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.TurnTimeout);

        SupportReply reply;
        try
        {
            reply = await HandleAsync(request, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            reply = new SupportReply(TimeoutMessage, ReplyRoute.Timeout);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Turn failed for conversation {ConversationId}", request.ConversationId);
            reply = new SupportReply(FailureMessage, ReplyRoute.Failure);
        }

        var conversationId = request.ConversationId;
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        var route = reply.Route;
        logger.LogInformation(
            "Turn handled: conversation={ConversationId} route={Route} durationMs={DurationMs}",
            conversationId, route, elapsedMs);
        return reply;
    }

    private async Task<SupportReply> HandleAsync(ConversationRequest request, CancellationToken ct)
    {
        var text = request.Text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return new SupportReply("Tell me what you need, for example \"Where is order NS-1042?\"", ReplyRoute.NotUnderstood);
        }

        if (text.Length > _options.MaxMessageLength)
        {
            return new SupportReply("That message is too long for me. Please keep it short, for example \"Where is order NS-1042?\"", ReplyRoute.TooLong);
        }

        var current = await ReadStateAsync(request.ConversationId, ct);
        var intent = await model.InterpretAsync(text, new ModelContext(current.LastOrderId), ct);
        var caller = new CallerIdentity(request.CustomerId);

        var (reply, rememberedOrderId) = await DispatchAsync(intent, caller, ct);

        var next = current with
        {
            TurnCount = current.TurnCount + 1,
            LastOrderId = rememberedOrderId ?? current.LastOrderId,
        };
        await WriteStateAsync(request.ConversationId, next, ct);
        return reply;
    }

    private async Task<(SupportReply Reply, string? RememberedOrderId)> DispatchAsync(Intent intent, CallerIdentity caller, CancellationToken ct)
    {
        switch (intent.Kind)
        {
            case IntentKind.Greeting:
                return (new SupportReply("Hello! " + Welcome(), ReplyRoute.Greeting), null);
            case IntentKind.Help:
                return (new SupportReply(Welcome(), ReplyRoute.Help), null);
            case IntentKind.ListOrders:
                return (await ListOrdersAsync(caller, ct), null);
            case IntentKind.OrderStatus:
            case IntentKind.ReturnInfo:
                if (string.IsNullOrWhiteSpace(intent.OrderId))
                {
                    return (new SupportReply("Which order is this about? Please give me the order number, like NS-1042.", ReplyRoute.NeedOrderId), null);
                }

                return intent.Kind == IntentKind.OrderStatus
                    ? await OrderStatusAsync(caller, intent.OrderId, ct)
                    : await ReturnInfoAsync(caller, intent.OrderId, ct);
            default:
                return (new SupportReply("I can help with order status, recent orders and return information. Try \"Where is order NS-1042?\"", ReplyRoute.NotUnderstood), null);
        }
    }

    private async Task<(SupportReply, string?)> OrderStatusAsync(CallerIdentity caller, string orderId, CancellationToken ct)
    {
        var result = await actions.GetOrderStatusAsync(caller, orderId, ct);
        if (!result.IsSuccess)
        {
            return (FailureReply(result.Outcome, result.Detail), null);
        }

        var v = result.Value!;
        var text = v.Status switch
        {
            OrderStatus.Shipped => $"Order {v.OrderId} has shipped with {v.Carrier} (tracking {v.TrackingNumber}).",
            OrderStatus.Delivered => $"Order {v.OrderId} was delivered (carrier {v.Carrier}).",
            OrderStatus.Processing => $"Order {v.OrderId} is being prepared and has not shipped yet.",
            _ => $"Order {v.OrderId} was cancelled.",
        };
        return (new SupportReply(text, ReplyRoute.OrderStatus), v.OrderId);
    }

    private async Task<SupportReply> ListOrdersAsync(CallerIdentity caller, CancellationToken ct)
    {
        var result = await actions.ListRecentOrdersAsync(caller, ct);
        if (!result.IsSuccess)
        {
            return FailureReply(result.Outcome, result.Detail);
        }

        if (result.Value!.Count == 0)
        {
            return new SupportReply("I don't see any recent orders on your account.", ReplyRoute.ListOrders);
        }

        var lines = result.Value.Select(o => string.Create(CultureInfo.InvariantCulture, $"- {o.OrderId}: {o.Status}, placed {o.PlacedAt:yyyy-MM-dd}"));
        return new SupportReply("Your recent orders:\n" + string.Join('\n', lines), ReplyRoute.ListOrders);
    }

    private async Task<(SupportReply, string?)> ReturnInfoAsync(CallerIdentity caller, string orderId, CancellationToken ct)
    {
        var result = await actions.RequestReturnInfoAsync(caller, orderId, ct);
        if (!result.IsSuccess)
        {
            return (FailureReply(result.Outcome, result.Detail), null);
        }

        var v = result.Value!;
        var text = string.Create(CultureInfo.InvariantCulture, $"Order {v.OrderId} can be returned until {v.ReturnByDate:yyyy-MM-dd}. {v.Instructions}");
        return (new SupportReply(text, ReplyRoute.ReturnInfo), v.OrderId);
    }

    // NotAuthorized and NotFound must be indistinguishable to the customer: no order enumeration.
    private static SupportReply FailureReply(ActionOutcome outcome, string? detail) => outcome switch
    {
        ActionOutcome.InvalidInput => new SupportReply(detail ?? "That doesn't look like a valid order number.", ReplyRoute.InvalidInput),
        ActionOutcome.NotEligible => new SupportReply(detail ?? "That order isn't eligible.", ReplyRoute.NotEligible),
        _ => new SupportReply(NotFoundMessage, ReplyRoute.NotFound),
    };

    // State is a convenience, not a dependency: if it is down, answer the question without follow-up memory.
    private async Task<ConversationState> ReadStateAsync(string conversationId, CancellationToken ct)
    {
        try
        {
            return await state.GetAsync(conversationId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "State read failed for conversation {ConversationId}; continuing without it", conversationId);
            return ConversationState.Empty;
        }
    }

    private async Task WriteStateAsync(string conversationId, ConversationState next, CancellationToken ct)
    {
        try
        {
            await state.SaveAsync(conversationId, next, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "State write failed for conversation {ConversationId}; reply is still sent", conversationId);
        }
    }
}
