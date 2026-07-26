using Bogus;
using Vendor.Domain.Aggregates.Customer;

namespace Vendor.Domain.Tests.Generators;

public static class CustomerFaker
{
    static CustomerFaker()
    {
        Randomizer.Seed = new Random(42);
    }

    public static Faker<Customer> Create()
    {
        return new Faker<Customer>()
            .CustomInstantiator(f => new Customer(
                CustomerId.New(),
                f.Internet.Email(),
                f.Name.FirstName(),
                f.Name.LastName(),
                f.PickRandom<CustomerType>(),
                f.Random.Bool()));
    }
}
