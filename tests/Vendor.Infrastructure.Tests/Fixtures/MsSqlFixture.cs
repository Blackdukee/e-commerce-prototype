using DotNet.Testcontainers.Builders;
using Microsoft.EntityFrameworkCore;
using Respawn;
using Respawn.Graph;
using Testcontainers.MsSql;
using Vendor.Infrastructure.Persistence;
using Xunit;

namespace Vendor.Infrastructure.Tests.Fixtures;

public class MsSqlFixture : IAsyncLifetime
{
    private MsSqlContainer? _container;
    private Respawner? _respawner;

    public string ConnectionString => _container?.GetConnectionString() 
        ?? "Server=(localdb)\\mssqllocaldb;Database=VendorTestDb;Trusted_Connection=True;MultipleActiveResultSets=true";

    public DbContextOptions<VendorDbContext> DbContextOptions { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        try
        {
            _container = new MsSqlBuilder()
                .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
                .Build();

            await _container.StartAsync();
        }
        catch
        {
            // Fallback for environment without Docker daemon during build testing
            _container = null;
        }

        DbContextOptions = new DbContextOptionsBuilder<VendorDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        using var dbContext = new VendorDbContext(DbContextOptions);
        await dbContext.Database.EnsureCreatedAsync();

        if (_container != null)
        {
            _respawner = await Respawner.CreateAsync(ConnectionString, new RespawnerOptions
            {
                DbAdapter = DbAdapter.SqlServer,
                TablesToIgnore = [new Table("__EFMigrationsHistory")]
            });
        }
    }

    public async Task ResetAsync()
    {
        if (_respawner != null)
        {
            await _respawner.ResetAsync(ConnectionString);
        }
        else
        {
            using var dbContext = new VendorDbContext(DbContextOptions);
            await dbContext.Database.EnsureDeletedAsync();
            await dbContext.Database.EnsureCreatedAsync();
        }
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
        {
            await _container.StopAsync();
            await _container.DisposeAsync();
        }
    }
}
