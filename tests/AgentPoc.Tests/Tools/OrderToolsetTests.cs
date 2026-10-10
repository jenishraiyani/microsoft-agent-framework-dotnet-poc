using System.Text.Json;
using AgentPoc.Tests.TestSupport;
using Microsoft.Extensions.AI;

namespace AgentPoc.Tests.Tools;

/// <summary>
/// Guards the contract the model sees. A silent rename or a dropped description is a
/// behavioural regression that still compiles.
/// </summary>
public sealed class OrderToolsetTests
{
    private static readonly string[] ExpectedToolNames =
    [
        "GetOrderStatus",
        "CheckCancellationEligibility",
        "CancelOrder",
        "GetRefundStatus",
        "GetCustomerOrders"
    ];

    [Fact]
    public void Toolset_exposes_the_five_documented_capabilities()
    {
        var toolset = new TestOrderStack().Toolset;

        Assert.Equal(ExpectedToolNames, toolset.Tools.Select(t => t.Name).ToArray());
    }

    [Fact]
    public void Every_tool_has_a_description_for_the_model_to_select_on()
    {
        var toolset = new TestOrderStack().Toolset;

        Assert.All(toolset.Tools, tool => Assert.False(string.IsNullOrWhiteSpace(tool.Description)));
    }

    [Fact]
    public void Order_tools_require_an_order_id_and_never_expose_the_cancellation_token()
    {
        var toolset = new TestOrderStack().Toolset;

        foreach (var tool in toolset.Tools.OfType<AIFunction>().Where(t => t.Name != "GetCustomerOrders"))
        {
            var schema = JsonSerializer.Serialize(tool.JsonSchema);

            Assert.Contains("orderId", schema, StringComparison.Ordinal);

            Assert.DoesNotContain("cancellationToken", schema, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Customer_order_listing_does_not_force_the_model_to_invent_a_customer_id()
    {
        var toolset = new TestOrderStack().Toolset;
        var tool = Assert.IsAssignableFrom<AIFunction>(
            toolset.Tools.Single(t => t.Name == "GetCustomerOrders"));

        var schema = JsonSerializer.Serialize(tool.JsonSchema);

        // Optional: omitting it means the signed-in customer.
        Assert.DoesNotContain("\"required\"", schema, StringComparison.Ordinal);
    }
}
