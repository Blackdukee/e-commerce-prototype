using Bogus;
using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Aggregates.Payment;
using Vendor.Domain.Aggregates.Promotion;
using Vendor.Domain.Aggregates.Shipment;
using Vendor.Domain.ValueObjects;

namespace Vendor.Infrastructure.Tests.Generators;

public static class InfrastructureTestFakers
{
    static InfrastructureTestFakers()
    {
        Randomizer.Seed = new Random(42);
    }

    public static Payment CreatePayment()
    {
        var f = new Faker();
        var payment = new Payment(
            PaymentId.New(),
            OrderId.New(),
            new Money(f.Random.Decimal(20m, 300m), "USD"),
            "IK-" + f.Random.AlphaNumeric(10));
        payment.Capture("TXN-" + f.Random.AlphaNumeric(12));
        return payment;
    }

    public static Shipment CreateShipment()
    {
        var f = new Faker();
        var address = new Address(f.Address.StreetAddress(), f.Address.City(), f.Address.State(), f.Address.ZipCode(), "USA");
        return new Shipment(
            ShipmentId.New(),
            OrderId.New(),
            "FLAT-RATE",
            address);
    }

    public static Promotion CreatePromotion()
    {
        var f = new Faker();
        var code = "PROMO" + f.Random.AlphaNumeric(4).ToUpperInvariant();
        return new Promotion(
            PromotionId.New(),
            code,
            DiscountType.Percentage,
            15m,
            new DateRange(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30)));
    }
}
