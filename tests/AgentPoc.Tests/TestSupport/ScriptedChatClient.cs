using Microsoft.Extensions.AI;

namespace AgentPoc.Tests.TestSupport;

/// <summary>
/// Replays a fixed script instead of calling a model. One agent run may call it several
/// times, once returning tool calls and once returning the final answer, so the script is
/// a queue.
/// </summary>
public sealed class ScriptedChatClient : IChatClient
{
    private readonly Queue<ChatResponse> _script;

    public ScriptedChatClient(params ChatResponse[] script)
    {
        _script = new Queue<ChatResponse>(script);
    }

    /// <summary>Every message list the agent sent, in order.</summary>
    public List<IReadOnlyList<ChatMessage>> Requests { get; } = [];

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Requests.Add(messages.ToList());

        if (_script.Count == 0)
        {
            throw new InvalidOperationException(
                $"The scripted chat client was called {Requests.Count} time(s) but the script is exhausted.");
        }

        return Task.FromResult(_script.Dequeue());
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("The POC uses the non-streaming path.");

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }

    /// <summary>A plain assistant reply, which ends the run.</summary>
    public static ChatResponse Says(string text) => new(new ChatMessage(ChatRole.Assistant, text));

    /// <summary>A tool request, always followed by at least one more script entry.</summary>
    public static ChatResponse Calls(string toolName, string callId, params (string Name, object? Value)[] arguments)
    {
        var args = arguments.ToDictionary(a => a.Name, a => a.Value);
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent(callId, toolName, args)]));
    }
}
