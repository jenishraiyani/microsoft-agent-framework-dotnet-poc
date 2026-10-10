using AgentPoc.Application.Agents;
using AgentPoc.Application.Interfaces;
using AgentPoc.Application.Services;
using AgentPoc.Infrastructure.Sessions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AgentPoc.Tests.TestSupport;

/// <summary>
/// Wires the real agent stack over a scripted model, with production lifetimes: shared
/// stores, a new agent per turn.
/// </summary>
public sealed class TestAgentHost : IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly ICustomerSupportAgentFactory _agentFactory;
    private readonly IAgentSessionStore _sessionStore;
    private readonly AgentOptions _agentOptions = new();

    public TestAgentHost(ScriptedChatClient chatClient, string customerId = "CUST-001")
    {
        ArgumentNullException.ThrowIfNull(chatClient);

        ChatClient = chatClient;
        Stack = new TestOrderStack(customerId);

        _agentFactory = new CustomerSupportAgentFactory(
            chatClient,
            new ToolInvocationLogger(NullLogger<ToolInvocationLogger>.Instance),
            NullLoggerFactory.Instance);

        _sessionStore = new MemoryCacheAgentSessionStore(
            _cache,
            Options.Create(_agentOptions),
            NullLogger<MemoryCacheAgentSessionStore>.Instance);
    }

    public ScriptedChatClient ChatClient { get; }

    public TestOrderStack Stack { get; }

    /// <summary>Simulates one HTTP request: a new agent over the shared stores.</summary>
    public IAgentConversationService BeginRequest() => new AgentConversationService(
        _agentFactory.Create(Stack.Toolset.Tools),
        _sessionStore,
        Options.Create(_agentOptions),
        NullLogger<AgentConversationService>.Instance);

    public void Dispose() => _cache.Dispose();
}
