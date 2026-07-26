using MediatR;
using Vendor.Application.Common.Messaging;
using Vendor.Application.Common.Results;
using Vendor.Application.Interfaces;
using Vendor.Application.Modules.Auth;
using Vendor.Application.Modules.Customers.Commands;
using Vendor.Application.Modules.Customers.Queries;
using Vendor.Application.Modules.Orders.Dtos;
using Vendor.Domain.Aggregates.Customer;
using Vendor.Domain.Interfaces.Repositories;
using Vendor.Domain.ValueObjects;

namespace Vendor.Application.Modules.Customers;

public record RegisterGuestCustomerCommand(string Email, string FirstName, string LastName) : ICommand<Result<CustomerDto>>;
public record ConvertGuestToRegisteredCommand(Guid CustomerId, string Email) : ICommand<Result<CustomerDto>>;
public record UpdateCustomerProfileCommand(Guid CustomerId, string FirstName, string LastName) : ICommand<Result<CustomerDto>>;
public record AddShippingAddressCommand(Guid CustomerId, AddressDto Address) : ICommand<Result<CustomerDto>>;
public record RemoveShippingAddressCommand(Guid CustomerId, int AddressIndex) : ICommand<Result<CustomerDto>>, IIdempotentRequest<Result<CustomerDto>>
{
    public string IdempotencyKey => $"REM-ADDR-{CustomerId}-{AddressIndex}";
}
public record UpdateAnalyticsConsentCommand(Guid CustomerId, bool Granted) : ICommand<Result>, IIdempotentRequest<Result>
{
    public string IdempotencyKey => $"CONSENT-{CustomerId}-{Granted}";
}

public record GetCustomerByIdQuery(Guid CustomerId) : IQuery<Result<CustomerDto>>;
public record GetCustomerByEmailQuery(string Email) : IQuery<Result<CustomerDto>>;
public record GetCustomerOrderHistoryQuery(Guid CustomerId, int PageIndex = 0, int PageSize = 20) : IQuery<Result<IReadOnlyList<OrderDto>>>;

public class RegisterGuestCustomerCommandHandler(ICustomerRepository customerRepository) : IRequestHandler<RegisterGuestCustomerCommand, Result<CustomerDto>>
{
    public async Task<Result<CustomerDto>> Handle(RegisterGuestCustomerCommand request, CancellationToken ct)
    {
        var customer = new Customer(CustomerId.New(), request.Email, request.FirstName, request.LastName, CustomerType.Guest);
        await customerRepository.AddAsync(customer, ct);
        return new CustomerDto(customer.Id.Value, customer.Email, customer.FirstName, customer.LastName, customer.CustomerType.ToString(), customer.AnalyticsConsent);
    }
}

public class GetCustomerByIdQueryHandler(ICustomerRepository customerRepository) : IRequestHandler<GetCustomerByIdQuery, Result<CustomerDto>>
{
    public async Task<Result<CustomerDto>> Handle(GetCustomerByIdQuery request, CancellationToken ct)
    {
        var customer = await customerRepository.GetByIdAsync(new CustomerId(request.CustomerId), ct);
        if (customer == null) return Error.NotFound("Customer", request.CustomerId);
        return new CustomerDto(customer.Id.Value, customer.Email, customer.FirstName, customer.LastName, customer.CustomerType.ToString(), customer.AnalyticsConsent);
    }
}

