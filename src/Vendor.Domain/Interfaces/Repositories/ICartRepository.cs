using Vendor.Domain.Aggregates.Cart;
using Vendor.Domain.Aggregates.Customer;

namespace Vendor.Domain.Interfaces.Repositories;

public interface ICartRepository
{
    Task<Cart?> GetByIdAsync(CartId id, CancellationToken ct = default);
    Task<Cart?> GetByCustomerIdAsync(CustomerId customerId, CancellationToken ct = default);
    Task<Cart?> GetBySessionIdAsync(string sessionId, CancellationToken ct = default);
    Task AddAsync(Cart cart, CancellationToken ct = default);
    Task UpdateAsync(Cart cart, CancellationToken ct = default);
    Task<IReadOnlyList<Cart>> GetAbandonedCartsAsync(DateTime abandonedBefore, CancellationToken ct = default);
}
