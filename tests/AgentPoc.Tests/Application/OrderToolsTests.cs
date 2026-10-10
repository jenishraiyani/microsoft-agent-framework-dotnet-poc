using AgentPoc.Domain.Enums;
using AgentPoc.Tests.TestSupport;

namespace AgentPoc.Tests.Application;

/// <summary>
/// The tool layer turns every business answer, including refusals, into data the model
/// can read, and never throws.
/// </summary>
public sealed class OrderToolsTests
{
    [Fact]
    public async Task A_successful_lookup_carries_the_order_as_data()
    {
        var result = await new TestOrderStack().Tools
            .GetOrderStatusAsync("ORD-1001", CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("ORD-1001", result.Data.OrderId);
    }

    [Fact]
    public async Task A_missing_order_is_a_result_not_an_exception()
    {
        var result = await new TestOrderStack().Tools
            .GetOrderStatusAsync("ORD-9999", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Contains("No order matching", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_refused_cancellation_reports_failure_with_the_reason()
    {
        var stack = new TestOrderStack();

        var result = await stack.Tools.CancelOrderAsync("ORD-1002", CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.Data);
        Assert.False(result.Data.Cancelled);
        Assert.Contains("shipped", result.Message, StringComparison.OrdinalIgnoreCase);

        var stored = await stack.Repository.GetByIdAsync("ORD-1002", CancellationToken.None);
        Assert.Equal(OrderStatus.Shipped, stored!.Status);
    }

    [Fact]
    public async Task A_permitted_cancellation_reports_success()
    {
        var result = await new TestOrderStack().Tools.CancelOrderAsync("ORD-1001", CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.Data!.Cancelled);
    }

    [Fact]
    public async Task A_refused_customer_listing_returns_no_data_at_all()
    {
        // Null rather than an empty list, so the model cannot read it as "no orders".
        var result = await new TestOrderStack("CUST-001").Tools
            .GetCustomerOrdersAsync("CUST-002", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task Omitting_the_customer_id_lists_the_signed_in_customers_orders()
    {
        var result = await new TestOrderStack("CUST-001").Tools
            .GetCustomerOrdersAsync(cancellationToken: CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.NotEmpty(result.Data);
    }

    [Fact]
    public async Task Cancellation_flows_from_the_tool_into_the_service()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new TestOrderStack().Tools.GetOrderStatusAsync("ORD-1001", cts.Token));
    }
}
