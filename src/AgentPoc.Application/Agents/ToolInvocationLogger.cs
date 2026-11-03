using System.Diagnostics;
using AgentPoc.Application.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentPoc.Application.Agents;

/// <summary>
/// Runs around every tool invocation the agent makes. Arguments are not logged: they come
/// from the model and may contain whatever the customer typed.
/// </summary>
public sealed class ToolInvocationLogger
{
    private readonly ILogger<ToolInvocationLogger> _logger;

    public ToolInvocationLogger(ILogger<ToolInvocationLogger> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async ValueTask<object?> InvokeAsync(
        AIAgent agent,
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var toolName = context.Function.Name;
        var timestamp = Stopwatch.GetTimestamp();

        _logger.LogInformation("Tool {ToolName} invoked (iteration {Iteration}).", toolName, context.Iteration);

        try
        {
            var result = await next(context, cancellationToken).ConfigureAwait(false);
            var elapsedMs = Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds;

            // A business refusal comes back as a result, not an exception, so the log has to
            // read it. Otherwise every refused cancellation is recorded as a success.
            if (ToolOutcome.IsRefusal(result))
            {
                _logger.LogInformation(
                    "Tool {ToolName} refused the request in {ElapsedMs} ms.", toolName, elapsedMs);
            }
            else
            {
                _logger.LogInformation("Tool {ToolName} succeeded in {ElapsedMs} ms.", toolName, elapsedMs);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Tool {ToolName} was cancelled.", toolName);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Tool {ToolName} failed after {ElapsedMs} ms.",
                toolName,
                Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds);
            throw;
        }
    }
}
