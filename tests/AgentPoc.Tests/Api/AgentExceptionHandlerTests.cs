using System.ClientModel;
using Azure;
using AgentPoc.Api.ErrorHandling;
using AgentPoc.Application.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentPoc.Tests.Api;

/// <summary>
/// The handler decides both the status code and how much of the failure the caller sees.
/// </summary>
public sealed class AgentExceptionHandlerTests
{
    private sealed class CapturingProblemDetailsService : IProblemDetailsService
    {
        public ProblemDetails? Captured { get; private set; }

        public ValueTask<bool> TryWriteAsync(ProblemDetailsContext context)
        {
            Captured = context.ProblemDetails;
            return ValueTask.FromResult(true);
        }

        public ValueTask WriteAsync(ProblemDetailsContext context)
        {
            Captured = context.ProblemDetails;
            return ValueTask.CompletedTask;
        }
    }

    private static async Task<ProblemDetails> HandleAsync(Exception exception)
    {
        var problemDetails = new CapturingProblemDetailsService();
        var handler = new AgentExceptionHandler(problemDetails, NullLogger<AgentExceptionHandler>.Instance);

        Assert.True(await handler.TryHandleAsync(new DefaultHttpContext(), exception, CancellationToken.None));

        return problemDetails.Captured!;
    }

    [Fact]
    public async Task An_argument_exception_is_a_server_fault_and_its_text_stays_out_of_the_response()
    {
        // Model binding rejects an empty or oversized message, so one arriving here is a bug.
        var problem = await HandleAsync(new ArgumentException("seedCustomerId was empty", "seedCustomerId"));

        Assert.Equal(StatusCodes.Status500InternalServerError, problem.Status);
        Assert.DoesNotContain("seedCustomerId", problem.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_provider_failure_is_reported_without_the_endpoint_it_names()
    {
        var problem = await HandleAsync(
            new ClientResultException("https://contoso.openai.azure.com/ deployment gpt-4o request abc123"));

        Assert.Equal(StatusCodes.Status502BadGateway, problem.Status);
        Assert.DoesNotContain("contoso", problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_rate_limit_is_not_reported_as_an_outage()
    {
        // 502 invites an immediate retry, which is the worst response to a rate limit.
        var problem = await HandleAsync(new RequestFailedException(429, "rate limit exceeded"));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, problem.Status);
    }

    [Fact]
    public async Task A_message_the_model_rejects_is_not_reported_as_retryable()
    {
        var problem = await HandleAsync(new RequestFailedException(400, "content filter triggered"));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, problem.Status);
        Assert.DoesNotContain("try again", problem.Detail!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task An_unauthenticated_request_is_refused_with_401()
    {
        var problem = await HandleAsync(new UnauthenticatedCustomerException());

        Assert.Equal(StatusCodes.Status401Unauthorized, problem.Status);
    }

    [Fact]
    public async Task A_conversation_id_this_api_did_not_issue_is_a_client_error()
    {
        var problem = await HandleAsync(new InvalidConversationIdException());

        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
    }
}
