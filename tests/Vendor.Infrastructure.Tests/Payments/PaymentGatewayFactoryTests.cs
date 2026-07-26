using FluentAssertions;
using Vendor.Infrastructure.Payments;

namespace Vendor.Infrastructure.Tests.Payments;

public class PaymentGatewayFactoryTests
{
    private static PaymentGatewayFactory CreateFactory()
    {
        var stripe = new StripePaymentGateway();
        var paypal = new PayPalPaymentGateway(new HttpClient());
        var paymob = new PaymobPaymentGateway();

        return new PaymentGatewayFactory(stripe, paypal, paymob);
    }

    [Theory]
    [InlineData("stripe", typeof(StripePaymentGateway))]
    [InlineData("paypal", typeof(PayPalPaymentGateway))]
    [InlineData("paymob", typeof(PaymobPaymentGateway))]
    public void GetPaymentGateway_KnownProvider_ReturnsMatchingGateway(string providerName, Type expectedType)
    {
        var factory = CreateFactory();

        var result = factory.GetPaymentGateway(providerName);

        result.Should().BeOfType(expectedType);
    }

    [Fact]
    public void GetPaymentGateway_UnknownProvider_ThrowsArgumentException()
    {
        var factory = CreateFactory();

        Action act = () => factory.GetPaymentGateway("unknown");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Unsupported payment provider*");
    }
}
