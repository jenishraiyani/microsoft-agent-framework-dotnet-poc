namespace AgentPoc.Api.Configuration;

/// <summary>
/// How the POC determines the signed-in customer in the absence of an identity provider.
/// </summary>
public sealed class CustomerIdentityOptions
{
    public const string SectionName = "CustomerIdentity";

    /// <summary>Customer assumed when no authenticated principal is present.</summary>
    public string DevelopmentCustomerId { get; set; } = "CUST-001";

    /// <summary>
    /// Allows an <c>X-Customer-Id</c> header to select the customer in Development only, so
    /// the cross-customer refusal can be demonstrated. Ignored in every other environment.
    /// </summary>
    public bool AllowHeaderOverrideInDevelopment { get; set; } = true;
}
