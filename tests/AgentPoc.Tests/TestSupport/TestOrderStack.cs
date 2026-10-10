using AgentPoc.Application.Interfaces;
using AgentPoc.Application.Services;
using AgentPoc.Application.Tools;
using AgentPoc.Infrastructure.Repositories;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentPoc.Tests.TestSupport;

/// <summary>
/// Seeded repository, real business service and real tools. Only the clock and the
/// authenticated identity are substituted.
/// </summary>
public sealed class TestOrderStack
{
    public TestOrderStack(string customerId = "CUST-001")
    {
        CustomerContext = new TestCustomerContext(customerId);
        Repository = new InMemoryOrderRepository(
            TimeProvider.System,
            NullLogger<InMemoryOrderRepository>.Instance);

        Service = new OrderService(Repository, CustomerContext, NullLogger<OrderService>.Instance);
        Tools = new OrderTools(Service);
        Toolset = new OrderToolset(Tools);
    }

    public ICustomerContext CustomerContext { get; }

    public IOrderRepository Repository { get; }

    public IOrderService Service { get; }

    public OrderTools Tools { get; }

    public OrderToolset Toolset { get; }
}
