using System.Security.Cryptography;
using System.Text;
using Vendor.Domain.Interfaces.Adapters;
using Vendor.Domain.ValueObjects;

namespace Vendor.Infrastructure.Payments;

public class PaymobPaymentGateway : IPaymentGateway
{
    public Task<PaymentAuthorizationResult> AuthorizeAsync(Money amount, string idempotencyKey, CancellationToken ct = default)
    {
        var authToken = $"paymob_auth_{idempotencyKey}";
        return Task.FromResult(new PaymentAuthorizationResult(true, authToken, null));
    }

    public Task<PaymentCaptureResult> CaptureAsync(string authorizationToken, string idempotencyKey, CancellationToken ct = default)
    {
        var txnId = $"paymob_txn_{idempotencyKey}";
        return Task.FromResult(new PaymentCaptureResult(true, txnId, null));
    }

    public Task<PaymentRefundResult> RefundAsync(string gatewayTransactionId, Money refundAmount, string idempotencyKey, CancellationToken ct = default)
    {
        var refundTxnId = $"paymob_ref_{idempotencyKey}";
        return Task.FromResult(new PaymentRefundResult(true, refundTxnId, null));
    }

    public static bool VerifyPaymobHmac(IDictionary<string, string> payload, string hmacSecret, string receivedHmac)
    {
        var keysToConcatenate = new[]
        {
            "amount_cents", "created_at", "currency", "error_occured", "has_parent_transaction",
            "id", "integration_id", "is_3d_secure", "is_auth", "is_capture", "is_refunded",
            "is_standalone_payment", "order.id", "owner", "pending", "source_data.pan",
            "source_data.sub_type", "source_data.type", "success"
        };

        var concatenated = new StringBuilder();
        foreach (var key in keysToConcatenate)
        {
            if (payload.TryGetValue(key, out var val))
            {
                concatenated.Append(val);
            }
        }

        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(hmacSecret));
        var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(concatenated.ToString()));
        var computedHex = Convert.ToHexString(computedHash).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHex),
            Encoding.UTF8.GetBytes(receivedHmac.ToLowerInvariant()));
    }
}
