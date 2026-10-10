using AgentPoc.Application.Interfaces;
using AgentPoc.Domain.Entities;
using AgentPoc.Domain.Enums;
using AgentPoc.Infrastructure.Repositories;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentPoc.Tests.Infrastructure;

public sealed class InMemoryOrderRepositoryTests
{
    private static IOrderRepository CreateRepository() =>
        new InMemoryOrderRepository(TimeProvider.System, NullLogger<InMemoryOrderRepository>.Instance);

    [Fact]
    public async Task Lookups_ignore_casing()
    {
        var repository = CreateRepository();

        Assert.NotNull(await repository.GetByIdAsync("ord-1001", CancellationToken.None));
        Assert.True(await repository.ExistsAsync("Ord-1001", CancellationToken.None));
    }

    [Fact]
    public async Task Reads_hand_back_copies_so_stored_state_cannot_be_mutated_in_place()
    {
        var repository = CreateRepository();

        var first = await repository.GetByIdAsync("ORD-1001", CancellationToken.None);
        first!.Cancel();

        var second = await repository.GetByIdAsync("ORD-1001", CancellationToken.None);

        Assert.Equal(OrderStatus.Processing, second!.Status);
    }

    [Fact]
    public async Task Updating_an_order_that_does_not_exist_reports_failure_instead_of_creating_one()
    {
        var repository = CreateRepository();

        var ghost = new Order(
            id: "ORD-7777",
            customerId: "CUST-001",
            status: OrderStatus.Pending,
            totalAmount: 5m,
            createdAt: DateTimeOffset.UtcNow,
            estimatedDeliveryDate: null,
            cancellationAllowed: true,
            refundStatus: RefundStatus.NotApplicable);

        Assert.False(await repository.UpdateAsync(ghost, CancellationToken.None));
        Assert.False(await repository.ExistsAsync("ORD-7777", CancellationToken.None));
    }

    [Fact]
    public async Task Customer_listings_respect_the_requested_limit()
    {
        var repository = CreateRepository();

        var orders = await repository.GetByCustomerIdAsync("CUST-001", limit: 2, CancellationToken.None);

        Assert.Equal(2, orders.Count);
    }

    [Fact]
    public async Task A_cancelled_token_stops_a_read_before_it_touches_the_store()
    {
        var repository = CreateRepository();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => repository.GetByIdAsync("ORD-1001", cts.Token));
    }
}
