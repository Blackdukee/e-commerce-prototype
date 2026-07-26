namespace Vendor.Domain.ValueObjects;

public sealed record Address
{
    public string Street { get; }
    public string City { get; }
    public string State { get; }
    public string ZipCode { get; }
    public string CountryCode { get; }

    public Address(string street, string city, string state, string zipCode, string countryCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(street, nameof(street));
        ArgumentException.ThrowIfNullOrWhiteSpace(city, nameof(city));
        ArgumentException.ThrowIfNullOrWhiteSpace(state, nameof(state));
        ArgumentException.ThrowIfNullOrWhiteSpace(zipCode, nameof(zipCode));
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode, nameof(countryCode));

        Street = street.Trim();
        City = city.Trim();
        State = state.Trim();
        ZipCode = zipCode.Trim();
        CountryCode = countryCode.Trim().ToUpperInvariant();
    }
}
