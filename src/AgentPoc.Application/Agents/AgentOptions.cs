using System.ComponentModel.DataAnnotations;

namespace AgentPoc.Application.Agents;

/// <summary>Bound from the <c>Agent</c> configuration section.</summary>
public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    /// <summary>Ceiling on a single agent run, which may span several model round-trips.</summary>
    [Range(5, 300)]
    public int RunTimeoutSeconds { get; set; } = 60;

    [Range(1, 1440)]
    public int SessionIdleTimeoutMinutes { get; set; } = 30;

    public TimeSpan RunTimeout => TimeSpan.FromSeconds(RunTimeoutSeconds);

    public TimeSpan SessionIdleTimeout => TimeSpan.FromMinutes(SessionIdleTimeoutMinutes);
}
