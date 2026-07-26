using FluentAssertions;
using NSubstitute;
using Vendor.Application.Interfaces;
using Vendor.Application.Modules.Auth;
using Vendor.Application.Modules.Customers;
using Vendor.Application.Modules.Customers.Commands;
using Vendor.Domain.Aggregates.Customer;
using Vendor.Domain.Interfaces.Repositories;
using Xunit;

namespace Vendor.Application.Tests.Handlers;

public class CustomerAccountManagementHandlerTests
{
    private readonly ICustomerRepository _customerRepository = Substitute.For<ICustomerRepository>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task SuspendCustomerHandler_ValidCustomer_RevokesTokensAndSuspends()
    {
        var targetId = CustomerId.New();
        var targetCustomer = new Customer(targetId, "target@example.com", "Target", "User");
        var adminId = Guid.NewGuid();

        _customerRepository.GetByIdAsync(targetId, Arg.Any<CancellationToken>()).Returns(targetCustomer);
        _currentUserService.CustomerId.Returns(adminId);

        var handler = new SuspendCustomerCommandHandler(_customerRepository, _tokenService, _currentUserService, _unitOfWork);
        var result = await handler.Handle(new SuspendCustomerCommand(targetId.Value, "Violation"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        targetCustomer.Status.Should().Be(CustomerStatus.Suspended);
        await _tokenService.Received(1).RevokeAllTokensForUserAsync(targetId.Value, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PromoteCustomerHandler_NonSuperAdminCaller_ReturnsForbidden()
    {
        var callerId = CustomerId.New();
        var caller = new Customer(callerId, "admin@example.com", "Admin", "User", CustomerType.Registered, false, CustomerRole.Admin);
        var targetId = CustomerId.New();

        _currentUserService.CustomerId.Returns(callerId.Value);
        _customerRepository.GetByIdAsync(callerId, Arg.Any<CancellationToken>()).Returns(caller);

        var handler = new PromoteCustomerCommandHandler(_customerRepository, _currentUserService, _unitOfWork);
        var result = await handler.Handle(new PromoteCustomerCommand(targetId.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ACCESS_DENIED");
    }

    [Fact]
    public async Task PromoteCustomerHandler_SuperAdminCaller_PromotesTargetToAdmin()
    {
        var superAdminId = CustomerId.New();
        var superAdmin = new Customer(superAdminId, "super@example.com", "Super", "Admin", CustomerType.Registered, false, CustomerRole.SuperAdmin);
        var targetId = CustomerId.New();
        var targetCustomer = new Customer(targetId, "target@example.com", "Target", "User");

        _currentUserService.CustomerId.Returns(superAdminId.Value);
        _customerRepository.GetByIdAsync(superAdminId, Arg.Any<CancellationToken>()).Returns(superAdmin);
        _customerRepository.GetByIdAsync(targetId, Arg.Any<CancellationToken>()).Returns(targetCustomer);

        var handler = new PromoteCustomerCommandHandler(_customerRepository, _currentUserService, _unitOfWork);
        var result = await handler.Handle(new PromoteCustomerCommand(targetId.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        targetCustomer.Role.Should().Be(CustomerRole.Admin);
    }

    [Fact]
    public async Task LoginHandler_SuspendedCustomer_ReturnsAccountSuspendedError()
    {
        var customerId = CustomerId.New();
        var customer = new Customer(customerId, "suspended@example.com", "User", "Test", CustomerType.Registered, false, CustomerRole.Customer, CustomerStatus.Suspended);

        _customerRepository.GetByEmailAsync("suspended@example.com", Arg.Any<CancellationToken>()).Returns(customer);

        var handler = new LoginWithPasswordCommandHandler(_customerRepository, _tokenService);
        var result = await handler.Handle(new LoginWithPasswordCommand("suspended@example.com", "password123"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ACCOUNT_SUSPENDED");
    }
}
