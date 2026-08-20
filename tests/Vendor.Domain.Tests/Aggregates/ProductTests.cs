using FluentAssertions;
using Vendor.Domain.Aggregates.Product;
using Vendor.Domain.Events;
using Vendor.Domain.Exceptions;
using Vendor.Domain.ValueObjects;

namespace Vendor.Domain.Tests.Aggregates;

public class ProductTests
{
    [Fact]
    public void Product_ActivateWithZeroPrice_ThrowsException()
    {
        var product = new Product(ProductId.New(), "Test Product", new Slug("test-product"), new Money(0m, "USD"));
        product.AddImage("https://example.com/image.jpg");

        Action act = () => product.Activate();

        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*price <= 0*");
    }

    [Fact]
    public void Product_ActivateNoImages_ThrowsException()
    {
        var product = new Product(ProductId.New(), "Test Product", new Slug("test-product"), new Money(10m, "USD"));

        Action act = () => product.Activate();

        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*without at least one image*");
    }

    [Fact]
    public void Product_ActivateValid_ChangesStatusAndRaisesEvent()
    {
        var product = new Product(ProductId.New(), "Test Product", new Slug("test-product"), new Money(10m, "USD"));
        product.AddImage("https://example.com/image.jpg");

        product.Activate();

        product.Status.Should().Be(ProductStatus.Active);
        product.DomainEvents.Should().ContainSingle(e => e is ProductActivatedEvent);
    }

    [Fact]
    public void Product_AddDuplicateSkuVariant_ThrowsException()
    {
        var productId = ProductId.New();
        var product = new Product(productId, "Test Product", new Slug("test-product"), new Money(10m, "USD"));
        var weight = new Weight(1m, WeightUnit.Kg);
        var dimensions = new Dimensions(10m, 10m, 10m, DimensionUnit.Cm);

        var v1 = new ProductVariant(ProductVariantId.New(), productId, "SKU-001", Money.Zero("USD"), 10, weight, dimensions);
        var v2 = new ProductVariant(ProductVariantId.New(), productId, "sku-001", Money.Zero("USD"), 5, weight, dimensions);

        product.AddVariant(v1);
        Action act = () => product.AddVariant(v2);

        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public void Product_DeductStockBelowThreshold_RaisesProductLowStockEvent()
    {
        var productId = ProductId.New();
        var product = new Product(productId, "Test Product", new Slug("test-product"), new Money(10m, "USD"), lowStockThreshold: 3);
        var weight = new Weight(1m, WeightUnit.Kg);
        var dimensions = new Dimensions(10m, 10m, 10m, DimensionUnit.Cm);
        var variantId = ProductVariantId.New();
        var variant = new ProductVariant(variantId, productId, "SKU-001", Money.Zero("USD"), 5, weight, dimensions);
        product.AddVariant(variant);

        product.DeductStock(variantId, 3); // Stock becomes 2 (< 3)

        product.DomainEvents.Should().ContainSingle(e => e is ProductLowStockEvent)
            .Which.As<ProductLowStockEvent>().CurrentStock.Should().Be(2);
    }

    [Fact]
    public void Product_WithCategoriesAndTags_PopulatesCollectionsCorrectly()
    {
        var productId = ProductId.New();
        var product = new Product(
            productId,
            "Pro Laptop",
            new Slug("pro-laptop"),
            new Money(1500m, "USD"),
            description: "High performance",
            category: "Computers",
            categories: ["Electronics", "Computers", "Laptops"],
            tags: ["portable", "fast", "pro"]);

        product.Category.Should().Be("Computers");
        product.Categories.Should().ContainInOrder("Electronics", "Computers", "Laptops");
        product.Tags.Should().ContainInOrder("portable", "fast", "pro");

        product.UpdateDetails(
            "Pro Laptop 2",
            new Slug("pro-laptop-2"),
            new Money(1600m, "USD"),
            category: "Ultrabooks",
            categories: ["Laptops", "Ultrabooks"],
            tags: ["lightweight"]);

        product.Category.Should().Be("Ultrabooks");
        product.Categories.Should().ContainInOrder("Laptops", "Ultrabooks");
        product.Tags.Should().ContainSingle().Which.Should().Be("lightweight");
    }
}
