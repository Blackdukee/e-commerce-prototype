using MediatR;
using Vendor.Application.Common.Messaging;
using Vendor.Application.Common.Results;
using Vendor.Application.Modules.Auth;
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
