namespace Northstar.Agent.Model;

/// <summary>
/// An offline model whose answers you choose. Use it to replay a transcript, to prove the host does not depend on one
/// provider, or to hand the host output a real model could produce on a bad day.
/// </summary>
public sealed class ScriptedModelClient : IModelClient
{
    private readonly Func<string, ModelContext, Intent> _answer;

    public ScriptedModelClient(Func<string, ModelContext, Intent> answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        _answer = answer;
    }

    /// <summary>Answers with each intent in order, then <see cref="IntentKind.Unknown"/>.</summary>
    public ScriptedModelClient(params Intent[] script)
    {
        ArgumentNullException.ThrowIfNull(script);
        var queue = new Queue<Intent>(script);
        _answer = (_, _) => queue.Count > 0 ? queue.Dequeue() : new Intent(IntentKind.Unknown);
    }

    public Task<Intent> InterpretAsync(string userText, ModelContext context, CancellationToken cancellationToken) =>
        Task.FromResult(_answer(userText, context));
}
