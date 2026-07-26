using Vendor.Domain.Aggregates.Customer;

namespace Vendor.Domain.Interfaces.Repositories;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(CustomerId id, CancellationToken ct = default);
    Task<Customer?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task AddAsync(Customer customer, CancellationToken ct = default);
    Task UpdateAsync(Customer customer, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);

    Task<(IReadOnlyList<Customer> Items, int TotalCount)> GetPagedAsync(
        string? emailSearch,
        CustomerRole? role,
        CustomerStatus? status,
        DateTime? registeredFrom,
        DateTime? registeredTo,
        int pageIndex,
        int pageSize,
        CancellationToken ct = default);

    Task AddAuditLogAsync(CustomerAuditLog auditLog, CancellationToken ct = default);

    Task<(IReadOnlyList<CustomerAuditLog> Items, int TotalCount)> GetAuditLogsAsync(
        CustomerId customerId,
        int pageIndex,
        int pageSize,
        CancellationToken ct = default);
}
