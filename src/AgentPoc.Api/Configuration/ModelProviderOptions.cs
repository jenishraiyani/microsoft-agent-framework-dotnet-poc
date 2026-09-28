namespace AgentPoc.Api.Configuration;

public enum ModelProvider
{
    AzureOpenAI = 0,
    OpenAI = 1
}

/// <summary>
/// Model provider settings, bound from the <c>Agent</c> configuration section. This is the
/// only part of the solution that knows an endpoint or key exists.
/// </summary>
public sealed class ModelProviderOptions
{
    public const string SectionName = "Agent";

    public ModelProvider Provider { get; set; } = ModelProvider.AzureOpenAI;

    public AzureOpenAISettings AzureOpenAI { get; set; } = new();

    public OpenAISettings OpenAI { get; set; } = new();
}

public sealed class AzureOpenAISettings
{
    /// <summary>Resource endpoint, for example https://my-resource.openai.azure.com/.</summary>
    public string? Endpoint { get; set; }

    /// <summary>The deployment name from Azure AI Foundry, not the model name.</summary>
    public string? DeploymentName { get; set; }

    /// <summary>Leave empty to authenticate with Entra ID via DefaultAzureCredential.</summary>
    public string? ApiKey { get; set; }
}

public sealed class OpenAISettings
{
    public string? ModelId { get; set; }

    public string? ApiKey { get; set; }

    /// <summary>
    /// Optional. Leave empty for OpenAI itself, or point at any OpenAI-compatible endpoint
    /// such as GitHub Models, Groq or Ollama. The model must support tool calling.
    /// </summary>
    public string? Endpoint { get; set; }
}
