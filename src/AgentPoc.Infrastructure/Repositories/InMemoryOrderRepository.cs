using System.Collections.Concurrent;
using AgentPoc.Application.Interfaces;
using AgentPoc.Domain.Entities;
using AgentPoc.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace AgentPoc.Infrastructure.Repositories;

/// <summary>
/// In-memory order store. Reads return copies so callers cannot mutate stored state
/// without going through <see cref="UpdateAsync"/>.
/// </summary>
public sealed class InMemoryOrderRepository : IOrderRepository
{
    private readonly ConcurrentDictionary<string, Order> _orders;
    private readonly ILogger<InMemoryOrderRepository> _logger;

    // Serialises read-modify-write. A real database would use a transaction or a
    // concurrency token instead.
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public InMemoryOrderRepository(TimeProvider timeProvider, ILogger<InMemoryOrderRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _orders = new ConcurrentDictionary<string, Order>(
            OrderSeedData.Create(timeProvider).Select(o => new KeyValuePair<string, Order>(o.Id, o)),
            StringComparer.OrdinalIgnoreCase);

        _logger.LogInformation("Seeded in-memory order store with {OrderCount} orders.", _orders.Count);
    }

    public Task<Order?> GetByIdAsync(string orderId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);
        cancellationToken.ThrowIfCancellationRequested();

        var found = _orders.TryGetValue(orderId, out var order) ? order.Clone() : null;
        return Task.FromResult(found);
    }

    public Task<IReadOnlyList<Order>> GetByCustomerIdAsync(
        string customerId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<Order> results = _orders.Values
            .Where(o => string.Equals(o.CustomerId, customerId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(o => o.CreatedAt)
            .Take(limit)
            .Select(o => o.Clone())
            .ToList();

        return Task.FromResult(results);
    }

    public Task<bool> ExistsAsync(string orderId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(_orders.ContainsKey(orderId));
    }

    public async Task<bool> UpdateAsync(Order order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_orders.ContainsKey(order.Id))
            {
                return false;
            }

            _orders[order.Id] = order.Clone();
            _logger.LogInformation("Persisted update for order {OrderId}.", order.Id);
            return true;
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
