using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentPoc.Application.Agents;

public interface ICustomerSupportAgentFactory
{
    AIAgent Create(IEnumerable<AITool> tools);
}

/// <summary>
/// Builds the support agent over whichever <see cref="IChatClient"/> the composition root
/// registered. Agents are created per request because their tools capture request-scoped
/// services; the underlying HTTP client stays a singleton, and conversation state lives in
/// the session rather than the agent.
/// </summary>
public sealed class CustomerSupportAgentFactory : ICustomerSupportAgentFactory
{
    private readonly IChatClient _chatClient;
    private readonly ToolInvocationLogger _toolInvocationLogger;
    private readonly ILoggerFactory _loggerFactory;

    public CustomerSupportAgentFactory(
        IChatClient chatClient,
        ToolInvocationLogger toolInvocationLogger,
        ILoggerFactory loggerFactory)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _toolInvocationLogger = toolInvocationLogger ?? throw new ArgumentNullException(nameof(toolInvocationLogger));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    public AIAgent Create(IEnumerable<AITool> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        var agent = new ChatClientAgent(
            _chatClient,
            instructions: CustomerSupportAgentInstructions.Instructions,
            name: CustomerSupportAgentInstructions.AgentName,
            description: CustomerSupportAgentInstructions.AgentDescription,
            tools: [.. tools],
            loggerFactory: _loggerFactory);

        return agent
            .AsBuilder()
            .Use(_toolInvocationLogger.InvokeAsync)
            .UseLogging(_loggerFactory)
            .Build();
    }
}
