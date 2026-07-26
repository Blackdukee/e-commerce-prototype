using Vendor.Domain.Aggregates.AnalyticsEvent;
using Vendor.Domain.Aggregates.Customer;

namespace Vendor.Domain.Interfaces.Repositories;

public interface IAnalyticsEventRepository
{
    Task AddAsync(AnalyticsEvent analyticsEvent, CancellationToken ct = default);
    Task<IReadOnlyList<AnalyticsEvent>> GetByCustomerIdAsync(
        CustomerId customerId,
        int pageSize = 50,
        int pageIndex = 0,
        CancellationToken ct = default);
}
