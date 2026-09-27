using Microsoft.Agents.AI;

namespace AgentPoc.Application.Interfaces;

/// <summary>
/// Holds the serialised <see cref="AgentSession"/> between HTTP requests.
/// </summary>
public interface IAgentSessionStore
{
    ValueTask<AgentSession> GetOrCreateAsync(
        AIAgent agent,
        string conversationId,
        CancellationToken cancellationToken = default);

    ValueTask SaveAsync(
        AIAgent agent,
        string conversationId,
        AgentSession session,
        CancellationToken cancellationToken = default);
}
