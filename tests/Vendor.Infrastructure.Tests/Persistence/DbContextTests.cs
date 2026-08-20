using FluentAssertions;
using Vendor.Domain.Aggregates.Customer;
using Vendor.Domain.Aggregates.Product;
using Vendor.Domain.ValueObjects;
using Vendor.Infrastructure.Persistence;
using Vendor.Infrastructure.Persistence.Repositories;
using Vendor.Infrastructure.Tests.Fixtures;

namespace Vendor.Infrastructure.Tests.Persistence;

[Collection("Database")]
public class DbContextTests : IAsyncLifetime
{
    private readonly MsSqlFixture _fixture;

    public DbContextTests(MsSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ProductRepository_AddAndGetById_SucceedsWithValueObjects()
    {
        using var context = new VendorDbContext(_fixture.DbContextOptions);
        var repository = new ProductRepository(context);

        var productId = ProductId.New();
        var product = new Product(productId, "Test Laptop", new Slug("test-laptop"), new Money(999.99m, "USD"));

        await repository.AddAsync(product);
        await context.SaveChangesAsync();

        var fetched = await repository.GetByIdAsync(productId);
        fetched.Should().NotBeNull();
        fetched!.Name.Should().Be("Test Laptop");
        fetched.BasePrice.Amount.Should().Be(999.99m);
        fetched.BasePrice.Currency.Should().Be("USD");
    }

    [Fact]
    public async Task CustomerRepository_AddAndGetByEmail_Succeeds()
    {
        using var context = new VendorDbContext(_fixture.DbContextOptions);
        var repository = new CustomerRepository(context);

        var customer = new Customer(CustomerId.New(), "user@example.com", "Jane", "Doe", CustomerType.Registered);
        await repository.AddAsync(customer);
        await context.SaveChangesAsync();

        var fetched = await repository.GetByEmailAsync("user@example.com");
        fetched.Should().NotBeNull();
        fetched!.FirstName.Should().Be("Jane");
        fetched.CustomerType.Should().Be(CustomerType.Registered);
    }

    [Fact]
    public async Task ProductRepository_AddAndGetBySlug_SucceedsWithValueConverter()
    {
        using var context = new VendorDbContext(_fixture.DbContextOptions);
        var repository = new ProductRepository(context);

        var productId = ProductId.New();
        var slug = new Slug("gaming-mouse-rgb");
        var product = new Product(productId, "Gaming Mouse RGB", slug, new Money(49.99m, "USD"));

        await repository.AddAsync(product);
        await context.SaveChangesAsync();

        var fetched = await repository.GetBySlugAsync(slug);
        fetched.Should().NotBeNull();
        fetched!.Id.Should().Be(productId);
        fetched.Slug.Should().Be(slug);
    }

    [Fact]
    public async Task ProductRepository_Search_SucceedsWithLikeFilter()
    {
        using var context = new VendorDbContext(_fixture.DbContextOptions);
        var repository = new ProductRepository(context);

        var productId = ProductId.New();
        var slug = new Slug("wireless-keyboard-mech");
        var product = new Product(productId, "Mechanical Keyboard", slug, new Money(89.99m, "USD"), category: "Hardware", categories: ["Electronics", "Hardware"], tags: ["wireless", "gaming"]);

        await repository.AddAsync(product);
        await context.SaveChangesAsync();

        var (resultsBySlug, totalBySlug) = await repository.SearchAsync("keyboard-mech", pageIndex: 0, pageSize: 10);
        resultsBySlug.Should().Contain(p => p.Id == productId);
        totalBySlug.Should().BeGreaterThanOrEqualTo(1);

        var (resultsByName, totalByName) = await repository.SearchAsync("Mechanical", pageIndex: 0, pageSize: 10);
        resultsByName.Should().Contain(p => p.Id == productId);
        totalByName.Should().BeGreaterThanOrEqualTo(1);

        var (resultsByCategory, totalByCategory) = await repository.SearchAsync(null, category: "Hardware", pageIndex: 0, pageSize: 10);
        resultsByCategory.Should().Contain(p => p.Id == productId);
        totalByCategory.Should().BeGreaterThanOrEqualTo(1);
    }
}
