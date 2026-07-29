using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Vendor.Api.Endpoints;

namespace Vendor.Api.Tests.Payments;

public class ProcessPaymentIdempotencyTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
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
