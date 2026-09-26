using AgentPoc.Application.DTOs;

namespace AgentPoc.Application.Interfaces;

/// <summary>
/// Order business rules. <see cref="CancelOrderAsync"/> re-evaluates eligibility itself,
/// so a caller that skips <see cref="CheckCancellationEligibilityAsync"/> still cannot
/// cancel an ineligible order.
/// </summary>
public interface IOrderService
{
    /// <returns><c>null</c> when the order does not exist or belongs to another customer.</returns>
    Task<OrderDetails?> GetOrderAsync(string orderId, CancellationToken cancellationToken = default);

    /// <returns><c>null</c> when the order does not exist or belongs to another customer.</returns>
    Task<CancellationEligibility?> CheckCancellationEligibilityAsync(
        string orderId,
        CancellationToken cancellationToken = default);

    Task<CancellationOutcome> CancelOrderAsync(string orderId, CancellationToken cancellationToken = default);

    /// <returns><c>null</c> when the order does not exist or belongs to another customer.</returns>
    Task<RefundInfo?> GetRefundStatusAsync(string orderId, CancellationToken cancellationToken = default);

    /// <param name="customerId">
    /// Pass <c>null</c> for the authenticated customer. Any other value is checked against
    /// <see cref="ICustomerContext"/> and refused when it does not match.
    /// </param>
    Task<CustomerOrdersResult> GetCustomerOrdersAsync(
        string? customerId,
        CancellationToken cancellationToken = default);
}
