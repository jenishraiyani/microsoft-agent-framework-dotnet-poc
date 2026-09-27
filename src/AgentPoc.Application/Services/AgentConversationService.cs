using System.Diagnostics;
using AgentPoc.Application.Agents;
using AgentPoc.Application.DTOs;
using AgentPoc.Application.Exceptions;
using AgentPoc.Application.Interfaces;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentPoc.Application.Services;

public interface IAgentConversationService
{
    Task<AgentChatResult> SendAsync(
        string message,
        string? conversationId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Runs one conversational turn: restore the session, run the agent, persist the session,
/// and report which tools were used.
/// </summary>
public sealed class AgentConversationService : IAgentConversationService
{
    private readonly AIAgent _agent;
    private readonly IAgentSessionStore _sessionStore;
    private readonly AgentOptions _options;
    private readonly ILogger<AgentConversationService> _logger;

    public AgentConversationService(
        AIAgent agent,
        IAgentSessionStore sessionStore,
        IOptions<AgentOptions> options,
        ILogger<AgentConversationService> logger)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AgentChatResult> SendAsync(
        string message,
        string? conversationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var id = NormalizeConversationId(conversationId);

        using var logScope = _logger.BeginScope(new Dictionary<string, object> { ["ConversationId"] = id });

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(_options.RunTimeout);

        var startedAt = Stopwatch.GetTimestamp();
        _logger.LogInformation("Agent turn started ({MessageLength} characters).", message.Length);

        AgentResponse response;
        AgentSession session;
        try
        {
            session = await _sessionStore
                .GetOrCreateAsync(_agent, id, timeoutSource.Token)
                .ConfigureAwait(false);

            response = await _agent
                .RunAsync(message, session, cancellationToken: timeoutSource.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The linked token fired but the caller's did not, so this is our own timeout.
            _logger.LogWarning("Agent turn timed out after {TimeoutSeconds}s.", _options.RunTimeoutSeconds);
            throw new AgentTimeoutException(_options.RunTimeout);
        }

        // The turn already succeeded, so the updated history is saved even if the caller
        // has since disconnected.
        await _sessionStore
            .SaveAsync(_agent, id, session, CancellationToken.None)
            .ConfigureAwait(false);

        var toolCalls = ExtractToolCalls(response);
        var elapsed = Stopwatch.GetElapsedTime(startedAt);

        _logger.LogInformation(
            "Agent turn completed in {ElapsedMs} ms using {ToolCallCount} tool call(s): {ToolNames}.",
            (long)elapsed.TotalMilliseconds,
            toolCalls.Count,
            toolCalls.Count == 0 ? "(none)" : string.Join(", ", toolCalls.Select(t => t.Tool)));

        return new AgentChatResult(
            response.Text,
            id,
            toolCalls,
            (long)elapsed.TotalMilliseconds);
    }

    /// <summary>
    /// Pairs calls with results on CallId, since the model may request several tools at
    /// once and a run may span multiple round-trips.
    /// </summary>
    private static IReadOnlyList<AgentToolCall> ExtractToolCalls(AgentResponse response)
    {
        var contents = response.Messages.SelectMany(m => m.Contents).ToList();

        var results = contents
            .OfType<FunctionResultContent>()
            .GroupBy(r => r.CallId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        return contents
            .OfType<FunctionCallContent>()
            .Select(call => new AgentToolCall(
                call.Name,
                results.TryGetValue(call.CallId, out var result)
                    ? result.Exception is null ? "Success" : "Failed"
                    : "NoResult"))
            .ToList();
    }

    /// <summary>
    /// Accepts only ids this service issued. The id is a cache key, so echoing back
    /// arbitrary caller input would let a client grow the session store without bound.
    /// </summary>
    private static string NormalizeConversationId(string? conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            return Guid.NewGuid().ToString("N");
        }

        return Guid.TryParseExact(conversationId.Trim(), "N", out var parsed)
            ? parsed.ToString("N")
            : throw new InvalidConversationIdException();
    }
}
