using AgentPoc.Application.Agents;
using AgentPoc.Application.Interfaces;
using AgentPoc.Application.Services;
using AgentPoc.Application.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentPoc.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<AgentOptions>()
            .Bind(configuration.GetSection(AgentOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddScoped<IOrderService, OrderService>();

        services.TryAddSingleton<ToolInvocationLogger>();
        services.TryAddSingleton<ICustomerSupportAgentFactory, CustomerSupportAgentFactory>();

        // Scoped: tools capture the request-scoped customer context, so the agent built
        // from them is request-scoped too.
        services.TryAddScoped<OrderTools>();
        services.TryAddScoped<OrderToolset>();

        services.TryAddScoped<AIAgent>(sp =>
            sp.GetRequiredService<ICustomerSupportAgentFactory>()
              .Create(sp.GetRequiredService<OrderToolset>().Tools));

        services.TryAddScoped<IAgentConversationService, AgentConversationService>();

        return services;
    }
}
