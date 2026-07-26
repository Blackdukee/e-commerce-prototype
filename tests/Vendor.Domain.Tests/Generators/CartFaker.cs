using Bogus;
using Vendor.Domain.Aggregates.Cart;
using Vendor.Domain.Aggregates.Customer;
using Vendor.Domain.Aggregates.Product;
using Vendor.Domain.ValueObjects;

namespace Vendor.Domain.Tests.Generators;

public static class CartFaker
{
    static CartFaker()
    {
        Randomizer.Seed = new Random(42);
    }

    public static Faker<Cart> Create()
    {
        return new Faker<Cart>()
            .CustomInstantiator(f =>
            {
                var cartId = CartId.New();
                var cart = new Cart(cartId, CustomerId.New(), null);
                var item = new CartItem(
                    cartId,
                    ProductVariantId.New(),
                    quantity: f.Random.Number(1, 4),
                    unitPrice: new Money(f.Random.Decimal(10m, 80m), "USD"));
                cart.AddItem(item);
                return cart;
            });
    }
}