public class SuspendCustomerCommandHandler(
    ICustomerRepository customerRepository,
    ITokenService tokenService,
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork) : IRequestHandler<SuspendCustomerCommand, Result>
{
    public async Task<Result> Handle(SuspendCustomerCommand request, CancellationToken ct)
    {
        var targetCustomer = await customerRepository.GetByIdAsync(new CustomerId(request.TargetCustomerId), ct);
        if (targetCustomer == null) return Error.NotFound("Customer", request.TargetCustomerId);

        var callerIdGuid = currentUserService.CustomerId ?? Guid.Empty;
        var callerId = new CustomerId(callerIdGuid);

        targetCustomer.Suspend(request.Reason, callerId);
        await tokenService.RevokeAllTokensForUserAsync(targetCustomer.Id.Value, ct);

        var auditLog = new CustomerAuditLog(
            Guid.NewGuid(),
            targetCustomer.Id,
            "Suspended",
            $"{{\"reason\":\"{request.Reason}\"}}",
            callerId,
            DateTime.UtcNow);

        await customerRepository.AddAuditLogAsync(auditLog, ct);
        await customerRepository.UpdateAsync(targetCustomer, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}

public class ReactivateCustomerCommandHandler(
    ICustomerRepository customerRepository,
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork) : IRequestHandler<ReactivateCustomerCommand, Result>
{
    public async Task<Result> Handle(ReactivateCustomerCommand request, CancellationToken ct)
    {
        var targetCustomer = await customerRepository.GetByIdAsync(new CustomerId(request.TargetCustomerId), ct);
        if (targetCustomer == null) return Error.NotFound("Customer", request.TargetCustomerId);

        var callerIdGuid = currentUserService.CustomerId ?? Guid.Empty;
        var callerId = new CustomerId(callerIdGuid);

        targetCustomer.Reactivate(callerId);

        var auditLog = new CustomerAuditLog(
            Guid.NewGuid(),
            targetCustomer.Id,
            "Reactivated",
            "{}",
            callerId,
            DateTime.UtcNow);

        await customerRepository.AddAuditLogAsync(auditLog, ct);
        await customerRepository.UpdateAsync(targetCustomer, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}

public class PromoteCustomerCommandHandler(
    ICustomerRepository customerRepository,
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork) : IRequestHandler<PromoteCustomerCommand, Result>
{
    public async Task<Result> Handle(PromoteCustomerCommand request, CancellationToken ct)
    {
        var callerIdGuid = currentUserService.CustomerId ?? Guid.Empty;
        var caller = await customerRepository.GetByIdAsync(new CustomerId(callerIdGuid), ct);

        if (caller == null || caller.Role != CustomerRole.SuperAdmin)
        {
            return Error.Forbidden("ACCESS_DENIED", "Only SuperAdmin callers can perform role promotions.");
        }

        var targetCustomer = await customerRepository.GetByIdAsync(new CustomerId(request.TargetCustomerId), ct);
        if (targetCustomer == null) return Error.NotFound("Customer", request.TargetCustomerId);

        var previousRole = targetCustomer.Role;
        targetCustomer.ChangeRole(CustomerRole.Admin, caller.Id);

        var auditLog = new CustomerAuditLog(
            Guid.NewGuid(),
            targetCustomer.Id,
            "RoleChanged",
            $"{{\"previousRole\":\"{previousRole}\",\"newRole\":\"Admin\"}}",
            caller.Id,
            DateTime.UtcNow);

        await customerRepository.AddAuditLogAsync(auditLog, ct);
        await customerRepository.UpdateAsync(targetCustomer, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}

public class DemoteCustomerCommandHandler(
    ICustomerRepository customerRepository,
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork) : IRequestHandler<DemoteCustomerCommand, Result>
{
    public async Task<Result> Handle(DemoteCustomerCommand request, CancellationToken ct)
    {
        var callerIdGuid = currentUserService.CustomerId ?? Guid.Empty;
        var caller = await customerRepository.GetByIdAsync(new CustomerId(callerIdGuid), ct);

        if (caller == null || caller.Role != CustomerRole.SuperAdmin)
        {
            return Error.Forbidden("ACCESS_DENIED", "Only SuperAdmin callers can perform role demotions.");
        }

        var targetCustomer = await customerRepository.GetByIdAsync(new CustomerId(request.TargetCustomerId), ct);
        if (targetCustomer == null) return Error.NotFound("Customer", request.TargetCustomerId);

        var previousRole = targetCustomer.Role;
        targetCustomer.ChangeRole(CustomerRole.Customer, caller.Id);

        var auditLog = new CustomerAuditLog(
            Guid.NewGuid(),
            targetCustomer.Id,
            "RoleChanged",
            $"{{\"previousRole\":\"{previousRole}\",\"newRole\":\"Customer\"}}",
            caller.Id,
            DateTime.UtcNow);

        await customerRepository.AddAuditLogAsync(auditLog, ct);
        await customerRepository.UpdateAsync(targetCustomer, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}

public class GetAdminCustomersQueryHandler(
    ICustomerRepository customerRepository) : IRequestHandler<GetAdminCustomersQuery, Result<PagedResult<AdminCustomerDto>>>
{
    public async Task<Result<PagedResult<AdminCustomerDto>>> Handle(GetAdminCustomersQuery request, CancellationToken ct)
    {
        var (items, totalCount) = await customerRepository.GetPagedAsync(
            request.Email,
            request.Role,
            request.Status,
            request.RegisteredFrom,
            request.RegisteredTo,
            request.PageIndex,
            request.PageSize,
            ct);

        var dtos = items.Select(c => new AdminCustomerDto(
            c.Id.Value,
            c.Email,
            c.FirstName,
            c.LastName,
            c.CustomerType.ToString(),
            c.Role.ToString(),
            c.Status.ToString(),
            c.CreatedAtUtc,
            c.SuspendedAtUtc,
            c.SuspensionReason)).ToList();

        return new PagedResult<AdminCustomerDto>(dtos, totalCount, request.PageIndex, request.PageSize);
    }
}

public class GetCustomerDetailQueryHandler(
    ICustomerRepository customerRepository,
    IOrderRepository orderRepository) : IRequestHandler<GetCustomerDetailQuery, Result<CustomerDetailDto>>
{
    public async Task<Result<CustomerDetailDto>> Handle(GetCustomerDetailQuery request, CancellationToken ct)
    {
        var customer = await customerRepository.GetByIdAsync(new CustomerId(request.CustomerId), ct);
        if (customer == null) return Error.NotFound("Customer", request.CustomerId);

        var profile = new AdminCustomerDto(
            customer.Id.Value,
            customer.Email,
            customer.FirstName,
            customer.LastName,
            customer.CustomerType.ToString(),
            customer.Role.ToString(),
            customer.Status.ToString(),
            customer.CreatedAtUtc,
            customer.SuspendedAtUtc,
            customer.SuspensionReason);

        var orders = await orderRepository.GetByCustomerIdAsync(customer.Id, ct);
        var orderDtos = orders.Select(OrderDto.FromDomain).ToList();

        return new CustomerDetailDto(profile, orderDtos);
    }
}

public class GetCustomerAuditLogsQueryHandler(
    ICustomerRepository customerRepository,
    ICurrentUserService currentUserService) : IRequestHandler<GetCustomerAuditLogsQuery, Result<PagedResult<CustomerAuditLogDto>>>
{
    public async Task<Result<PagedResult<CustomerAuditLogDto>>> Handle(GetCustomerAuditLogsQuery request, CancellationToken ct)
    {
        var callerIdGuid = currentUserService.CustomerId ?? Guid.Empty;
        var caller = await customerRepository.GetByIdAsync(new CustomerId(callerIdGuid), ct);

        if (caller == null || caller.Role != CustomerRole.SuperAdmin)
        {
            return Error.Forbidden("ACCESS_DENIED", "Only SuperAdmin callers can view audit logs.");
        }

        var (items, totalCount) = await customerRepository.GetAuditLogsAsync(
            new CustomerId(request.CustomerId),
            request.PageIndex,
            request.PageSize,
            ct);

        var dtos = items.Select(a => new CustomerAuditLogDto(
            a.Id,
            a.CustomerId.Value,
            a.EventType,
            a.DetailsJson,
            a.PerformedByCustomerId.Value,
            a.TimestampUtc)).ToList();

        return new PagedResult<CustomerAuditLogDto>(dtos, totalCount, request.PageIndex, request.PageSize);
    }
}
