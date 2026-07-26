using Vendor.Domain.Interfaces.Adapters;

namespace Vendor.Infrastructure.Payments;

public interface IPaymentGatewayFactory
{
    IPaymentGateway GetPaymentGateway(string providerName);
}

public class PaymentGatewayFactory(
    StripePaymentGateway stripeGateway,
    PayPalPaymentGateway payPalGateway,
    PaymobPaymentGateway paymobGateway)
    : IPaymentGatewayFactory
{
    public IPaymentGateway GetPaymentGateway(string providerName) => providerName.ToLowerInvariant() switch
    {
        "stripe" => stripeGateway,
        "paypal" => payPalGateway,
        "paymob" => paymobGateway,
        _ => throw new ArgumentException($"Unsupported payment provider: '{providerName}'")
    };
}
