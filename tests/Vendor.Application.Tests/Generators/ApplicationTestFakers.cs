using Bogus;
using Vendor.Domain.Aggregates.Cart;
using Vendor.Domain.Aggregates.Customer;
using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Aggregates.Product;
using Vendor.Domain.ValueObjects;

namespace Vendor.Application.Tests.Generators;

public static class ApplicationTestFakers
{
    static ApplicationTestFakers()
    {
        Randomizer.Seed = new Random(42);
    }

    public static Customer CreateCustomer()
    {
        var f = new Faker();
        return new Customer(
            CustomerId.New(),
            f.Internet.Email(),
            f.Name.FirstName(),
            f.Name.LastName(),
            CustomerType.Registered,
            true);
    }

    public static Product CreateProduct()
    {
        var f = new Faker();
        var name = f.Commerce.ProductName();
        var slugStr = name.ToLowerInvariant().Replace(" ", "-").Replace("'", "");
        return new Product(
            ProductId.New(),
            name,
            new Slug(slugStr),
            new Money(f.Random.Decimal(10m, 200m), "USD"));
    }

    public static Order CreateOrder()
    {
        var f = new Faker();
        var address = new Address(f.Address.StreetAddress(), f.Address.City(), f.Address.State(), f.Address.ZipCode(), "USA");
        var line = new OrderLine(OrderId.New(), ProductVariantId.New(), f.Commerce.ProductName(), "SKU-001", 1, new Money(25m, "USD"));
        return new Order(OrderId.New(), CustomerId.New(), "ORD-001", address, [line], new Money(2m, "USD"), new Money(5m, "USD"), new Money(0m, "USD"));
    }
}
