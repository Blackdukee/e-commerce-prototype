using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Vendor.Api.Endpoints;
using Vendor.Api.Tests.Helpers;
using Xunit;

namespace Vendor.Api.Tests.Payments;

public class ProcessPaymentIdempotencyTests(VendorApiFactory factory) : IClassFixture<VendorApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task ProcessPayment_MissingIdempotencyKey_Returns400BadRequest()
    {
        var payload = new ProcessPaymentApiRequest(Guid.NewGuid(), 100m, "USD", "CreditCard", "Stripe");

        var response = await _client.PostAsJsonAsync("/api/v1/payments/process", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
