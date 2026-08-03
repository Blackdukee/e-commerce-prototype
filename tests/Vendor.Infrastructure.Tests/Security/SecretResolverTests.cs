using FluentAssertions;
using Vendor.Domain.ValueObjects;
using Vendor.Infrastructure.Security.Resolvers;
using Xunit;

namespace Vendor.Infrastructure.Tests.Security;

public class SecretResolverTests
{
    [Fact]
    public async Task EnvSecretResolver_WithValidEnvVar_ReturnsValue()
    {
        Environment.SetEnvironmentVariable("TEST_SECRET_VAR", "my-secret-val");
        try
        {
            var resolver = new EnvSecretResolver();
            var secretRef = new SecretReference("ref:env:TEST_SECRET_VAR");
            var val = await resolver.ResolveSecretAsync(secretRef);
            val.Should().Be("my-secret-val");
        }
        finally
        {
            Environment.SetEnvironmentVariable("TEST_SECRET_VAR", null);
        }
    }

    [Fact]
    public async Task VaultSecretResolver_Unconfigured_FallsBackToEnv()
    {
        Environment.SetEnvironmentVariable("FALLBACK_VAR", "fallback-val");
        try
        {
            var resolver = new VaultSecretResolver();
            var secretRef = new SecretReference("ref:vault:FALLBACK_VAR");
            var val = await resolver.ResolveSecretAsync(secretRef);
            val.Should().Be("fallback-val");
        }
        finally
        {
            Environment.SetEnvironmentVariable("FALLBACK_VAR", null);
        }
    }

    [Fact]
    public async Task AwsSsmSecretResolver_Unconfigured_FallsBackToEnv()
    {
        Environment.SetEnvironmentVariable("AWS_SECRET_VAR", "aws-val");
        try
        {
            var resolver = new AwsSsmSecretResolver();
            var secretRef = new SecretReference("ref:aws-ssm:AWS_SECRET_VAR");
            var val = await resolver.ResolveSecretAsync(secretRef);
            val.Should().Be("aws-val");
        }
        finally
        {
            Environment.SetEnvironmentVariable("AWS_SECRET_VAR", null);
        }
    }

    [Fact]
    public async Task CompositeSecretResolver_RoutesEnvReferenceCorrectly()
    {
        Environment.SetEnvironmentVariable("COMPOSITE_VAR", "comp-val");
        try
        {
            var resolver = new CompositeSecretResolver();
            var val = await resolver.ResolveSecretAsync("ref:env:COMPOSITE_VAR");
            val.Should().Be("comp-val");
        }
        finally
        {
            Environment.SetEnvironmentVariable("COMPOSITE_VAR", null);
        }
    }
}
