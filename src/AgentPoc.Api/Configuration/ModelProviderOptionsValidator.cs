using Microsoft.Extensions.Options;

namespace AgentPoc.Api.Configuration;

/// <summary>
/// Which settings are required depends on the selected provider, so this is hand-written
/// rather than data annotations. Messages name the setting, never its value.
/// </summary>
public sealed class ModelProviderOptionsValidator : IValidateOptions<ModelProviderOptions>
{
    public ValidateOptionsResult Validate(string? name, ModelProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        switch (options.Provider)
        {
            case ModelProvider.AzureOpenAI:
                ValidateAzureOpenAI(options.AzureOpenAI, failures);
                break;

            case ModelProvider.OpenAI:
                ValidateOpenAI(options.OpenAI, failures);
                break;

            default:
                failures.Add($"Agent:Provider has an unsupported value '{options.Provider}'.");
                break;
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    private static void ValidateAzureOpenAI(AzureOpenAISettings settings, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(settings.Endpoint))
        {
            failures.Add("Agent:AzureOpenAI:Endpoint is required. Set AGENT__AZUREOPENAI__ENDPOINT.");
        }
        else if (!Uri.TryCreate(settings.Endpoint, UriKind.Absolute, out _))
        {
            failures.Add("Agent:AzureOpenAI:Endpoint must be an absolute URI.");
        }

        if (string.IsNullOrWhiteSpace(settings.DeploymentName))
        {
            failures.Add(
                "Agent:AzureOpenAI:DeploymentName is required. Set AGENT__AZUREOPENAI__DEPLOYMENTNAME " +
                "to the deployment name from Azure AI Foundry.");
        }

        // ApiKey is intentionally unchecked: empty selects Entra ID authentication.
    }

    private static void ValidateOpenAI(OpenAISettings settings, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(settings.ModelId))
        {
            failures.Add("Agent:OpenAI:ModelId is required. Set AGENT__OPENAI__MODELID.");
        }

        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            failures.Add("Agent:OpenAI:ApiKey is required. Set AGENT__OPENAI__APIKEY.");
        }

        if (!string.IsNullOrWhiteSpace(settings.Endpoint) &&
            !Uri.TryCreate(settings.Endpoint, UriKind.Absolute, out _))
        {
            failures.Add("Agent:OpenAI:Endpoint must be an absolute URI when supplied.");
        }
    }
}
