using AgentPoc.Domain.Entities;

namespace AgentPoc.Application.Interfaces;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(string orderId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Order>> GetByCustomerIdAsync(
        string customerId,
        int limit,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string orderId, CancellationToken cancellationToken = default);

    /// <returns><c>false</c> if the order no longer exists.</returns>
    Task<bool> UpdateAsync(Order order, CancellationToken cancellationToken = default);
}
