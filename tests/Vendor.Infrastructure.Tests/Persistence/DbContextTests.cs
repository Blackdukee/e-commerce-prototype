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
}
