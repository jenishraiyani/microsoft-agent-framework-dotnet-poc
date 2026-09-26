namespace AgentPoc.Application.DTOs;

public sealed record CancellationEligibility(
    string OrderId,
    bool CanCancel,
    string Reason);

public enum CancellationOutcomeCode
{
    Cancelled = 0,
    NotEligible = 1,
    OrderNotFound = 2
}

public sealed record CancellationOutcome(
    string OrderId,
    CancellationOutcomeCode Code,
    string Status,
    string RefundStatus,
    string Message)
{
    public bool Cancelled => Code == CancellationOutcomeCode.Cancelled;
}
