using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentValidation;
using NSubstitute;
using Vendor.Application.Commands.VendorSettings;
using Vendor.Application.Validators;
using Vendor.Domain.Interfaces;
using Xunit;

namespace Vendor.Application.Tests.Handlers;

public class UpdateVendorSettingsHandlerTests
{
    [Fact]
    public async Task Handle_ValidRuntimePatch_UpdatesConfigAndReturnsDto()
    {
        var baseConfig = TestConfigFactory.CreateValidVendorConfig();
        var repo = Substitute.For<IVendorSettingsRepository>();
        var validator = new VendorConfigValidator();

        repo.GetVersionAsync(baseConfig.VendorId, Arg.Any<CancellationToken>())
            .Returns(2);

        var handler = new UpdateVendorSettingsHandler(baseConfig, repo, validator);
        var command = new UpdateVendorSettingsCommand(baseConfig.VendorId, baseConfig.Runtime, 1, "admin@acme.com");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Version.Should().Be(2);
        await repo.Received(1).UpdateRuntimeConfigAsync(
            baseConfig.VendorId,
            baseConfig.Runtime,
            1,
            "admin@acme.com",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InvalidRuntimePatch_ThrowsValidationException()
    {
        var baseConfig = TestConfigFactory.CreateValidVendorConfig();
        var repo = Substitute.For<IVendorSettingsRepository>();
        var validator = new VendorConfigValidator();

        var invalidPayments = new[]
        {
            baseConfig.Runtime.Payments[0],
            baseConfig.Runtime.Payments[0] // 2 default payment providers
        };
        var invalidRuntime = baseConfig.Runtime with { Payments = invalidPayments };

        var handler = new UpdateVendorSettingsHandler(baseConfig, repo, validator);
        var command = new UpdateVendorSettingsCommand(baseConfig.VendorId, invalidRuntime, 1, "admin@acme.com");

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }
}
