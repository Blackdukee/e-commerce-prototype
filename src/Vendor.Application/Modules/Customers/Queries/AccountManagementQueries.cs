using Vendor.Application.Common.Messaging;
using Vendor.Application.Common.Models;
using Vendor.Application.Common.Results;
using Vendor.Application.Modules.Orders.Dtos;
using Vendor.Domain.Aggregates.Customer;

namespace Vendor.Application.Modules.Customers.Queries;

public record AdminCustomerDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string CustomerType,
    string Role,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? SuspendedAtUtc,
    string? SuspensionReason);

public record CustomerDetailDto(
    AdminCustomerDto Profile,
    IReadOnlyList<OrderDto> OrderHistory);

public record CustomerAuditLogDto(
    Guid Id,
    Guid CustomerId,
    string EventType,
    string DetailsJson,
    Guid PerformedByCustomerId,
    DateTime TimestampUtc);

public record GetAdminCustomersQuery(
    string? Email = null,
    CustomerRole? Role = null,
    CustomerStatus? Status = null,
    DateTime? RegisteredFrom = null,
    DateTime? RegisteredTo = null,
    int PageIndex = 0,
    int PageSize = 20) : IQuery<Result<PagedResult<AdminCustomerDto>>>;

public record GetCustomerDetailQuery(Guid CustomerId) : IQuery<Result<CustomerDetailDto>>;

public record GetCustomerAuditLogsQuery(Guid CustomerId, int PageIndex = 0, int PageSize = 20) : IQuery<Result<PagedResult<CustomerAuditLogDto>>>;
