using System.ComponentModel;
using AgentPoc.Application.DTOs;
using AgentPoc.Application.Interfaces;

namespace AgentPoc.Application.Tools;

/// <summary>
/// The capabilities exposed to the agent. Each method adapts <see cref="IOrderService"/>
/// for the model and decides nothing itself. The Description text is what the model uses
/// to choose a tool, so it states when to use each one and what it will not do.
/// </summary>
public sealed class OrderTools
{
    private readonly IOrderService _orderService;

    public OrderTools(IOrderService orderService)
    {
        _orderService = orderService ?? throw new ArgumentNullException(nameof(orderService));
    }

    [Description(
        "Gets the current status of one specific order, including its estimated delivery " +
        "date, total amount and refund status. Use this whenever the customer asks where " +
        "an order is, what is happening with it, when it will arrive, or for any detail " +
        "about an order they have identified.")]
    public async Task<ToolResult<OrderDetails>> GetOrderStatusAsync(
        [Description("The order reference the customer gave, for example ORD-1001.")]
        string orderId,
        CancellationToken cancellationToken)
    {
        var order = await _orderService.GetOrderAsync(orderId, cancellationToken).ConfigureAwait(false);

        return order is null
            ? ToolResult<OrderDetails>.NotFound(orderId)
            : ToolResult<OrderDetails>.Ok(order, $"Order {order.OrderId} is currently {order.Status}.");
    }

    [Description(
        "Checks whether one specific order can still be cancelled and returns the reason " +
        "either way. Use this whenever the customer asks IF an order can be cancelled, and " +
        "always before actually cancelling one. This only checks eligibility; it never " +
        "changes the order.")]
    public async Task<ToolResult<CancellationEligibility>> CheckCancellationEligibilityAsync(
        [Description("The order reference the customer gave, for example ORD-1001.")]
        string orderId,
        CancellationToken cancellationToken)
    {
        var eligibility = await _orderService
            .CheckCancellationEligibilityAsync(orderId, cancellationToken)
            .ConfigureAwait(false);

        return eligibility is null
            ? ToolResult<CancellationEligibility>.NotFound(orderId)
            : ToolResult<CancellationEligibility>.Ok(eligibility, eligibility.Reason);
    }

    [Description(
        "Cancels one specific order. Only use this when the customer has clearly asked for " +
        "the order to be cancelled, not when they are merely asking whether it is possible. " +
        "The request is re-checked against the cancellation rules and may be refused, so " +
        "always read the result before telling the customer anything.")]
    public async Task<ToolResult<CancellationOutcome>> CancelOrderAsync(
        [Description("The order reference the customer asked to cancel, for example ORD-1001.")]
        string orderId,
        CancellationToken cancellationToken)
    {
        var outcome = await _orderService.CancelOrderAsync(orderId, cancellationToken).ConfigureAwait(false);

        return new ToolResult<CancellationOutcome>(outcome.Cancelled, outcome.Message, outcome);
    }

    [Description(
        "Gets the refund status for one specific order, and the refund amount when a refund " +
        "is actually in progress. Use this whenever the customer asks about a refund, their " +
        "money back, or when they will be repaid.")]
    public async Task<ToolResult<RefundInfo>> GetRefundStatusAsync(
        [Description("The order reference the customer gave, for example ORD-1002.")]
        string orderId,
        CancellationToken cancellationToken)
    {
        var refund = await _orderService.GetRefundStatusAsync(orderId, cancellationToken).ConfigureAwait(false);

        return refund is null
            ? ToolResult<RefundInfo>.NotFound(orderId)
            : ToolResult<RefundInfo>.Ok(refund, refund.Message);
    }

    [Description(
        "Lists the signed-in customer's recent orders, newest first. Use this when the " +
        "customer asks about their orders in general, or when they refer to an order " +
        "without giving a reference, so you can work out which one they mean.")]
    public async Task<ToolResult<IReadOnlyList<OrderSummary>>> GetCustomerOrdersAsync(
        [Description(
            "Optional. Only pass a customer identifier if the customer explicitly stated " +
            "one. Leave it out to list the signed-in customer's own orders.")]
        // The default value is what makes the parameter optional in the generated schema.
        // Without it the model must supply a customer id, and will invent one.
        string? customerId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _orderService
            .GetCustomerOrdersAsync(customerId, cancellationToken)
            .ConfigureAwait(false);

        return new ToolResult<IReadOnlyList<OrderSummary>>(
            result.AccessGranted,
            result.Message,
            result.AccessGranted ? result.Orders : null);
    }
}
