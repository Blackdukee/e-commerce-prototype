using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
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
using Vendor.Domain.Entities;
using Vendor.Infrastructure.Auth;
using Vendor.Infrastructure.Identity;
using Vendor.Infrastructure.Outbox;
using Vendor.Infrastructure.Persistence.Entities;

namespace Vendor.Infrastructure.Persistence;

public class VendorDbContext(DbContextOptions<VendorDbContext> options) : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options), IUnitOfWork
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerAuditLog> CustomerAuditLogs => Set<CustomerAuditLog>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<ReturnRequest> ReturnRequests => Set<ReturnRequest>();
    public DbSet<AnalyticsEvent> AnalyticsEvents => Set<AnalyticsEvent>();
    public DbSet<VendorSettings> VendorSettings => Set<VendorSettings>();
    public DbSet<PaymentIdempotencyKey> PaymentIdempotencyKeys => Set<PaymentIdempotencyKey>();
    public DbSet<PaymentLedgerEntry> PaymentLedgerEntries => Set<PaymentLedgerEntry>();
    public DbSet<WebhookEventEntry> WebhookEventEntries => Set<WebhookEventEntry>();
    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public Task BeginTransactionAsync(CancellationToken ct = default)
    {
        if (Database.CurrentTransaction != null) return Task.CompletedTask;
        return Database.BeginTransactionAsync(ct);
    }

    public Task CommitAsync(CancellationToken ct = default)
    {
        if (Database.CurrentTransaction == null) return Task.CompletedTask;
        return Database.CommitTransactionAsync(ct);
    }

    public Task RollbackAsync(CancellationToken ct = default)
    {
        if (Database.CurrentTransaction == null) return Task.CompletedTask;
        return Database.RollbackTransactionAsync(ct);
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken ct = default)
    {
        if (Database.CurrentTransaction != null || Database.ProviderName?.EndsWith("InMemory") == true)
        {
            return await operation();
        }

        var strategy = Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await Database.BeginTransactionAsync(ct);
            try
            {
                var result = await operation();
                if (result is Vendor.Application.Common.Results.IResult res && res.IsFailure)
                {
                    await transaction.RollbackAsync(ct);
                }
                else
                {
                    await transaction.CommitAsync(ct);
                }
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VendorDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
