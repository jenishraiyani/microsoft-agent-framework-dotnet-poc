using System.ComponentModel.DataAnnotations;

namespace AgentPoc.Api.Contracts;

/// <param name="ConversationId">
/// Omit on the first turn, then echo back the value from the previous response.
/// </param>
public sealed record ChatRequest(
    // Attributes belong on the constructor parameters. MVC validates records through the
    // primary constructor and throws outright if it finds validation metadata on the
    // generated properties, which turns every request into a 500.
    [Required]
    [MaxLength(2000)]
    string Message,
    string? ConversationId = null);
