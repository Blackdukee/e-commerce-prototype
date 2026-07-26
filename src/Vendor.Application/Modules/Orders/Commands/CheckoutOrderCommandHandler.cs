using MediatR;
using Vendor.Application.Common.Results;
using Vendor.Application.Interfaces;
using Vendor.Application.Modules.Orders.Dtos;
using Vendor.Domain.Aggregates.Cart;
using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Aggregates.Payment;
using Vendor.Domain.Aggregates.Product;
using Vendor.Domain.Aggregates.Promotion;
using Vendor.Domain.Interfaces.Adapters;
using Vendor.Domain.Interfaces.Repositories;
using Vendor.Domain.ValueObjects;

namespace Vendor.Application.Modules.Orders.Commands;

public class CheckoutOrderCommandHandler(
    ICartRepository cartRepository,
    IProductRepository productRepository,
    IPromotionRepository promotionRepository,
    IOrderRepository orderRepository,
    IPaymentRepository paymentRepository,
    ITaxCalculator taxCalculator,
    IPaymentGateway paymentGateway,
    IUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<CheckoutOrderCommand, Result<OrderDto>>
{
    public async Task<Result<OrderDto>> Handle(CheckoutOrderCommand request, CancellationToken ct)
    {
        // 1. Validate Cart
        var cart = await cartRepository.GetByIdAsync(new CartId(request.CartId), ct);
        if (cart == null)
        {
            return Error.NotFound("Cart", request.CartId);
        }

        if (cart.Items.Count == 0)
        {
            return Error.Failure("Cart.Empty", "Cannot checkout an empty cart.");
        }

        // 2. Verify Stock & Build Order Lines
        var currency = cart.Items.First().UnitPrice.Currency;
        var orderLines = new List<OrderLine>();
        var productsToUpdate = new List<Product>();

        foreach (var cartItem in cart.Items)
        {
            var product = await productRepository.GetByIdAsync(new ProductId(cartItem.ProductVariantId.Value), ct);
            if (product == null)
            {
                // Fallback: check if product exists by variant
                return Error.NotFound("ProductVariant", cartItem.ProductVariantId);
            }

            var variant = product.Variants.FirstOrDefault(v => v.Id == cartItem.ProductVariantId);
            if (variant == null)
            {
                return Error.NotFound("ProductVariant", cartItem.ProductVariantId);
            }

            if (variant.StockQuantity < cartItem.Quantity)
            {
                return Error.Failure("Stock.Insufficient", $"Insufficient stock for SKU '{variant.Sku}'. Available: {variant.StockQuantity}, requested: {cartItem.Quantity}.");
            }

            product.DeductStock(variant.Id, cartItem.Quantity);
            productsToUpdate.Add(product);

            var line = new OrderLine(
                OrderId.Empty,
                variant.Id,
                product.Name,
                variant.Sku,
                cartItem.Quantity,
                cartItem.UnitPrice);
            orderLines.Add(line);
        }

        // 3. Evaluate Discount Code
        var discountAmount = Money.Zero(currency);
        Promotion? appliedPromotion = null;

        if (!string.IsNullOrWhiteSpace(cart.DiscountCode))
        {
            appliedPromotion = await promotionRepository.GetByCodeAsync(cart.DiscountCode, ct);
            if (appliedPromotion != null && appliedPromotion.IsActive)
            {
                var subtotalAmount = orderLines.Sum(l => l.LineTotal.Amount);
                var subtotalMoney = new Money(subtotalAmount, currency);
                discountAmount = appliedPromotion.CalculateDiscount(subtotalMoney, dateTimeProvider.UtcNow);
                appliedPromotion.RecordUsage();
            }
        }

        // 4. Calculate Tax
        var shippingAddress = request.ShippingAddress.ToDomain();
        var taxAmount = await taxCalculator.CalculateTaxAsync(orderLines, shippingAddress, currency, ct);
        var shippingCost = new Money(5.00m, currency); // Fixed default shipping

        // 5 & 6. Instantiate Order & Payment
        var customerId = cart.CustomerId ?? Domain.Aggregates.Customer.CustomerId.New();
        var orderNumber = $"ORD-{dateTimeProvider.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpperInvariant()}";
        var orderId = OrderId.New();

        // Re-link order lines to actual order ID
        var finalLines = orderLines.Select(l => new OrderLine(
            orderId,
            l.ProductVariantId,
            l.ProductName,
            l.Sku,
            l.Quantity,
            l.UnitPrice)).ToList();

        var order = new Order(
            orderId,
            customerId,
            orderNumber,
            shippingAddress,
            finalLines,
            taxAmount,
            shippingCost,
            discountAmount);

        var payment = new Payment(
            PaymentId.New(),
            order.Id,
            order.Total,
            request.IdempotencyKey);

        // 7 & 8. Update Product Stock, Promotion Usage, Clear Cart
        foreach (var p in productsToUpdate)
        {
            await productRepository.UpdateAsync(p, ct);
        }

        if (appliedPromotion != null)
        {
            await promotionRepository.UpdateAsync(appliedPromotion, ct);
        }

        cart.MarkConvertedToOrder();
        await cartRepository.UpdateAsync(cart, ct);
        await orderRepository.AddAsync(order, ct);
        await paymentRepository.AddAsync(payment, ct);

        // 9. Commit Local Database Transaction
        await unitOfWork.SaveChangesAsync(ct);

        // 10. Post-Commit Payment Gateway Authorization
        var authResult = await paymentGateway.AuthorizeAsync(order.Total, request.IdempotencyKey, ct);
        if (authResult.Success)
        {
            payment.Capture(authResult.AuthorizationToken, dateTimeProvider.UtcNow);
            order.ConfirmPayment();
        }
        else
        {
            payment.Fail(authResult.ErrorMessage ?? "Payment authorization failed.");
            order.Cancel(authResult.ErrorMessage);
        }

        await paymentRepository.UpdateAsync(payment, ct);
        await orderRepository.UpdateAsync(order, ct);
        await unitOfWork.SaveChangesAsync(ct);

        if (!authResult.Success)
        {
            return Error.Failure("Payment.Failed", authResult.ErrorMessage ?? "Payment failed during checkout.");
        }

        return OrderDto.FromDomain(order);
    }
}
