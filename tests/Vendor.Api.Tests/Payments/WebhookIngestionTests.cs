using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Vendor.Api.Endpoints;
using Xunit;

namespace Vendor.Api.Tests.Payments;

public class WebhookIngestionTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Webhook_MissingSignature_Returns400BadRequest()
    {
        var payload = new WebhookApiPayload("evt_100", "payment_intent.succeeded", Guid.NewGuid(), 100m, "USD", "pi_100");

        var response = await _client.PostAsJsonAsync("/api/v1/webhooks/stripe", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
