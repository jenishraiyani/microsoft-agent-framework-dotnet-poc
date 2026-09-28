using AgentPoc.Api.Contracts;
using AgentPoc.Application.DTOs;
using AgentPoc.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgentPoc.Api.Controllers;

[ApiController]
[Route("api/agent")]
[Produces("application/json")]
public sealed class AgentController : ControllerBase
{
    private readonly IAgentConversationService _conversationService;
    private readonly ILogger<AgentController> _logger;

    public AgentController(IAgentConversationService conversationService, ILogger<AgentController> logger)
    {
        _conversationService = conversationService ?? throw new ArgumentNullException(nameof(conversationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Sends one message to the support agent.</summary>
    /// <response code="200">The agent answered.</response>
    /// <response code="400">The message or conversation id was not acceptable.</response>
    /// <response code="502">The language model could not be reached.</response>
    /// <response code="504">The agent exceeded its time budget.</response>
    [HttpPost("chat")]
    [ProducesResponseType<AgentChatResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<AgentChatResult>> ChatAsync(
        [FromBody] ChatRequest request,
        CancellationToken cancellationToken)
    {
        // Bound from HttpContext.RequestAborted, so a caller who gives up unwinds the model
        // call and every tool underneath it.
        var result = await _conversationService
            .SendAsync(request.Message, request.ConversationId, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Answered conversation {ConversationId} in {ElapsedMs} ms.",
            result.ConversationId,
            result.ElapsedMilliseconds);

        return Ok(result);
    }
}
