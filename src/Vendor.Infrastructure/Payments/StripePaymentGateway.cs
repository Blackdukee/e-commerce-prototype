using System.Security.Cryptography;
using System.Text;
using Stripe;
using Vendor.Domain.Interfaces.Adapters;
using Vendor.Domain.ValueObjects;

namespace Vendor.Infrastructure.Payments;

public class StripePaymentGateway : IPaymentGateway
{
    public Task<PaymentAuthorizationResult> AuthorizeAsync(Money amount, string idempotencyKey, CancellationToken ct = default)
    {
        // Simulated Stripe PaymentIntents call with idempotency key
        var authToken = $"stripe_auth_{idempotencyKey}";
        return Task.FromResult(new PaymentAuthorizationResult(true, authToken, null));
    }

    public Task<PaymentCaptureResult> CaptureAsync(string authorizationToken, string idempotencyKey, CancellationToken ct = default)
    {
        var txnId = $"stripe_txn_{idempotencyKey}";
        return Task.FromResult(new PaymentCaptureResult(true, txnId, null));
    }

    public Task<PaymentRefundResult> RefundAsync(string gatewayTransactionId, Money refundAmount, string idempotencyKey, CancellationToken ct = default)
    {
        var refundTxnId = $"stripe_ref_{idempotencyKey}";
        return Task.FromResult(new PaymentRefundResult(true, refundTxnId, null));
    }

    public static bool VerifyWebhookSignature(string jsonPayload, string stripeSignatureHeader, string secret)
    {
        try
        {
            var stripeEvent = EventUtility.ConstructEvent(
                jsonPayload,
                stripeSignatureHeader,
                secret,
                tolerance: 300,
                throwOnApiVersionMismatch: false);
            return stripeEvent != null;
        }
        catch
        {
            return false;
        }
    }
}
