using MediatR;
using Vendor.Application.Common.Results;
using Vendor.Application.Interfaces;
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

namespace Vendor.Application.Modules.Orders.Commands;

public class CheckoutOrderCommandHandler(
    ICartRepository cartRepository,
    ICustomerRepository customerRepository,
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

        // Check Customer Suspension Status if customer ID is present on cart
        if (cart.CustomerId != null)
        {
            var customer = await customerRepository.GetByIdAsync(cart.CustomerId.Value, ct);
            if (customer != null && customer.Status == CustomerStatus.Suspended)
            {
                return Error.Forbidden("ACCOUNT_SUSPENDED", "Customer account is suspended.");
            }
        }

        // 2. Verify Stock & Build Order Lines
        var orderId = OrderId.New();
        var currency = cart.Items.First().UnitPrice.Currency;
        var orderLines = new List<OrderLine>();

        foreach (var cartItem in cart.Items)
        {
            var product = await productRepository.GetByVariantIdAsync(cartItem.ProductVariantId, ct);
            if (product == null)
            {
                return Error.NotFound("ProductVariant", cartItem.ProductVariantId);
            }

            var variant = product.Variants.FirstOrDefault(v => v.Id == cartItem.ProductVariantId);
            if (variant == null)
            {
                return Error.NotFound("ProductVariant", cartItem.ProductVariantId);
            }

            if (variant.StockQuantity < cartItem.Quantity)
            {
                return Error.Failure("Stock.Insufficient", $"Insufficient stock for SKU '{variant.Sku}'. Requested: {cartItem.Quantity}, Available: {variant.StockQuantity}");
            }

            product.DeductStock(cartItem.ProductVariantId, cartItem.Quantity);

            orderLines.Add(new OrderLine(
                orderId,
                cartItem.ProductVariantId,
                product.Name,
                variant.Sku,
                cartItem.Quantity,
                cartItem.UnitPrice));
        }

        // 3. Tax Calculation
        var taxAmount = await taxCalculator.CalculateTaxAsync(orderLines, request.ShippingAddress.ToDomain(), currency, ct);
        var subtotalAmount = orderLines.Sum(l => l.LineTotal.Amount);
        var subtotal = new Money(subtotalAmount, currency);

        // 4. Discount Application
        var discountAmount = Money.Zero(currency);
        if (!string.IsNullOrWhiteSpace(cart.DiscountCode))
        {
            var promotion = await promotionRepository.GetByCodeAsync(cart.DiscountCode, ct);
            var now = dateTimeProvider.UtcNow;
            if (promotion != null && promotion.IsValidAt(now, subtotal))
            {
                discountAmount = promotion.CalculateDiscount(subtotal, now);
                promotion.RecordUsage();
                await promotionRepository.UpdateAsync(promotion, ct);
            }
        }

        // 5. Create Order
        var orderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
        var customerId = cart.CustomerId ?? CustomerId.New();
        var shippingCost = Money.Zero(currency);

        var order = new Order(
            orderId,
            customerId,
            orderNumber,
            request.ShippingAddress.ToDomain(),
            orderLines,
            taxAmount,
            shippingCost,
            discountAmount);

        await orderRepository.AddAsync(order, ct);

        // 6. Process Payment
        var authResult = await paymentGateway.AuthorizeAsync(order.Total, request.IdempotencyKey, ct);
        var payment = new Payment(
            PaymentId.New(),
            orderId,
            order.Total,
            request.IdempotencyKey);

        if (authResult.Success)
        {
            var captureResult = await paymentGateway.CaptureAsync(authResult.AuthorizationToken, request.IdempotencyKey, ct);
            if (captureResult.Success)
            {
                payment.Capture(captureResult.GatewayTransactionId);
                order.ConfirmPayment();
            }
            else
            {
                payment.Fail(captureResult.ErrorMessage ?? "Payment capture failed.");
            }
        }
        else
        {
            payment.Fail(authResult.ErrorMessage ?? "Payment authorization failed.");
        }

        await paymentRepository.AddAsync(payment, ct);

        // 7. Update Cart Status
        cart.MarkConvertedToOrder();
        await cartRepository.UpdateAsync(cart, ct);

        // 8. Commit Unit of Work
        await unitOfWork.SaveChangesAsync(ct);

        return OrderDto.FromDomain(order);
    }
}
