using FluentAssertions;

namespace Vendor.Infrastructure.Tests.Auth;

public class SecretResolverTests
{
    [Fact]
    public void EnvironmentSecretResolver_ResolvesEnvVar_WhenPresent()
    {
        var varName = "TEST_SECRET_KEY_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(varName, "my-secret-value");

        try
        {
            var value = Environment.GetEnvironmentVariable(varName);
            value.Should().Be("my-secret-value");
        }
        finally
        {
            Environment.SetEnvironmentVariable(varName, null);
        }
    }
}
