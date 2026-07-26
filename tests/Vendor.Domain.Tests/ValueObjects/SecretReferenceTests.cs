using FluentAssertions;
using Vendor.Domain.Enums;
using Vendor.Domain.ValueObjects;

namespace Vendor.Domain.Tests.ValueObjects;

public class SecretReferenceTests
{
    [Theory]
    [InlineData("ref:env:STRIPE_KEY", SecretBackend.Env, "STRIPE_KEY")]
    [InlineData("ref:vault:secret/data/db", SecretBackend.Vault, "secret/data/db")]
    [InlineData("ref:aws-ssm:/prod/db/password", SecretBackend.AwsSsm, "/prod/db/password")]
    public void SecretReference_ValidPrefixes_ParsesCorrectly(string raw, SecretBackend expectedBackend, string expectedPath)
    {
        var secretRef = new SecretReference(raw);

        secretRef.Backend.Should().Be(expectedBackend);
        secretRef.Path.Should().Be(expectedPath);
    }

    [Theory]
    [InlineData("raw-secret-key")]
    [InlineData("ref:invalid:path")]
    [InlineData("")]
    public void SecretReference_InvalidPrefix_ThrowsArgumentException(string invalidRef)
    {
        Action act = () => _ = new SecretReference(invalidRef);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SecretReference_ToString_MasksValue()
    {
        var secretRef = new SecretReference("ref:env:STRIPE_KEY");

        secretRef.ToString().Should().Be("ref:***");
    }
}
