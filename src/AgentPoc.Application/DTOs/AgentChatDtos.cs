namespace AgentPoc.Application.DTOs;

/// <param name="Status">
/// Whether the tool ran: <c>Success</c>, <c>Failed</c>, or <c>NoResult</c>. This is the
/// invocation outcome, not the business outcome. A refused cancellation is a
/// <c>Success</c> because the tool reported the refusal correctly.
/// </param>
public sealed record AgentToolCall(string Tool, string Status);

public sealed record AgentChatResult(
    string Response,
    string ConversationId,
    IReadOnlyList<AgentToolCall> ToolCalls,
    long ElapsedMilliseconds);
