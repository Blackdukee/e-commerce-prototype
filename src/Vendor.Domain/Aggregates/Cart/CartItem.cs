using Vendor.Domain.Aggregates.Product;
using Vendor.Domain.Exceptions;
using Vendor.Domain.ValueObjects;

namespace Vendor.Domain.Aggregates.Cart;

public class CartItem
{
    public CartId CartId { get; private set; }
    public ProductVariantId ProductVariantId { get; private set; }
    public int Quantity { get; private set; }
    public Money UnitPrice { get; private set; }

    public Money Subtotal => UnitPrice * Quantity;

    private CartItem()
    {
    }

    public CartItem(CartId cartId, ProductVariantId productVariantId, int quantity, Money unitPrice)
    {
        if (quantity <= 0)
        {
            throw new BusinessRuleViolationException("Cart item quantity must be greater than zero.", nameof(CartItem));
        }

        CartId = cartId;
        ProductVariantId = productVariantId;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    public void UpdateQuantity(int newQuantity)
    {
        if (newQuantity <= 0)
        {
            throw new BusinessRuleViolationException("Quantity must be greater than zero.", nameof(CartItem));
        }

        Quantity = newQuantity;
    }
}
