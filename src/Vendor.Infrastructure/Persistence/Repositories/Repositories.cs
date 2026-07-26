using Microsoft.EntityFrameworkCore;
using Vendor.Domain.Aggregates.AnalyticsEvent;
using Vendor.Domain.Aggregates.Cart;
using Vendor.Domain.Aggregates.Customer;
using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Aggregates.Payment;
using Vendor.Domain.Aggregates.Product;
using Vendor.Domain.Aggregates.Promotion;
using Vendor.Domain.Aggregates.ReturnRequest;
using Vendor.Domain.Aggregates.Shipment;
using Vendor.Domain.Interfaces.Repositories;
using Vendor.Domain.ValueObjects;

namespace Vendor.Infrastructure.Persistence.Repositories;

public class ProductRepository(VendorDbContext context) : IProductRepository
{
    public async Task<Product?> GetByIdAsync(ProductId id, CancellationToken ct = default)
        => await context.Products.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<Product?> GetBySlugAsync(Slug slug, CancellationToken ct = default)
        => await context.Products.FirstOrDefaultAsync(p => p.Slug.Value == slug.Value, ct);

    public async Task<IReadOnlyList<ProductVariant>> GetVariantsByIdsAsync(IEnumerable<ProductVariantId> variantIds, CancellationToken ct = default)
    {
        var ids = variantIds.Select(v => v.Value).ToList();
        var products = await context.Products.ToListAsync(ct);
        return products.SelectMany(p => p.Variants).Where(v => ids.Contains(v.Id.Value)).ToList();
    }

    public async Task<ProductVariant?> GetVariantByIdAsync(ProductVariantId variantId, CancellationToken ct = default)
    {
        var products = await context.Products.ToListAsync(ct);
        return products.SelectMany(p => p.Variants).FirstOrDefault(v => v.Id == variantId);
    }

    public async Task AddAsync(Product product, CancellationToken ct = default)
        => await context.Products.AddAsync(product, ct);

    public Task UpdateAsync(Product product, CancellationToken ct = default)
    {
        context.Products.Update(product);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(ProductId id, CancellationToken ct = default)
        => await context.Products.AnyAsync(p => p.Id == id, ct);
}

public class CustomerRepository(VendorDbContext context) : ICustomerRepository
{
    public async Task<Customer?> GetByIdAsync(CustomerId id, CancellationToken ct = default)
        => await context.Customers.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<Customer?> GetByEmailAsync(string email, CancellationToken ct = default)
        => await context.Customers.FirstOrDefaultAsync(c => c.Email == email, ct);

    public async Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
        => await context.Customers.AnyAsync(c => c.Email == email, ct);

    public async Task AddAsync(Customer customer, CancellationToken ct = default)
        => await context.Customers.AddAsync(customer, ct);

    public Task UpdateAsync(Customer customer, CancellationToken ct = default)
    {
        context.Customers.Update(customer);
        return Task.CompletedTask;
    }

    public async Task<(IReadOnlyList<Customer> Items, int TotalCount)> GetPagedAsync(
        string? emailSearch,
        CustomerRole? role,
        CustomerStatus? status,
        DateTime? registeredFrom,
        DateTime? registeredTo,
        int pageIndex,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = context.Customers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(emailSearch))
        {
            var search = emailSearch.Trim().ToLowerInvariant();
            query = query.Where(c => c.Email.Contains(search));
        }

        if (role.HasValue)
        {
            query = query.Where(c => c.Role == role.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(c => c.Status == status.Value);
        }

        if (registeredFrom.HasValue)
        {
            query = query.Where(c => c.CreatedAtUtc >= registeredFrom.Value);
        }

        if (registeredTo.HasValue)
        {
            query = query.Where(c => c.CreatedAtUtc <= registeredTo.Value);
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(c => c.CreatedAtUtc)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task AddAuditLogAsync(CustomerAuditLog auditLog, CancellationToken ct = default)
    {
        await context.CustomerAuditLogs.AddAsync(auditLog, ct);
    }

    public async Task<(IReadOnlyList<CustomerAuditLog> Items, int TotalCount)> GetAuditLogsAsync(
        CustomerId customerId,
        int pageIndex,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = context.CustomerAuditLogs.Where(a => a.CustomerId == customerId);
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.TimestampUtc)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }
}

public class CartRepository(VendorDbContext context) : ICartRepository
{
    public async Task<Cart?> GetByIdAsync(CartId id, CancellationToken ct = default)
        => await context.Carts.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<Cart?> GetByCustomerIdAsync(CustomerId customerId, CancellationToken ct = default)
        => await context.Carts.FirstOrDefaultAsync(c => c.CustomerId == customerId, ct);

    public async Task<Cart?> GetBySessionIdAsync(string sessionId, CancellationToken ct = default)
        => await context.Carts.FirstOrDefaultAsync(c => c.SessionId == sessionId, ct);

