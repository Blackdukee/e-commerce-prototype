using Bogus;
using Vendor.Domain.Aggregates.Product;
using Vendor.Domain.ValueObjects;

namespace Vendor.Domain.Tests.Generators;

public static class ProductFaker
{
    static ProductFaker()
    {
        Randomizer.Seed = new Random(42);
    }

    public static Faker<Product> Create()
    {
        return new Faker<Product>()
            .CustomInstantiator(f =>
            {
                var productName = f.Commerce.ProductName();
                var slugStr = productName.ToLowerInvariant().Replace(" ", "-").Replace("'", "");
                return new Product(
                    ProductId.New(),
                    productName,
                    new Slug(slugStr),
                    new Money(f.Random.Decimal(10m, 500m), "USD"),
                    lowStockThreshold: 5,
                    description: f.Commerce.ProductDescription());
            });
    }
}
