using AgentPoc.Application.DTOs;
using AgentPoc.Domain.Enums;
using AgentPoc.Tests.TestSupport;

namespace AgentPoc.Tests.Application;

/// <summary>
/// The business rules, tested directly. The agent has no other way in.
/// </summary>
public sealed class OrderServiceTests
{
    // --- lookup -------------------------------------------------------------

    [Fact]
    public async Task An_existing_order_is_returned_with_its_details()
    {
        var order = await new TestOrderStack().Service.GetOrderAsync("ORD-1001", CancellationToken.None);

        Assert.NotNull(order);
        Assert.Equal("ORD-1001", order.OrderId);
        Assert.Equal(nameof(OrderStatus.Processing), order.Status);
        Assert.Equal(149.99m, order.TotalAmount);
        Assert.NotNull(order.EstimatedDeliveryDate);
    }

    [Fact]
    public async Task An_unknown_order_returns_null()
    {
        Assert.Null(await new TestOrderStack().Service.GetOrderAsync("ORD-9999", CancellationToken.None));
    }

    [Theory]
    [InlineData("ord-1001")]
    [InlineData("  ORD-1001  ")]
    [InlineData("Ord-1001")]
    public async Task Order_references_are_normalised_before_lookup(string reference)
    {
        var order = await new TestOrderStack().Service.GetOrderAsync(reference, CancellationToken.None);

        Assert.NotNull(order);
        Assert.Equal("ORD-1001", order.OrderId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("my order")]
    [InlineData("ORD-ABC")]
    [InlineData("'; DROP TABLE Orders; --")]
    public async Task A_malformed_reference_is_treated_as_a_miss_rather_than_an_error(string reference)
    {
        // A hallucinated reference is an ordinary miss, not a validation failure.
        Assert.Null(await new TestOrderStack().Service.GetOrderAsync(reference, CancellationToken.None));
    }

    [Fact]
    public async Task Another_customers_order_is_reported_as_not_found()
    {
        // ORD-1003 belongs to CUST-002. Reporting it as forbidden would let a caller
        // enumerate real order references.
        var stack = new TestOrderStack("CUST-001");

        Assert.Null(await stack.Service.GetOrderAsync("ORD-1003", CancellationToken.None));
    }

    // --- cancellation eligibility -------------------------------------------

    [Theory]
    [InlineData("ORD-1001")] // Processing, cancellation allowed
    [InlineData("ORD-1004")] // Pending, cancellation allowed
    public async Task Cancellation_is_allowed_while_the_order_is_still_being_prepared(string orderId)
    {
        var eligibility = await new TestOrderStack()
            .Service.CheckCancellationEligibilityAsync(orderId, CancellationToken.None);

        Assert.NotNull(eligibility);
        Assert.True(eligibility.CanCancel);
    }

    [Fact]
    public async Task Cancellation_is_refused_once_an_order_has_shipped()
    {
        var eligibility = await new TestOrderStack()
            .Service.CheckCancellationEligibilityAsync("ORD-1002", CancellationToken.None);

        Assert.NotNull(eligibility);
        Assert.False(eligibility.CanCancel);
        Assert.Contains("shipped", eligibility.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cancellation_is_refused_when_the_merchant_flag_is_off()
    {
        // Still Processing, so a status-only check would allow it.
        var eligibility = await new TestOrderStack()
            .Service.CheckCancellationEligibilityAsync("ORD-1006", CancellationToken.None);

        Assert.NotNull(eligibility);
        Assert.False(eligibility.CanCancel);
        Assert.Contains("not eligible", eligibility.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Eligibility_of_an_unknown_order_is_null_rather_than_false()
    {
        // Not-found and not-allowed are different answers to the customer.
        Assert.Null(await new TestOrderStack()
            .Service.CheckCancellationEligibilityAsync("ORD-9999", CancellationToken.None));
    }

    // --- cancellation -------------------------------------------------------

    [Fact]
    public async Task An_eligible_cancellation_succeeds_and_is_persisted()
    {
        var stack = new TestOrderStack();

        var outcome = await stack.Service.CancelOrderAsync("ORD-1001", CancellationToken.None);

        Assert.True(outcome.Cancelled);
        Assert.Equal(CancellationOutcomeCode.Cancelled, outcome.Code);
        Assert.Equal(nameof(OrderStatus.Cancelled), outcome.Status);
        Assert.Equal(nameof(RefundStatus.Pending), outcome.RefundStatus);

        var stored = await stack.Repository.GetByIdAsync("ORD-1001", CancellationToken.None);
        Assert.Equal(OrderStatus.Cancelled, stored!.Status);
    }

    [Theory]
    [InlineData("ORD-1002", OrderStatus.Shipped)]   // too late
    [InlineData("ORD-1006", OrderStatus.Processing)] // merchant flag off
    public async Task An_ineligible_cancellation_is_refused_and_changes_nothing(string orderId, OrderStatus expected)
    {
        // CancelOrderAsync re-evaluates eligibility rather than trusting the caller.
        var stack = new TestOrderStack();

        var outcome = await stack.Service.CancelOrderAsync(orderId, CancellationToken.None);

        Assert.False(outcome.Cancelled);
        Assert.Equal(CancellationOutcomeCode.NotEligible, outcome.Code);

        var stored = await stack.Repository.GetByIdAsync(orderId, CancellationToken.None);
        Assert.Equal(expected, stored!.Status);
        Assert.Equal(RefundStatus.NotApplicable, stored.RefundStatus);
    }

    [Fact]
    public async Task Cancelling_twice_refuses_the_second_attempt()
    {
        var stack = new TestOrderStack();

        Assert.True((await stack.Service.CancelOrderAsync("ORD-1001", CancellationToken.None)).Cancelled);

        var second = await stack.Service.CancelOrderAsync("ORD-1001", CancellationToken.None);

        Assert.False(second.Cancelled);
        Assert.Contains("already been cancelled", second.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cancelling_an_unknown_order_reports_not_found()
    {
        var outcome = await new TestOrderStack().Service.CancelOrderAsync("ORD-9999", CancellationToken.None);

        Assert.Equal(CancellationOutcomeCode.OrderNotFound, outcome.Code);
        Assert.False(outcome.Cancelled);
    }

    [Fact]
    public async Task Cancelling_another_customers_order_reports_not_found_and_changes_nothing()
    {
        var stack = new TestOrderStack("CUST-001");

        var outcome = await stack.Service.CancelOrderAsync("ORD-1003", CancellationToken.None);

        Assert.Equal(CancellationOutcomeCode.OrderNotFound, outcome.Code);

        var stored = await stack.Repository.GetByIdAsync("ORD-1003", CancellationToken.None);
        Assert.Equal(OrderStatus.Delivered, stored!.Status);
    }

    // --- refunds ------------------------------------------------------------

    [Fact]
    public async Task A_refund_in_progress_is_reported_with_its_amount()
    {
        var refund = await new TestOrderStack("CUST-002")
            .Service.GetRefundStatusAsync("ORD-1005", CancellationToken.None);

        Assert.NotNull(refund);
        Assert.Equal(nameof(RefundStatus.Pending), refund.RefundStatus);
        Assert.Equal(410.20m, refund.RefundAmount);
    }

    [Fact]
    public async Task An_active_order_has_no_refund_amount_to_quote()
    {
        var refund = await new TestOrderStack().Service.GetRefundStatusAsync("ORD-1001", CancellationToken.None);

        Assert.NotNull(refund);
        Assert.Equal(nameof(RefundStatus.NotApplicable), refund.RefundStatus);
        Assert.Null(refund.RefundAmount);
    }

    [Fact]
    public async Task Refund_status_for_an_unknown_order_is_null()
    {
        Assert.Null(await new TestOrderStack().Service.GetRefundStatusAsync("ORD-9999", CancellationToken.None));
    }

    // --- customer order listing (authorization) -----------------------------

    [Fact]
    public async Task Omitting_the_customer_id_lists_the_signed_in_customers_orders()
    {
        var result = await new TestOrderStack("CUST-001")
            .Service.GetCustomerOrdersAsync(null, CancellationToken.None);

        Assert.True(result.AccessGranted);
        Assert.NotEmpty(result.Orders);
        Assert.All(result.Orders, o => Assert.StartsWith("ORD-", o.OrderId, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Supplying_the_signed_in_customers_own_id_is_accepted()
    {
        var result = await new TestOrderStack("CUST-001")
            .Service.GetCustomerOrdersAsync("cust-001", CancellationToken.None);

        Assert.True(result.AccessGranted);
        Assert.NotEmpty(result.Orders);
    }

    [Fact]
    public async Task Supplying_another_customers_id_is_refused_and_returns_nothing()
    {
        // The customer id came from the model, so it cannot widen its own access.
        var result = await new TestOrderStack("CUST-001")
            .Service.GetCustomerOrdersAsync("CUST-002", CancellationToken.None);

        Assert.False(result.AccessGranted);
        Assert.Empty(result.Orders);
        Assert.Equal("CUST-001", result.CustomerId);
    }

    [Fact]
    public async Task Recent_orders_are_returned_newest_first()
    {
        var result = await new TestOrderStack("CUST-001")
            .Service.GetCustomerOrdersAsync(null, CancellationToken.None);

        var createdDates = result.Orders.Select(o => o.CreatedAt).ToList();
        Assert.Equal(createdDates.OrderByDescending(d => d), createdDates);
    }
}
