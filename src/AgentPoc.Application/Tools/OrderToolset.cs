using Microsoft.Extensions.AI;

namespace AgentPoc.Application.Tools;

/// <summary>
/// Wraps <see cref="OrderTools"/> methods as the tools the agent is built with. Names are
/// explicit so the model sees GetOrderStatus rather than GetOrderStatusAsync; descriptions
/// come from the Description attributes on the methods.
/// </summary>
public sealed class OrderToolset
{
    public OrderToolset(OrderTools tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        Tools =
        [
            AIFunctionFactory.Create(tools.GetOrderStatusAsync, name: "GetOrderStatus"),
            AIFunctionFactory.Create(tools.CheckCancellationEligibilityAsync, name: "CheckCancellationEligibility"),
            AIFunctionFactory.Create(tools.CancelOrderAsync, name: "CancelOrder"),
            AIFunctionFactory.Create(tools.GetRefundStatusAsync, name: "GetRefundStatus"),
            AIFunctionFactory.Create(tools.GetCustomerOrdersAsync, name: "GetCustomerOrders")
        ];
    }

    public IReadOnlyList<AITool> Tools { get; }
}
