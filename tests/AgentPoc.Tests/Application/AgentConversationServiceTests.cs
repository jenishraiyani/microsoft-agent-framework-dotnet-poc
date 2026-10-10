using AgentPoc.Application.Agents;
using AgentPoc.Application.Exceptions;
using AgentPoc.Application.Services;
using AgentPoc.Infrastructure.Sessions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AgentPoc.Tests.Application;

public sealed class AgentConversationServiceTests
{
    /// <summary>A model that never answers, so the only way out is a cancelled token.</summary>
    private sealed class StalledChatClient : IChatClient
    {
        public async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("unreachable");
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    private static (IAgentConversationService Service, MemoryCache Cache) CreateService(
        IChatClient chatClient,
        int runTimeoutSeconds = 60)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());

        // Set directly: the [Range] guards what an operator may configure, not this test.
        var options = Options.Create(new AgentOptions { RunTimeoutSeconds = runTimeoutSeconds });

        var agent = new CustomerSupportAgentFactory(
                chatClient,
                new ToolInvocationLogger(NullLogger<ToolInvocationLogger>.Instance),
                NullLoggerFactory.Instance)
            .Create([]);

        var store = new MemoryCacheAgentSessionStore(
            cache, options, NullLogger<MemoryCacheAgentSessionStore>.Instance);

        return (
            new AgentConversationService(agent, store, options, NullLogger<AgentConversationService>.Instance),
            cache);
    }

    [Fact]
    public async Task A_conversation_id_this_service_did_not_issue_is_rejected()
    {
        var (service, cache) = CreateService(new StalledChatClient());
        using (cache)
        {
            await Assert.ThrowsAsync<InvalidConversationIdException>(
                () => service.SendAsync("hello", "../../etc/passwd", CancellationToken.None));
        }
    }

    [Fact]
    public async Task An_empty_message_is_rejected_before_the_model_is_called()
    {
        var (service, cache) = CreateService(new StalledChatClient());
        using (cache)
        {
            await Assert.ThrowsAsync<ArgumentException>(
                () => service.SendAsync("   ", null, CancellationToken.None));
        }
    }

    [Fact]
    public async Task A_model_that_never_answers_is_abandoned_at_the_configured_timeout()
    {
        var (service, cache) = CreateService(new StalledChatClient(), runTimeoutSeconds: 1);
        using (cache)
        {
            await Assert.ThrowsAsync<AgentTimeoutException>(
                () => service.SendAsync("hello", null, CancellationToken.None));
        }
    }

    [Fact]
    public async Task A_caller_who_gives_up_is_not_reported_as_a_timeout()
    {
        // Identical at the token level, but a 504 and a closed tab are not the same event.
        var (service, cache) = CreateService(new StalledChatClient(), runTimeoutSeconds: 300);
        using (cache)
        {
            using var callerGaveUp = new CancellationTokenSource();
            callerGaveUp.CancelAfter(TimeSpan.FromMilliseconds(200));

            var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => service.SendAsync("hello", null, callerGaveUp.Token));

            Assert.IsNotType<AgentTimeoutException>(exception);
        }
    }
}
