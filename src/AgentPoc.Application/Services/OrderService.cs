using System.Text.RegularExpressions;
using AgentPoc.Application.DTOs;
using AgentPoc.Application.Interfaces;
using AgentPoc.Domain.Entities;
using AgentPoc.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace AgentPoc.Application.Services;

public sealed partial class OrderService : IOrderService
{
    private const int RecentOrderLimit = 10;

    private readonly IOrderRepository _repository;
    private readonly ICustomerContext _customerContext;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IOrderRepository repository,
        ICustomerContext customerContext,
        ILogger<OrderService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _customerContext = customerContext ?? throw new ArgumentNullException(nameof(customerContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<OrderDetails?> GetOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        var order = await LoadAuthorizedOrderAsync(orderId, cancellationToken).ConfigureAwait(false);
        return order is null ? null : ToDetails(order);
    }

    public async Task<CancellationEligibility?> CheckCancellationEligibilityAsync(
        string orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await LoadAuthorizedOrderAsync(orderId, cancellationToken).ConfigureAwait(false);
        if (order is null)
        {
            return null;
        }

        var (canCancel, reason) = EvaluateCancellationRule(order);
        return new CancellationEligibility(order.Id, canCancel, reason);
    }

    public async Task<CancellationOutcome> CancelOrderAsync(
        string orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await LoadAuthorizedOrderAsync(orderId, cancellationToken).ConfigureAwait(false);
        if (order is null)
        {
            return new CancellationOutcome(
                orderId,
                CancellationOutcomeCode.OrderNotFound,
                Status: "Unknown",
                RefundStatus: "Unknown",
                Message: "No order with that reference was found for this customer.");
        }

        // Evaluated here rather than trusted from the caller, so an agent that skips the
        // eligibility check cannot cancel an ineligible order.
        var (canCancel, reason) = EvaluateCancellationRule(order);
        if (!canCancel)
        {
            _logger.LogInformation("Cancellation refused for order {OrderId}: {Reason}", order.Id, reason);

            return new CancellationOutcome(
                order.Id,
                CancellationOutcomeCode.NotEligible,
                order.Status.ToString(),
                order.RefundStatus.ToString(),
                reason);
        }

        order.Cancel();

        var persisted = await _repository.UpdateAsync(order, cancellationToken).ConfigureAwait(false);
        if (!persisted)
        {
            _logger.LogWarning("Cancellation of order {OrderId} could not be persisted.", order.Id);

            return new CancellationOutcome(
                order.Id,
                CancellationOutcomeCode.OrderNotFound,
                Status: "Unknown",
                RefundStatus: "Unknown",
                Message: "The order could not be updated because it is no longer available.");
        }

        _logger.LogInformation(
            "AUDIT: order {OrderId} cancelled for customer {CustomerId}. Refund status is now {RefundStatus}.",
            order.Id,
            order.CustomerId,
            order.RefundStatus);

        return new CancellationOutcome(
            order.Id,
            CancellationOutcomeCode.Cancelled,
            order.Status.ToString(),
            order.RefundStatus.ToString(),
            order.RefundStatus == RefundStatus.Pending
                ? "The order was cancelled. A refund has been initiated and is now pending."
                : "The order was cancelled.");
    }

    public async Task<RefundInfo?> GetRefundStatusAsync(
        string orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await LoadAuthorizedOrderAsync(orderId, cancellationToken).ConfigureAwait(false);
        if (order is null)
        {
            return null;
        }

        var message = order.RefundStatus switch
        {
            RefundStatus.NotApplicable => "No refund applies to this order.",
            RefundStatus.Pending => "A refund has been initiated and is still being processed.",
            RefundStatus.Processed => "The refund has been processed.",
            _ => "Refund status is unavailable."
        };

        return new RefundInfo(
            order.Id,
            order.RefundStatus.ToString(),
            order.RefundStatus == RefundStatus.NotApplicable ? null : order.TotalAmount,
            message);
    }

    public async Task<CustomerOrdersResult> GetCustomerOrdersAsync(
        string? customerId,
        CancellationToken cancellationToken = default)
    {
        var authenticatedCustomerId = _customerContext.CustomerId;

        // The customer id originates from the model, so it is only ever used to refuse.
        if (!string.IsNullOrWhiteSpace(customerId) &&
            !string.Equals(customerId.Trim(), authenticatedCustomerId, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Blocked customer-orders lookup: requested {RequestedCustomerId}, authenticated {AuthenticatedCustomerId}.",
                Sanitize(customerId),
                authenticatedCustomerId);

            return new CustomerOrdersResult(
                authenticatedCustomerId,
                AccessGranted: false,
                Orders: [],
                Message: "Orders can only be listed for the signed-in customer.");
        }

        var orders = await _repository
            .GetByCustomerIdAsync(authenticatedCustomerId, RecentOrderLimit, cancellationToken)
            .ConfigureAwait(false);

        var summaries = orders
            .Select(o => new OrderSummary(
                o.Id, o.Status.ToString(), o.TotalAmount, o.CreatedAt, o.EstimatedDeliveryDate))
            .ToList();

        return new CustomerOrdersResult(
            authenticatedCustomerId,
            AccessGranted: true,
            summaries,
            summaries.Count == 0
                ? "No recent orders were found."
                : $"Found {summaries.Count} recent order(s).");
    }

    private static (bool CanCancel, string Reason) EvaluateCancellationRule(Order order)
    {
        if (order.Status == OrderStatus.Cancelled)
        {
            return (false, "The order has already been cancelled.");
        }

        if (!order.IsInCancellableState)
        {
            var status = order.Status.ToString().ToLowerInvariant();
            return (false, $"The order can no longer be cancelled because it has already been {status}.");
        }

        if (!order.CancellationAllowed)
        {
            return (false, "This order is not eligible for self-service cancellation.");
        }

        return (true, "The order is still being prepared, so it can be cancelled.");
    }

    /// <summary>
    /// Returns <c>null</c> unless the order exists and belongs to the authenticated
    /// customer. Another customer's order is reported as missing rather than forbidden,
    /// so the caller cannot probe which references are real.
    /// </summary>
    private async Task<Order?> LoadAuthorizedOrderAsync(string orderId, CancellationToken cancellationToken)
    {
        if (!TryNormalizeOrderId(orderId, out var normalized))
        {
            _logger.LogDebug("Rejected malformed order reference {OrderId}.", Sanitize(orderId));
            return null;
        }

        var order = await _repository.GetByIdAsync(normalized, cancellationToken).ConfigureAwait(false);
        if (order is null)
        {
            return null;
        }

        if (!string.Equals(order.CustomerId, _customerContext.CustomerId, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Blocked cross-customer access to order {OrderId} by customer {CustomerId}.",
                normalized,
                _customerContext.CustomerId);
            return null;
        }

        return order;
    }

    private static OrderDetails ToDetails(Order order) => new(
        order.Id,
        order.Status.ToString(),
        order.TotalAmount,
        order.CreatedAt,
        order.EstimatedDeliveryDate,
        order.RefundStatus.ToString(),
        order.CancellationAllowed);

    private static bool TryNormalizeOrderId(string? orderId, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(orderId))
        {
            return false;
        }

        var candidate = orderId.Trim().ToUpperInvariant();
        if (!OrderIdPattern().IsMatch(candidate))
        {
            return false;
        }

        normalized = candidate;
        return true;
    }

    /// <summary>Strips control characters so untrusted input cannot forge log lines.</summary>
    private static string Sanitize(string? value) =>
        string.IsNullOrEmpty(value)
            ? "(empty)"
            : new string(value.Where(c => !char.IsControl(c)).Take(64).ToArray());

    [GeneratedRegex(@"^ORD-\d{3,10}$", RegexOptions.CultureInvariant)]
    private static partial Regex OrderIdPattern();
}
