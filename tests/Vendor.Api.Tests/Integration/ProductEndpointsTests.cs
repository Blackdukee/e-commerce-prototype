using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Vendor.Api.DTOs;
using Vendor.Api.Tests.Helpers;

namespace Vendor.Api.Tests.Integration;

public class ProductEndpointsTests : IClassFixture<VendorApiFactory>
{
    private readonly VendorApiFactory _factory;

    public ProductEndpointsTests(VendorApiFactory factory)
    {
        _factory = factory;
    }

    private static CreateProductRequest SampleCreateRequest => new(
        "New Item",
        "new-item",
        "Description",
        99.99m,
        "USD",
        ["electronics"],
        ["gadgets"],
        ["https://example.com/item.jpg"]);

    [Fact]
    public async Task GetProducts_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/products");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetProductBySlug_ReturnsOk()
    {
        var client = _factory.CreateClient();
        client.WithAdminBearerToken();
        var request = new CreateProductRequest("Slug Test Item", "slug-test-item", "Description", 10m, "USD", [], [], []);
        await client.PostAsJsonAsync("/api/v1/products", request);

        var response = await client.GetAsync("/api/v1/products/slug/slug-test-item");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateProduct_WithoutAuth_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/products", SampleCreateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateProduct_WithCustomerRole_ReturnsForbidden()
    {
        var client = _factory.CreateClient();
        client.WithCustomerBearerToken();

        var response = await client.PostAsJsonAsync("/api/v1/products", SampleCreateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateProduct_WithAdminRole_ReturnsCreated()
    {
        var client = _factory.CreateClient();
        client.WithAdminBearerToken();

        var response = await client.PostAsJsonAsync("/api/v1/products", SampleCreateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
