namespace AgentPoc.Application.Tools;

/// <summary>
/// Envelope returned by every tool. Business refusals are reported as data rather than
/// thrown, because the agent has to read them to explain them.
/// </summary>
public sealed record ToolResult<T>(bool Success, string Message, T? Data)
{
    public static ToolResult<T> Ok(T data, string message) => new(true, message, data);

    public static ToolResult<T> Failed(string message) => new(false, message, default);

    public static ToolResult<T> NotFound(string orderId) => new(
        false,
        $"No order matching '{orderId}' was found for this customer.",
        default);
}
