using Microsoft.EntityFrameworkCore;
using Vendor.Application.Interfaces;
using Vendor.Domain.Aggregates.AnalyticsEvent;
using Vendor.Domain.Aggregates.Cart;
using Vendor.Domain.Aggregates.Customer;
using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Aggregates.Payment;
using Vendor.Domain.Aggregates.Product;
using Vendor.Domain.Aggregates.Promotion;
using Vendor.Domain.Aggregates.ReturnRequest;
using Vendor.Domain.Aggregates.Shipment;
using Vendor.Infrastructure.Auth;
using Vendor.Infrastructure.Outbox;
using Vendor.Infrastructure.Persistence.Entities;

namespace Vendor.Infrastructure.Persistence;

public class VendorDbContext(DbContextOptions<VendorDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<ReturnRequest> ReturnRequests => Set<ReturnRequest>();
    public DbSet<AnalyticsEvent> AnalyticsEvents => Set<AnalyticsEvent>();
    public DbSet<VendorSettings> VendorSettings => Set<VendorSettings>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public Task BeginTransactionAsync(CancellationToken ct = default)
    {
        return Database.BeginTransactionAsync(ct);
    }

    public Task CommitAsync(CancellationToken ct = default)
    {
        return Database.CommitTransactionAsync(ct);
    }

    public Task RollbackAsync(CancellationToken ct = default)
    {
        return Database.RollbackTransactionAsync(ct);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VendorDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
