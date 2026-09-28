using System.ClientModel;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;

namespace AgentPoc.Api.Configuration;

/// <summary>
/// Registers the chat model as an <see cref="IChatClient"/>. Everything downstream depends
/// on that interface, so switching provider is a configuration change.
/// </summary>
public static class ModelProviderServiceCollectionExtensions
{
    public static IServiceCollection AddModelProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<ModelProviderOptions>()
            .Bind(configuration.GetSection(ModelProviderOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<ModelProviderOptions>, ModelProviderOptionsValidator>();

        services.AddSingleton<IChatClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ModelProviderOptions>>().Value;
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(ModelProviderServiceCollectionExtensions));

            return options.Provider switch
            {
                ModelProvider.AzureOpenAI => CreateAzureOpenAIChatClient(options.AzureOpenAI, logger),
                ModelProvider.OpenAI => CreateOpenAIChatClient(options.OpenAI),
                _ => throw new InvalidOperationException($"Unsupported model provider '{options.Provider}'.")
            };
        });

        return services;
    }

    private static IChatClient CreateAzureOpenAIChatClient(AzureOpenAISettings settings, ILogger logger)
    {
        var endpoint = new Uri(settings.Endpoint!);

        AzureOpenAIClient client;
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            logger.LogInformation("Azure OpenAI: authenticating with Microsoft Entra ID (DefaultAzureCredential).");
            client = new AzureOpenAIClient(endpoint, new DefaultAzureCredential());
        }
        else
        {
            logger.LogWarning(
                "Azure OpenAI: authenticating with an API key. Prefer Microsoft Entra ID outside local development.");
            client = new AzureOpenAIClient(endpoint, new ApiKeyCredential(settings.ApiKey));
        }

        return client.GetChatClient(settings.DeploymentName).AsIChatClient();
    }

    private static IChatClient CreateOpenAIChatClient(OpenAISettings settings)
    {
        // An explicit endpoint redirects the same client at any OpenAI-compatible service.
        var options = string.IsNullOrWhiteSpace(settings.Endpoint)
            ? new OpenAIClientOptions()
            : new OpenAIClientOptions { Endpoint = new Uri(settings.Endpoint) };

        var client = new OpenAIClient(new ApiKeyCredential(settings.ApiKey!), options);
        return client.GetChatClient(settings.ModelId).AsIChatClient();
    }
}
