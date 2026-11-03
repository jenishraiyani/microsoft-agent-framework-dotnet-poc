using AgentPoc.Domain.Entities;
using AgentPoc.Domain.Enums;

namespace AgentPoc.Application.Interfaces;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(string orderId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Order>> GetByCustomerIdAsync(
        string customerId,
        int limit,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the order back only if its stored status is still <paramref name="expectedStatus"/>,
    /// so the rule a caller evaluated cannot be overtaken between the check and the write.
    /// </summary>
    /// <returns><c>false</c> if the order no longer exists or has moved on.</returns>
    Task<bool> UpdateAsync(
        Order order,
        OrderStatus expectedStatus,
        CancellationToken cancellationToken = default);
}
