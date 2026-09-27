using AgentPoc.Application.Interfaces;
using AgentPoc.Infrastructure.Repositories;
using AgentPoc.Infrastructure.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentPoc.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.AddMemoryCache();

        // Singleton because the store is the data. With a real database this becomes
        // scoped alongside the DbContext.
        services.TryAddSingleton<IOrderRepository, InMemoryOrderRepository>();
        services.TryAddSingleton<IAgentSessionStore, MemoryCacheAgentSessionStore>();

        return services;
    }
}
