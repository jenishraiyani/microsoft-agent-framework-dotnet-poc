using Microsoft.Agents.AI;

namespace AgentPoc.Application.Interfaces;

/// <summary>
/// Holds the serialised <see cref="AgentSession"/> between HTTP requests, scoped to the
/// customer it belongs to: a conversation id on its own must not resume someone else's chat.
/// </summary>
public interface IAgentSessionStore
{
    ValueTask<AgentSession> GetOrCreateAsync(
        AIAgent agent,
        string customerId,
        string conversationId,
        CancellationToken cancellationToken = default);

    ValueTask SaveAsync(
        AIAgent agent,
        string customerId,
        string conversationId,
        AgentSession session,
        CancellationToken cancellationToken = default);
}
