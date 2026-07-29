using System.Net.Http.Json;
using Vendor.Domain.Interfaces.Adapters;
using Vendor.Domain.ValueObjects;

namespace Vendor.Infrastructure.Payments;

public record PayPalVerificationResponse(string VerificationStatus);

public class PayPalPaymentGateway(HttpClient httpClient) : IPaymentGateway
{
    public Task<PaymentAuthorizationResult> AuthorizeAsync(Money amount, string idempotencyKey, CancellationToken ct = default)
    {
        var authToken = $"paypal_auth_{idempotencyKey}";
        return Task.FromResult(new PaymentAuthorizationResult(true, authToken, null));
    }

    public Task<PaymentCaptureResult> CaptureAsync(string authorizationToken, string idempotencyKey, CancellationToken ct = default)
    {
        var txnId = $"paypal_txn_{idempotencyKey}";
        return Task.FromResult(new PaymentCaptureResult(true, txnId, null));
    }

    public Task<PaymentRefundResult> RefundAsync(string gatewayTransactionId, Money refundAmount, string idempotencyKey, CancellationToken ct = default)
    {
        var refundTxnId = $"paypal_ref_{idempotencyKey}";
        return Task.FromResult(new PaymentRefundResult(true, refundTxnId, null));
    }

    public Task<bool> VerifyWebhookSignatureAsync(string payload, string signatureHeader, string secret, CancellationToken ct = default)
    {
        return Task.FromResult(!string.IsNullOrWhiteSpace(signatureHeader));
    }

    public async Task<bool> VerifyWebhookSignatureAsync(object requestPayload, CancellationToken ct = default)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync("/v1/notifications/verify-webhook-signature", requestPayload, ct);
            if (!response.IsSuccessStatusCode) return false;

            var result = await response.Content.ReadFromJsonAsync<PayPalVerificationResponse>(cancellationToken: ct);
            return result?.VerificationStatus?.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase) ?? false;
        }
        catch
        {
            return false;
        }
    }
}
