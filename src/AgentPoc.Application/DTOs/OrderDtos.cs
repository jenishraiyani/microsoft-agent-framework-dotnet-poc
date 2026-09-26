namespace AgentPoc.Application.DTOs;

// These records are serialised into tool results and read by the model, so statuses are
// exposed as strings and only customer-safe fields appear.

public sealed record OrderDetails(
    string OrderId,
    string Status,
    decimal TotalAmount,
    DateTimeOffset CreatedAt,
    DateOnly? EstimatedDeliveryDate,
    string RefundStatus,
    bool CancellationAllowed);

public sealed record OrderSummary(
    string OrderId,
    string Status,
    decimal TotalAmount,
    DateTimeOffset CreatedAt,
    DateOnly? EstimatedDeliveryDate);
