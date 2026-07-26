using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Vendor.Infrastructure.Tests;
using Vendor.Domain.Interfaces;
using Vendor.Domain.ValueObjects;
using Vendor.Infrastructure.Config;
using Xunit;

namespace Vendor.Infrastructure.Tests.Config;

public class SecretResolutionFilterTests
{
    [Fact]
    public void SecretResolutionFilter_MissingSecret_ThrowsFatalException()
    {
        var config = TestConfigFactory.CreateValidVendorConfig();
        var resolver = Substitute.For<ISecretResolver>();
        resolver.ResolveAsync(Arg.Any<SecretReference>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new InvalidOperationException("Environment variable not found"));

        var filter = new SecretResolutionFilter(config, resolver, NullLogger<SecretResolutionFilter>.Instance);

        Action act = () => filter.Configure(_ => { })(null!);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*Fatal boot error*");
    }

    [Fact]
    public void SecretResolutionFilter_AllSecretsResolved_Succeeds()
    {
        var config = TestConfigFactory.CreateValidVendorConfig();
        var resolver = Substitute.For<ISecretResolver>();
        resolver.ResolveAsync(Arg.Any<SecretReference>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult("resolved_secret_val"));

        var filter = new SecretResolutionFilter(config, resolver, NullLogger<SecretResolutionFilter>.Instance);

        Action act = () => filter.Configure(b => { })(null!);

        act.Should().NotThrow();
    }
}
