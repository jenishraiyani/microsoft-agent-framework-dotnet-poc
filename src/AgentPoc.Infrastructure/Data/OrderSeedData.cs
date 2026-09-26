using AgentPoc.Domain.Entities;
using AgentPoc.Domain.Enums;

namespace AgentPoc.Infrastructure.Data;

public static class OrderSeedData
{
    public static IReadOnlyList<Order> Create(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        return
        [
            new Order(
                id: "ORD-1001",
                customerId: "CUST-001",
                status: OrderStatus.Processing,
                totalAmount: 149.99m,
                createdAt: now.AddDays(-2),
                estimatedDeliveryDate: today.AddDays(3),
                cancellationAllowed: true,
                refundStatus: RefundStatus.NotApplicable),

            new Order(
                id: "ORD-1002",
                customerId: "CUST-001",
                status: OrderStatus.Shipped,
                totalAmount: 89.50m,
                createdAt: now.AddDays(-5),
                estimatedDeliveryDate: today.AddDays(1),
                cancellationAllowed: false,
                refundStatus: RefundStatus.NotApplicable),

            new Order(
                id: "ORD-1003",
                customerId: "CUST-002",
                status: OrderStatus.Delivered,
                totalAmount: 245.00m,
                createdAt: now.AddDays(-20),
                estimatedDeliveryDate: today.AddDays(-12),
                cancellationAllowed: false,
                refundStatus: RefundStatus.NotApplicable),

            new Order(
                id: "ORD-1004",
                customerId: "CUST-001",
                status: OrderStatus.Pending,
                totalAmount: 32.75m,
                createdAt: now.AddHours(-4),
                estimatedDeliveryDate: today.AddDays(6),
                cancellationAllowed: true,
                refundStatus: RefundStatus.NotApplicable),

            // Cancellable by status, but flagged as non-cancellable: covers the half of the
            // rule that a status-only check would miss.
            new Order(
                id: "ORD-1006",
                customerId: "CUST-001",
                status: OrderStatus.Processing,
                totalAmount: 59.00m,
                createdAt: now.AddDays(-1),
                estimatedDeliveryDate: today.AddDays(4),
                cancellationAllowed: false,
                refundStatus: RefundStatus.NotApplicable),

            new Order(
                id: "ORD-1005",
                customerId: "CUST-002",
                status: OrderStatus.Cancelled,
                totalAmount: 410.20m,
                createdAt: now.AddDays(-10),
                estimatedDeliveryDate: null,
                cancellationAllowed: false,
                refundStatus: RefundStatus.Pending)
        ];
    }
}
