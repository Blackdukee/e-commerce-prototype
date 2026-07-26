using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vendor.Infrastructure.Persistence;
using Vendor.Infrastructure.Persistence.Repositories;
using Vendor.Infrastructure.Tests.Fixtures;

namespace Vendor.Infrastructure.Tests.Persistence;

[Collection("Database")]
public class VendorSettingsRepositoryTests : IAsyncLifetime
{
    private readonly MsSqlFixture _fixture;

    public VendorSettingsRepositoryTests(MsSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task UpdateRuntimeConfigAsync_PersistsAndIncrementsVersion()
    {
        using var dbContext = new VendorDbContext(_fixture.DbContextOptions);
        var repo = new VendorSettingsRepository(dbContext);
        var baseConfig = TestConfigFactory.CreateValidVendorConfig();

        await repo.UpdateRuntimeConfigAsync(baseConfig.VendorId, baseConfig.Runtime, 1, "admin", default);

        var version = await repo.GetVersionAsync(baseConfig.VendorId, default);
        version.Should().Be(1);

        var retrievedRuntime = await repo.GetRuntimeConfigAsync(baseConfig.VendorId, default);
        retrievedRuntime.Should().NotBeNull();
        retrievedRuntime!.Branding.PrimaryColor.Should().Be("#2563EB");
    }
}
