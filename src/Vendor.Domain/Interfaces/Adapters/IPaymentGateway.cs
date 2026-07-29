using Vendor.Domain.ValueObjects;

namespace Vendor.Domain.Interfaces.Adapters;

public record PaymentAuthorizationResult(bool Success, string AuthorizationToken, string? ErrorMessage);
public record PaymentCaptureResult(bool Success, string GatewayTransactionId, string? ErrorMessage);
public record PaymentRefundResult(bool Success, string RefundTransactionId, string? ErrorMessage);

public interface IPaymentGateway
{
    Task<PaymentAuthorizationResult> AuthorizeAsync(
        Money amount,
        string idempotencyKey,
        CancellationToken ct = default);

    Task<PaymentCaptureResult> CaptureAsync(
        string authorizationToken,
        string idempotencyKey,
        CancellationToken ct = default);

    Task<PaymentRefundResult> RefundAsync(
        string gatewayTransactionId,
        Money refundAmount,
        string idempotencyKey,
        CancellationToken ct = default);

    Task<bool> VerifyWebhookSignatureAsync(
        string payload,
        string signatureHeader,
        string secret,
        CancellationToken ct = default);
}
