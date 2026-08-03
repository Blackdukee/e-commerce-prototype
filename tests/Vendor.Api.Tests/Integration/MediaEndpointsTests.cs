using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Vendor.Api.Tests.Helpers;
using Xunit;

namespace Vendor.Api.Tests.Integration;

public class MediaEndpointsTests : IClassFixture<VendorApiFactory>
{
    private readonly VendorApiFactory _factory;

    public MediaEndpointsTests(VendorApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetPresignedUrl_WithValidFileName_ReturnsOkAndUrl()
    {
        var client = _factory.CreateClient().WithCustomerBearerToken();
        var response = await client.GetAsync("/api/v1/media/presigned-url?fileName=avatar.png&contentType=image/png");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<PresignedUrlResponse>();
        content.Should().NotBeNull();
        content!.Url.Should().NotBeNullOrEmpty();
        content.Url.Should().Contain("avatar.png");
    }

    [Fact]
    public async Task GetPresignedUrl_WithoutFileName_ReturnsBadRequest()
    {
        var client = _factory.CreateClient().WithCustomerBearerToken();
        var response = await client.GetAsync("/api/v1/media/presigned-url");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetPresignedUrl_WithoutAuth_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/media/presigned-url?fileName=avatar.png");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private record PresignedUrlResponse(string Url, string FileName);
}
