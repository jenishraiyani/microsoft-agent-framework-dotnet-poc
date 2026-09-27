namespace AgentPoc.Application.Exceptions;

/// <summary>
/// The agent run exceeded <c>Agent:RunTimeoutSeconds</c>. Distinguished from a plain
/// <see cref="OperationCanceledException"/> so the API can tell a timeout apart from a
/// caller that disconnected.
/// </summary>
public sealed class AgentTimeoutException : Exception
{
    public AgentTimeoutException(TimeSpan timeout)
        : base($"The agent did not complete within {timeout.TotalSeconds:0} seconds.")
    {
        Timeout = timeout;
    }

    public TimeSpan Timeout { get; }
}

public sealed class InvalidConversationIdException : Exception
{
    public InvalidConversationIdException()
        : base("The supplied conversationId is not in a recognised format.")
    {
    }
}
