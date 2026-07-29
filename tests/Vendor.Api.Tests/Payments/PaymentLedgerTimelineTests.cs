using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Vendor.Application.Queries.Payments.GetPaymentLedger;

namespace Vendor.Api.Tests.Payments;

public class PaymentLedgerTimelineTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetPaymentLedger_NonExistentPayment_Returns404NotFound()
    {
        var nonExistentId = Guid.NewGuid();

        var response = await _client.GetAsync($"/api/v1/payments/{nonExistentId}/ledger");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
