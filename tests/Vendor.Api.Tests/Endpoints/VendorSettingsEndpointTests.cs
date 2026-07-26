using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Vendor.Api.Tests.Helpers;
using Vendor.Application.DTOs;
using Xunit;

namespace Vendor.Api.Tests.Endpoints;

public class VendorSettingsEndpointTests : IClassFixture<VendorApiFactory>
{
    private readonly VendorApiFactory _factory;

    public VendorSettingsEndpointTests(VendorApiFactory factory)
    {
        _factory = factory;
        Environment.SetEnvironmentVariable("JWT_SECRET", "test_jwt_secret_value_min_32_bytes_long");
        Environment.SetEnvironmentVariable("GOOGLE_CLIENT_SECRET", "test_google_secret");
        Environment.SetEnvironmentVariable("SG_KEY", "test_sendgrid_key");
        Environment.SetEnvironmentVariable("STRIPE_SK", "test_stripe_secret_key");
        Environment.SetEnvironmentVariable("STRIPE_WH", "test_stripe_webhook_secret");
    }

    [Fact]
    public async Task GetVendorConfig_Returns200OKWithTiers()
    {
        var client = _factory.CreateClient().WithAdminBearerToken();

        var response = await client.GetAsync("/api/v1/admin/config");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<VendorConfigDto>();
        dto.Should().NotBeNull();
        dto!.VendorId.Should().Be("acme-store");
        dto.Tiers.Build.VendorId.Should().Be("acme-store");
    }

    [Fact]
    public async Task PatchVendorConfig_NullRuntime_Returns400BadRequest()
    {
        var client = _factory.CreateClient().WithAdminBearerToken();
        var patch = new VendorConfigPatchDto(null, 1);

        var response = await client.PatchAsJsonAsync("/api/v1/admin/config", patch);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
