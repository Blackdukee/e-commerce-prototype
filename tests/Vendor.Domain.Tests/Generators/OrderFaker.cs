using Bogus;
using Vendor.Domain.Aggregates.Customer;
using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Aggregates.Product;
using Vendor.Domain.ValueObjects;

namespace Vendor.Domain.Tests.Generators;

public static class OrderFaker
{
    static OrderFaker()
    {
        Randomizer.Seed = new Random(42);
    }

    public static Faker<Order> Create()
    {
        return new Faker<Order>()
            .CustomInstantiator(f =>
            {
                var address = new Address(
                    f.Address.StreetAddress(),
                    f.Address.City(),
                    f.Address.State(),
                    f.Address.ZipCode(),
                    "USA");

                var line = new OrderLine(
                    OrderId.New(),
                    ProductVariantId.New(),
                    f.Commerce.ProductName(),
                    "SKU-" + f.Random.AlphaNumeric(6).ToUpperInvariant(),
                    quantity: f.Random.Number(1, 3),
                    unitPrice: new Money(f.Random.Decimal(15m, 100m), "USD"));

                return new Order(
                    OrderId.New(),
                    CustomerId.New(),
                    "ORD-" + f.Random.AlphaNumeric(8).ToUpperInvariant(),
                    address,
                    [line],
                    tax: new Money(5.00m, "USD"),
                    shippingCost: new Money(10.00m, "USD"),
                    discount: new Money(0.00m, "USD"));
            });
    }
}
