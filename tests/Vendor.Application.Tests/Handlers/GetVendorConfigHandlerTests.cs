using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using Vendor.Application.Queries.VendorSettings;
using Vendor.Application.Validators;
using Vendor.Domain.Interfaces;
using Xunit;

namespace Vendor.Application.Tests.Handlers;

public class GetVendorConfigHandlerTests
{
    [Fact]
    public async Task Handle_ExistingConfig_ReturnsVendorConfigDto()
    {
        var baseConfig = TestConfigFactory.CreateValidVendorConfig();
        var repo = Substitute.For<IVendorSettingsRepository>();
        repo.GetRuntimeConfigAsync(baseConfig.VendorId, Arg.Any<CancellationToken>())
            .Returns((Vendor.Domain.Aggregates.VendorSettings.VendorRuntimeConfig?)null);
        repo.GetVersionAsync(baseConfig.VendorId, Arg.Any<CancellationToken>())
            .Returns(1);

        var handler = new GetVendorConfigHandler(baseConfig, repo);
        var query = new GetVendorConfigQuery(baseConfig.VendorId);

        var dto = await handler.Handle(query, CancellationToken.None);

        dto.Should().NotBeNull();
        dto.VendorId.Should().Be("acme-store");
        dto.Version.Should().Be(1);
        dto.Tiers.Build.VendorId.Should().Be("acme-store");
    }
}
