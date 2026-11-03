using System.ClientModel;
using AgentPoc.Application.Exceptions;
using Azure;
using Azure.Identity;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AgentPoc.Api.ErrorHandling;

/// <summary>
/// Maps unhandled exceptions to RFC 9457 problem responses. Only messages this application
/// wrote are returned to the caller; framework and provider exception text is logged and
/// never serialised, because it carries endpoints, deployment names and request ids.
/// </summary>
public sealed class AgentExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<AgentExceptionHandler> _logger;

    public AgentExceptionHandler(
        IProblemDetailsService problemDetailsService,
        ILogger<AgentExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService ?? throw new ArgumentNullException(nameof(problemDetailsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            _logger.LogInformation("Request aborted by the caller.");
            return true;
        }

        var (status, title, detail) = Map(exception);

        if (status >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled failure returning {StatusCode}.", status);
        }
        else
        {
            _logger.LogWarning("Request failed with {StatusCode}: {Title}", status, title);
        }

        httpContext.Response.StatusCode = status;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Instance = httpContext.Request.Path
            }
        }).ConfigureAwait(false);
    }

    private static (int Status, string Title, string Detail) Map(Exception exception) => exception switch
    {
        UnauthenticatedCustomerException =>
            (StatusCodes.Status401Unauthorized,
             "Not authenticated",
             "This request carries no authenticated customer."),

        InvalidConversationIdException ex =>
            (StatusCodes.Status400BadRequest, "Invalid conversation id", ex.Message),

        AgentTimeoutException ex =>
            (StatusCodes.Status504GatewayTimeout, "The assistant took too long to respond", ex.Message),

        // Transport, service and token-acquisition failures all mean the same thing to the
        // caller: the model could not be reached.
        ClientResultException or RequestFailedException or AuthenticationFailedException =>
            (StatusCodes.Status502BadGateway,
             "The assistant is temporarily unavailable",
             "The language model could not be reached. Please try again shortly."),

        // Everything else is a server fault, including ArgumentException: an empty or
        // oversized message is rejected by the model binder long before this point, so a
        // validation failure arriving here is a bug in this application, not a bad request.
        _ => (StatusCodes.Status500InternalServerError,
              "Unexpected error",
              "The request could not be completed. Please try again.")
    };
}
