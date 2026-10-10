using AgentPoc.Application.Interfaces;

namespace AgentPoc.Tests.TestSupport;

public sealed class TestCustomerContext : ICustomerContext
{
    public TestCustomerContext(string customerId = "CUST-001") => CustomerId = customerId;

    public string CustomerId { get; }
}