    public async Task AddAsync(Cart cart, CancellationToken ct = default)
        => await context.Carts.AddAsync(cart, ct);

    public Task UpdateAsync(Cart cart, CancellationToken ct = default)
    {
        context.Carts.Update(cart);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<Cart>> GetAbandonedCartsAsync(DateTime abandonedBefore, CancellationToken ct = default)
        => await context.Carts.Where(c => c.LastModifiedUtc <= abandonedBefore && c.Status == CartStatus.Active).ToListAsync(ct);
}

public class OrderRepository(VendorDbContext context) : IOrderRepository
{
    public async Task<Order?> GetByIdAsync(OrderId id, CancellationToken ct = default)
        => await context.Orders.FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<Order?> GetByOrderNumberAsync(string number, CancellationToken ct = default)
        => await context.Orders.FirstOrDefaultAsync(o => o.OrderNumber == number, ct);

    public async Task AddAsync(Order order, CancellationToken ct = default)
        => await context.Orders.AddAsync(order, ct);

    public Task UpdateAsync(Order order, CancellationToken ct = default)
    {
        context.Orders.Update(order);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<Order>> GetByCustomerIdAsync(CustomerId customerId, CancellationToken ct = default)
        => await context.Orders.Where(o => o.CustomerId == customerId).ToListAsync(ct);
}

public class PaymentRepository(VendorDbContext context) : IPaymentRepository
{
    public async Task<Payment?> GetByIdAsync(PaymentId id, CancellationToken ct = default)
        => await context.Payments.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<Payment?> GetByOrderIdAsync(OrderId orderId, CancellationToken ct = default)
        => await context.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId, ct);

    public async Task<Payment?> GetByIdempotencyKeyAsync(string key, CancellationToken ct = default)
        => await context.Payments.FirstOrDefaultAsync(p => p.IdempotencyKey == key, ct);

    public async Task AddAsync(Payment payment, CancellationToken ct = default)
        => await context.Payments.AddAsync(payment, ct);

    public Task UpdateAsync(Payment payment, CancellationToken ct = default)
    {
        context.Payments.Update(payment);
        return Task.CompletedTask;
    }
}

public class ShipmentRepository(VendorDbContext context) : IShipmentRepository
{
    public async Task<Shipment?> GetByIdAsync(ShipmentId id, CancellationToken ct = default)
        => await context.Shipments.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<Shipment?> GetByOrderIdAsync(OrderId orderId, CancellationToken ct = default)
        => await context.Shipments.FirstOrDefaultAsync(s => s.OrderId == orderId, ct);

    public async Task AddAsync(Shipment shipment, CancellationToken ct = default)
        => await context.Shipments.AddAsync(shipment, ct);

    public Task UpdateAsync(Shipment shipment, CancellationToken ct = default)
    {
        context.Shipments.Update(shipment);
        return Task.CompletedTask;
    }
}

public class PromotionRepository(VendorDbContext context) : IPromotionRepository
{
    public async Task<Promotion?> GetByIdAsync(PromotionId id, CancellationToken ct = default)
        => await context.Promotions.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<Promotion?> GetByCodeAsync(string code, CancellationToken ct = default)
        => await context.Promotions.FirstOrDefaultAsync(p => p.Code == code, ct);

    public async Task AddAsync(Promotion promotion, CancellationToken ct = default)
        => await context.Promotions.AddAsync(promotion, ct);

    public Task UpdateAsync(Promotion promotion, CancellationToken ct = default)
    {
        context.Promotions.Update(promotion);
        return Task.CompletedTask;
    }
}

public class ReturnRequestRepository(VendorDbContext context) : IReturnRequestRepository
{
    public async Task<ReturnRequest?> GetByIdAsync(ReturnRequestId id, CancellationToken ct = default)
        => await context.ReturnRequests.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<ReturnRequest>> GetByOrderIdAsync(OrderId orderId, CancellationToken ct = default)
        => await context.ReturnRequests.Where(r => r.OrderId == orderId).ToListAsync(ct);

    public async Task AddAsync(ReturnRequest returnRequest, CancellationToken ct = default)
        => await context.ReturnRequests.AddAsync(returnRequest, ct);

    public Task UpdateAsync(ReturnRequest returnRequest, CancellationToken ct = default)
    {
        context.ReturnRequests.Update(returnRequest);
        return Task.CompletedTask;
    }
}

public class AnalyticsEventRepository(VendorDbContext context) : IAnalyticsEventRepository
{
    public async Task AddAsync(AnalyticsEvent analyticsEvent, CancellationToken ct = default)
        => await context.AnalyticsEvents.AddAsync(analyticsEvent, ct);

    public async Task<IReadOnlyList<AnalyticsEvent>> GetByCustomerIdAsync(CustomerId customerId, int pageSize = 50, int pageIndex = 0, CancellationToken ct = default)
        => await context.AnalyticsEvents
            .Where(a => a.CustomerId == customerId)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
}
