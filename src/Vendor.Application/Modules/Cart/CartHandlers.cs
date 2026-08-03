using MediatR;
using Vendor.Application.Common.Messaging;
using Vendor.Application.Common.Results;
using Vendor.Application.Interfaces;
using Vendor.Domain.Aggregates.Cart;
using Vendor.Domain.Aggregates.Customer;
using Vendor.Domain.Aggregates.Product;
using Vendor.Domain.Interfaces.Repositories;
using Vendor.Domain.ValueObjects;

namespace Vendor.Application.Modules.Cart;

public record CartItemDto(Guid VariantId, int Quantity, decimal UnitPrice, decimal LineTotal);
public record CartDto(Guid Id, Guid? CustomerId, string? SessionId, string Status, string? DiscountCode, decimal Total, IReadOnlyList<CartItemDto> Items)
{
    public static CartDto FromDomain(Domain.Aggregates.Cart.Cart cart) => new(
        cart.Id.Value,
        cart.CustomerId?.Value,
        cart.SessionId,
        cart.Status.ToString(),
        cart.DiscountCode,
        cart.Items.Sum(i => i.Subtotal.Amount),
        cart.Items.Select(i => new CartItemDto(i.ProductVariantId.Value, i.Quantity, i.UnitPrice.Amount, i.Subtotal.Amount)).ToList());
}

public record CreateCartCommand(Guid? CustomerId, string? SessionId) : ICommand<Result<CartDto>>;
public record AddCartItemCommand(Guid CartId, Guid VariantId, int Quantity, decimal UnitPrice, string Currency) : ICommand<Result<CartDto>>;
public record UpdateCartItemQuantityCommand(Guid CartId, Guid VariantId, int Quantity) : ICommand<Result<CartDto>>;
public record RemoveCartItemCommand(Guid CartId, Guid VariantId) : ICommand<Result<CartDto>>, IIdempotentRequest<Result<CartDto>>
{
    public string IdempotencyKey => $"REM-CART-ITEM-{CartId}-{VariantId}";
}
public record ApplyCartDiscountCodeCommand(Guid CartId, string DiscountCode) : ICommand<Result<CartDto>>, IIdempotentRequest<Result<CartDto>>
{
    public string IdempotencyKey => $"APPLY-DISC-{CartId}-{DiscountCode}";
}
public record RemoveCartDiscountCodeCommand(Guid CartId) : ICommand<Result<CartDto>>, IIdempotentRequest<Result<CartDto>>
{
    public string IdempotencyKey => $"REM-DISC-{CartId}";
}
public record ClearCartCommand(Guid CartId) : ICommand<Result>, IIdempotentRequest<Result>
{
    public string IdempotencyKey => $"CLEAR-CART-{CartId}";
}
public record MergeGuestCartCommand(Guid GuestCartId, Guid CustomerCartId) : ICommand<Result<CartDto>>, IIdempotentRequest<Result<CartDto>>
{
    public string IdempotencyKey => $"MERGE-CART-{GuestCartId}-{CustomerCartId}";
}
public record ProcessCartAbandonmentCommand(int TimeoutHours) : ICommand<Result<int>>, IIdempotentRequest<Result<int>>
{
    public string IdempotencyKey => $"ABANDON-RUN-{DateTime.UtcNow:yyyyMMddHH}";
}

public record GetCartByIdQuery(Guid CartId) : IQuery<Result<CartDto>>;
public record GetCartByCustomerIdQuery(Guid CustomerId) : IQuery<Result<CartDto>>;
public record GetCartBySessionIdQuery(string SessionId) : IQuery<Result<CartDto>>;

public class CreateCartCommandHandler(ICartRepository cartRepository) : IRequestHandler<CreateCartCommand, Result<CartDto>>
{
    public async Task<Result<CartDto>> Handle(CreateCartCommand request, CancellationToken ct)
    {
        CustomerId? custId = request.CustomerId.HasValue ? new CustomerId(request.CustomerId.Value) : null;
        var cart = new Domain.Aggregates.Cart.Cart(CartId.New(), custId, request.SessionId);
        await cartRepository.AddAsync(cart, ct);
        return CartDto.FromDomain(cart);
    }
}

public class GetCartByIdQueryHandler(ICartRepository cartRepository) : IRequestHandler<GetCartByIdQuery, Result<CartDto>>
{
    public async Task<Result<CartDto>> Handle(GetCartByIdQuery request, CancellationToken ct)
    {
        var cart = await cartRepository.GetByIdAsync(new CartId(request.CartId), ct);
        if (cart == null) return Error.NotFound("Cart", request.CartId);
        return CartDto.FromDomain(cart);
    }
}

public class AddCartItemCommandHandler(ICartRepository cartRepository, IProductRepository productRepository, IUnitOfWork unitOfWork) : IRequestHandler<AddCartItemCommand, Result<CartDto>>
{
    public async Task<Result<CartDto>> Handle(AddCartItemCommand request, CancellationToken ct)
    {
        var cart = await cartRepository.GetByIdAsync(new CartId(request.CartId), ct);
        var isNew = false;
        if (cart == null)
        {
            cart = new Domain.Aggregates.Cart.Cart(new CartId(request.CartId), null, "guest-session");
            isNew = true;
        }

        var product = await productRepository.GetByVariantIdAsync(new ProductVariantId(request.VariantId), ct);
        if (product == null) return Error.NotFound("ProductVariant", request.VariantId);

        var variant = product.Variants.FirstOrDefault(v => v.Id.Value == request.VariantId);
        if (variant == null) return Error.NotFound("ProductVariant", request.VariantId);

        var unitPrice = product.BasePrice.Amount + variant.PriceAdjustment.Amount;
        var currency = product.BasePrice.Currency;

        cart.AddItem(new CartItem(cart.Id, variant.Id, request.Quantity, new Money(unitPrice, currency)));

        if (isNew)
        {
            await cartRepository.AddAsync(cart, ct);
        }
        else
        {
            await cartRepository.UpdateAsync(cart, ct);
        }

        await unitOfWork.SaveChangesAsync(ct);

        return CartDto.FromDomain(cart);
    }
}

public class UpdateCartItemQuantityCommandHandler(ICartRepository cartRepository, IUnitOfWork unitOfWork) : IRequestHandler<UpdateCartItemQuantityCommand, Result<CartDto>>
{
    public async Task<Result<CartDto>> Handle(UpdateCartItemQuantityCommand request, CancellationToken ct)
    {
        var cart = await cartRepository.GetByIdAsync(new CartId(request.CartId), ct);
        if (cart == null) return Error.NotFound("Cart", request.CartId);

        var item = cart.Items.FirstOrDefault(i => i.ProductVariantId.Value == request.VariantId);
        if (item == null) return Error.NotFound("CartItem", request.VariantId);

        item.UpdateQuantity(request.Quantity);
        await cartRepository.UpdateAsync(cart, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return CartDto.FromDomain(cart);
    }
}

public class RemoveCartItemCommandHandler(ICartRepository cartRepository, IUnitOfWork unitOfWork) : IRequestHandler<RemoveCartItemCommand, Result<CartDto>>
{
    public async Task<Result<CartDto>> Handle(RemoveCartItemCommand request, CancellationToken ct)
    {
        var cart = await cartRepository.GetByIdAsync(new CartId(request.CartId), ct);
        if (cart == null) return Error.NotFound("Cart", request.CartId);

        cart.RemoveItem(new ProductVariantId(request.VariantId));
        await cartRepository.UpdateAsync(cart, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return CartDto.FromDomain(cart);
    }
}
