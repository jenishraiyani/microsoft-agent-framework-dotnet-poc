namespace AgentPoc.Application.DTOs;

public sealed record RefundInfo(
    string OrderId,
    string RefundStatus,
    decimal? RefundAmount,
    string Message);

public sealed record CustomerOrdersResult(
    string CustomerId,
    bool AccessGranted,
    IReadOnlyList<OrderSummary> Orders,
    string Message);
