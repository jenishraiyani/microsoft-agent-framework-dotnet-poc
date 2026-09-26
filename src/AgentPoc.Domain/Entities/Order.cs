using AgentPoc.Domain.Enums;

namespace AgentPoc.Domain.Entities;

public sealed class Order
{
    public Order(
        string id,
        string customerId,
        OrderStatus status,
        decimal totalAmount,
        DateTimeOffset createdAt,
        DateOnly? estimatedDeliveryDate,
        bool cancellationAllowed,
        RefundStatus refundStatus)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(customerId);
        ArgumentOutOfRangeException.ThrowIfNegative(totalAmount);

        Id = id;
        CustomerId = customerId;
        Status = status;
        TotalAmount = totalAmount;
        CreatedAt = createdAt;
        EstimatedDeliveryDate = estimatedDeliveryDate;
        CancellationAllowed = cancellationAllowed;
        RefundStatus = refundStatus;
    }

    public string Id { get; }

    public string CustomerId { get; }

    public OrderStatus Status { get; private set; }

    public decimal TotalAmount { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateOnly? EstimatedDeliveryDate { get; private set; }

    /// <summary>Merchant-level flag, for example cleared off for final-sale items.</summary>
    public bool CancellationAllowed { get; private set; }

    public RefundStatus RefundStatus { get; private set; }

    public bool IsInCancellableState => Status is OrderStatus.Pending or OrderStatus.Processing;

    /// <summary>
    /// Throws if the order is not in a cancellable state. Callers are expected to check
    /// eligibility first; this guard exists so an unchecked call fails instead of
    /// reporting a cancellation that never happened.
    /// </summary>
    public void Cancel()
    {
        if (!IsInCancellableState)
        {
            throw new InvalidOperationException(
                $"Order '{Id}' cannot be cancelled from status '{Status}'.");
        }

        Status = OrderStatus.Cancelled;
        CancellationAllowed = false;
        EstimatedDeliveryDate = null;
        RefundStatus = TotalAmount > 0m ? RefundStatus.Pending : RefundStatus.NotApplicable;
    }

    public Order Clone() => new(
        Id, CustomerId, Status, TotalAmount, CreatedAt,
        EstimatedDeliveryDate, CancellationAllowed, RefundStatus);
}
