using FluentAssertions;
using NSubstitute;
using Vendor.Application.Interfaces;
using Vendor.Application.Modules.Auth;
using Vendor.Application.Modules.Products;
using Vendor.Application.Modules.Promotions;
using Vendor.Domain.Aggregates.Customer;
using Vendor.Domain.Aggregates.Product;
using Vendor.Domain.Aggregates.Promotion;
using Vendor.Domain.Interfaces.Repositories;
using Vendor.Domain.ValueObjects;

namespace Vendor.Application.Tests.Modules;

public class ModuleHandlersTests
{
    [Fact]
    public async Task RegisterCustomer_UniqueEmail_ReturnsAuthResponse()
    {
        var repo = Substitute.For<ICustomerRepository>();
        var tokenService = Substitute.For<ITokenService>();
        var identityAuth = Substitute.For<IIdentityAuthService>();

        var customerId = Guid.NewGuid();
        identityAuth.RegisterAsync("test@example.com", "Secret123!", "John", "Doe", Arg.Any<CancellationToken>())
            .Returns(new IdentityRegisterResult(true, Guid.NewGuid(), customerId, null, null));

        tokenService.GenerateTokens(customerId, "test@example.com", Arg.Any<IEnumerable<string>>())
            .Returns(new TokenResult("ACCESS", "REFRESH", DateTime.UtcNow.AddHours(1)));

        var handler = new RegisterCustomerCommandHandler(identityAuth, repo, tokenService);
        var command = new RegisterCustomerCommand("test@example.com", "Secret123!", "John", "Doe");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("ACCESS");
    }

    [Fact]
    public async Task CreateProduct_UniqueSlug_ReturnsProductDto()
    {
        var repo = Substitute.For<IProductRepository>();
        repo.GetBySlugAsync(Arg.Any<Slug>(), Arg.Any<CancellationToken>()).Returns((Product?)null);

        var unitOfWork = Substitute.For<IUnitOfWork>();
        var handler = new CreateProductCommandHandler(repo, unitOfWork);
        var command = new CreateProductCommand("Widget", "widget", 19.99m, "USD");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Widget");
        result.Value.Slug.Should().Be("widget");
    }

    [Fact]
    public async Task CreatePromotion_DuplicateCode_ReturnsConflictError()
    {
        var repo = Substitute.For<IPromotionRepository>();
        var validity = new DateRange(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        var existingPromo = new Promotion(PromotionId.New(), "SAVE10", DiscountType.Percentage, 10m, validity);
        repo.GetByCodeAsync("SAVE10", Arg.Any<CancellationToken>()).Returns(existingPromo);

        var handler = new CreatePromotionCommandHandler(repo);
        var command = new CreatePromotionCommand("SAVE10", "Percentage", 10m, DateTime.UtcNow, DateTime.UtcNow.AddDays(1));

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(Vendor.Application.Common.Results.ErrorType.Conflict);
    }
}
