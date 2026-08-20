using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Aggregates.ReturnRequest;

namespace Vendor.Domain.Interfaces.Repositories;

public interface IReturnRequestRepository
{
    Task<ReturnRequest?> GetByIdAsync(ReturnRequestId id, CancellationToken ct = default);
    Task<IReadOnlyList<ReturnRequest>> GetByOrderIdAsync(OrderId orderId, CancellationToken ct = default);
    Task<(IReadOnlyList<ReturnRequest> Items, int TotalCount)> GetPagedAsync(ReturnRequestStatus? status, int pageIndex, int pageSize, CancellationToken ct = default);
    Task AddAsync(ReturnRequest request, CancellationToken ct = default);
    Task UpdateAsync(ReturnRequest request, CancellationToken ct = default);
}
