using AgentPoc.Domain.Entities;
using AgentPoc.Domain.Enums;

namespace AgentPoc.Tests.Domain;

public sealed class OrderTests
{
    private static Order CreateOrder(
        OrderStatus status = OrderStatus.Processing,
        decimal totalAmount = 100m,
        bool cancellationAllowed = true) =>
        new(
            id: "ORD-1001",
            customerId: "CUST-001",
            status: status,
            totalAmount: totalAmount,
            createdAt: DateTimeOffset.UtcNow.AddDays(-1),
            estimatedDeliveryDate: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3),
            cancellationAllowed: cancellationAllowed,
            refundStatus: RefundStatus.NotApplicable);

    [Theory]
    [InlineData(OrderStatus.Pending, true)]
    [InlineData(OrderStatus.Processing, true)]
    [InlineData(OrderStatus.Shipped, false)]
    [InlineData(OrderStatus.Delivered, false)]
    [InlineData(OrderStatus.Cancelled, false)]
    public void Only_pending_and_processing_orders_are_in_a_cancellable_state(OrderStatus status, bool expected)
    {
        Assert.Equal(expected, CreateOrder(status).IsInCancellableState);
    }

    [Fact]
    public void Cancelling_moves_the_order_to_cancelled_and_opens_a_refund()
    {
        var order = CreateOrder(OrderStatus.Processing, totalAmount: 149.99m);

        order.Cancel();

        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal(RefundStatus.Pending, order.RefundStatus);
        Assert.False(order.CancellationAllowed);
        Assert.Null(order.EstimatedDeliveryDate);
    }

    [Fact]
    public void Cancelling_a_zero_value_order_leaves_no_refund_to_make()
    {
        var order = CreateOrder(OrderStatus.Pending, totalAmount: 0m);

        order.Cancel();

        Assert.Equal(RefundStatus.NotApplicable, order.RefundStatus);
    }

    [Theory]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Cancelled)]
    public void Cancelling_from_an_invalid_state_throws_rather_than_silently_succeeding(OrderStatus status)
    {
        var order = CreateOrder(status);

        // Unreachable when the service checks first. The guard exists so a caller that
        // forgets fails loudly instead of corrupting the order.
        Assert.Throws<InvalidOperationException>(order.Cancel);
    }

    [Fact]
    public void A_clone_does_not_share_state_with_its_original()
    {
        var original = CreateOrder();
        var copy = original.Clone();

        copy.Cancel();

        Assert.Equal(OrderStatus.Processing, original.Status);
        Assert.Equal(OrderStatus.Cancelled, copy.Status);
    }

    [Fact]
    public void An_order_cannot_be_constructed_without_an_identity()
    {
        Assert.Throws<ArgumentException>(() => new Order(
            id: "  ",
            customerId: "CUST-001",
            status: OrderStatus.Pending,
            totalAmount: 10m,
            createdAt: DateTimeOffset.UtcNow,
            estimatedDeliveryDate: null,
            cancellationAllowed: true,
            refundStatus: RefundStatus.NotApplicable));
    }
}
