using Bogus;

namespace Vendor.Api.Tests.Generators;

public record RegisterCustomerRequestDto(string Email, string FirstName, string LastName, string Password);
public record PlaceOrderRequestDto(string CustomerId, string Address, string City, string State, string Zip, string Country);

public static class ApiTestFakers
{
    static ApiTestFakers()
    {
        Randomizer.Seed = new Random(42);
    }

    public static RegisterCustomerRequestDto CreateRegisterRequest()
    {
        var f = new Faker();
        return new RegisterCustomerRequestDto(
            f.Internet.Email(),
            f.Name.FirstName(),
            f.Name.LastName(),
            "StrongP@ss123!");
    }

    public static PlaceOrderRequestDto CreatePlaceOrderRequest(string customerId = "customer-001")
    {
        var f = new Faker();
        return new PlaceOrderRequestDto(
            customerId,
            f.Address.StreetAddress(),
            f.Address.City(),
            f.Address.State(),
            f.Address.ZipCode(),
            "USA");
    }
}
