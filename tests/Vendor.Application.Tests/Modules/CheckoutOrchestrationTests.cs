using FluentAssertions;
using NSubstitute;
using Vendor.Application.Interfaces;
using Vendor.Application.Modules.Orders.Commands;
using Vendor.Application.Modules.Orders.Dtos;
using Vendor.Domain.Aggregates.Cart;
using Vendor.Domain.Aggregates.Customer;
using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Aggregates.Payment;
using Vendor.Domain.Aggregates.Product;
using Vendor.Domain.Aggregates.Promotion;
using Vendor.Domain.Interfaces.Adapters;
using Vendor.Domain.Interfaces.Repositories;
using Vendor.Domain.ValueObjects;

namespace Vendor.Application.Tests.Modules;

public class CheckoutOrchestrationTests
{
    private readonly ICartRepository _cartRepository = Substitute.For<ICartRepository>();
    private readonly IProductRepository _productRepository = Substitute.For<IProductRepository>();
    private readonly IPromotionRepository _promotionRepository = Substitute.For<IPromotionRepository>();
    private readonly IOrderRepository _orderRepository = Substitute.For<IOrderRepository>();
    private readonly IPaymentRepository _paymentRepository = Substitute.For<IPaymentRepository>();
    private readonly ITaxCalculator _taxCalculator = Substitute.For<ITaxCalculator>();
    private readonly IPaymentGateway _paymentGateway = Substitute.For<IPaymentGateway>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    [Fact]
    public async Task CheckoutOrderCommand_EmptyCart_ReturnsFailureResult()
    {
        var cartId = Guid.NewGuid();
        var cart = new Cart(new CartId(cartId), CustomerId.New());
        _cartRepository.GetByIdAsync(new CartId(cartId), Arg.Any<CancellationToken>()).Returns(cart);

        var handler = new CheckoutOrderCommandHandler(
            _cartRepository, _productRepository, _promotionRepository,
            _orderRepository, _paymentRepository, _taxCalculator,
            _paymentGateway, _unitOfWork, _dateTimeProvider);

        var command = new CheckoutOrderCommand(
            cartId,
            new AddressDto("123 Main St", "City", "ST", "12345", "US"),
            "IDEMP-001");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Cart.Empty");
    }

    [Fact]
    public async Task CheckoutOrderCommand_ValidCart_ExecutesTwoPhaseCheckoutSuccessfully()
    {
        var cartId = Guid.NewGuid();
        var customerId = CustomerId.New();
        var cart = new Cart(new CartId(cartId), customerId);
        var variantId = ProductVariantId.New();
        var productId = new ProductId(variantId.Value);

        cart.AddItem(new CartItem(cart.Id, variantId, 2, new Money(50m, "USD")));
        _cartRepository.GetByIdAsync(new CartId(cartId), Arg.Any<CancellationToken>()).Returns(cart);

        var weight = new Weight(1m, WeightUnit.Kg);
        var dimensions = new Dimensions(10m, 10m, 10m, DimensionUnit.Cm);
        var product = new Product(productId, "Test Product", new Slug("test-product"), new Money(50m, "USD"));
        var variant = new ProductVariant(variantId, productId, "SKU-001", Money.Zero("USD"), 10, weight, dimensions);
        product.AddVariant(variant);

        _productRepository.GetByIdAsync(productId, Arg.Any<CancellationToken>()).Returns(product);
        _taxCalculator.CalculateTaxAsync(Arg.Any<IReadOnlyList<OrderLine>>(), Arg.Any<Address>(), "USD", Arg.Any<CancellationToken>())
            .Returns(new Money(5m, "USD"));
        _dateTimeProvider.UtcNow.Returns(new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc));
        _paymentGateway.AuthorizeAsync(Arg.Any<Money>(), "IDEMP-001", Arg.Any<CancellationToken>())
            .Returns(new PaymentAuthorizationResult(true, "AUTH-TOKEN-123", null));

        var handler = new CheckoutOrderCommandHandler(
            _cartRepository, _productRepository, _promotionRepository,
            _orderRepository, _paymentRepository, _taxCalculator,
            _paymentGateway, _unitOfWork, _dateTimeProvider);

        var command = new CheckoutOrderCommand(
            cartId,
            new AddressDto("123 Main St", "City", "ST", "12345", "US"),
            "IDEMP-001");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Confirmed");
        await _unitOfWork.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _orderRepository.Received(1).AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
    }
}
