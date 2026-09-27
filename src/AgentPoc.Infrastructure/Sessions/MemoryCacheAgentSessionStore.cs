using System.Text.Json;
using AgentPoc.Application.Agents;
using AgentPoc.Application.Interfaces;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentPoc.Infrastructure.Sessions;

/// <summary>
/// Stores the serialised session in the process memory cache, keyed by conversation id.
/// Serialising rather than holding the live object keeps the agent rebuildable per request
/// and makes a move to Redis a change to this class alone.
/// </summary>
public sealed class MemoryCacheAgentSessionStore : IAgentSessionStore
{
    private const string KeyPrefix = "agent-session:";

    private readonly IMemoryCache _cache;
    private readonly AgentOptions _options;
    private readonly ILogger<MemoryCacheAgentSessionStore> _logger;

    public MemoryCacheAgentSessionStore(
        IMemoryCache cache,
        IOptions<AgentOptions> options,
        ILogger<MemoryCacheAgentSessionStore> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async ValueTask<AgentSession> GetOrCreateAsync(
        AIAgent agent,
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);

        if (_cache.TryGetValue(KeyFor(conversationId), out JsonElement stored))
        {
            _logger.LogDebug("Resuming conversation {ConversationId}.", conversationId);
            return await agent.DeserializeSessionAsync(stored, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        _logger.LogDebug("Starting conversation {ConversationId}.", conversationId);
        return await agent.CreateSessionAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask SaveAsync(
        AIAgent agent,
        string conversationId,
        AgentSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);

        var serialized = await agent.SerializeSessionAsync(session, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        // Sliding expiry: active conversations stay, abandoned ones are evicted.
        _cache.Set(
            KeyFor(conversationId),
            serialized,
            new MemoryCacheEntryOptions { SlidingExpiration = _options.SessionIdleTimeout });
    }

    private static string KeyFor(string conversationId) => KeyPrefix + conversationId;
}
