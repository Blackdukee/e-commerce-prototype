using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Vendor.Api.DTOs;
using Vendor.Api.Tests.Helpers;
using AppProductDto = Vendor.Application.Modules.Products.ProductDto;
using AppVariantDto = Vendor.Application.Modules.Products.ProductVariantDto;
using AppCartDto = Vendor.Application.Modules.Cart.CartDto;

namespace Vendor.Api.Tests.Integration;

public class CartEndpointsTests : IClassFixture<VendorApiFactory>
{
    private readonly VendorApiFactory _factory;

    public CartEndpointsTests(VendorApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetCart_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/cart");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Checkout_ValidPayload_ReturnsCreated()
    {
        var client = _factory.CreateClient();
        client.WithAdminBearerToken();

        // 1. Create product & variant
        var createProductReq = new CreateProductRequest("Checkout Product", "checkout-product", "Desc", 50m, "USD", [], [], []);
        var createProductRes = await client.PostAsJsonAsync("/api/v1/products", createProductReq);
        var product = await createProductRes.Content.ReadFromJsonAsync<AppProductDto>();

        var addVariantReq = new CreateVariantRequest("CHK-SKU-1", 0m, "USD", 10, 1m, "Kg", 10, 10, 10, "Cm");
        var addVariantRes = await client.PostAsJsonAsync($"/api/v1/admin/products/{product!.Id}/variants", addVariantReq);
        var variant = await addVariantRes.Content.ReadFromJsonAsync<AppVariantDto>();

        // Add image
        var addImgRes = await client.PostAsJsonAsync($"/api/v1/admin/products/{product!.Id}/images", new AddProductImageRequest("https://example.com/image.jpg"));
        addImgRes.IsSuccessStatusCode.Should().BeTrue();

        // Activate product
        var activateReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/products/{product!.Id}/activate");
        activateReq.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var activateRes = await client.SendAsync(activateReq);
        activateRes.IsSuccessStatusCode.Should().BeTrue(await activateRes.Content.ReadAsStringAsync());

        // 2. Create cart and item
        client.WithCustomerBearerToken();
        var cartItemReq = new AddCartItemRequest(variant!.Id, 2);
        var addCartRes = await client.PostAsJsonAsync("/api/v1/cart/items", cartItemReq);
        addCartRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var cartDto = await addCartRes.Content.ReadFromJsonAsync<AppCartDto>();

        // 3. Checkout cart
        var checkoutPayload = new CheckoutRequest(
            new AddressDto("123 Main St", "NYC", "NY", "10001", "US"),
            "STANDARD",
            "stripe",
            cartDto!.Id);

        var checkoutReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders/checkout")
        {
            Content = JsonContent.Create(checkoutPayload)
        };
        checkoutReq.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var checkoutRes = await client.SendAsync(checkoutReq);
        checkoutRes.StatusCode.Should().Be(HttpStatusCode.Created, await checkoutRes.Content.ReadAsStringAsync());
    }
}
