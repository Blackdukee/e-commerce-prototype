using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vendor.Domain.Aggregates.Customer;
using Vendor.Domain.Aggregates.Product;
using Vendor.Domain.ValueObjects;
using Vendor.Infrastructure.Identity;

namespace Vendor.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(
        VendorDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ILogger logger)
    {
        // 1. Roles
        string[] roles = ["Admin", "Customer"];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new ApplicationRole(role));
            }
        }

        // 2. Admin User
        var adminEmail = "admin@vendor.com";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            var adminCustomer = new Customer(
                CustomerId.New(),
                adminEmail,
                "Admin",
                "User",
                CustomerType.Registered,
                analyticsConsent: false,
                role: CustomerRole.Admin);
            await context.Customers.AddAsync(adminCustomer);
            await context.SaveChangesAsync();

            adminUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                CustomerId = adminCustomer.Id.Value,
                CreatedAtUtc = DateTime.UtcNow
            };

            var res = await userManager.CreateAsync(adminUser, "Admin123!");
            if (res.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
                logger.LogInformation("Seeded default Admin user: {Email}", adminEmail);
            }
        }
        else
        {
            var existingAdminCustomer = await context.Customers.FirstOrDefaultAsync(c => c.Email == adminEmail);
            if (existingAdminCustomer != null && existingAdminCustomer.Role != CustomerRole.Admin && existingAdminCustomer.Role != CustomerRole.SuperAdmin)
            {
                existingAdminCustomer.ChangeRole(CustomerRole.Admin, existingAdminCustomer.Id);
                await context.SaveChangesAsync();
                logger.LogInformation("Updated existing Admin user role to Admin: {Email}", adminEmail);
            }
        }

        // 3. Customer User
        var customerEmail = "customer@vendor.com";
        var customerUser = await userManager.FindByEmailAsync(customerEmail);
        if (customerUser == null)
        {
            var customer = new Customer(CustomerId.New(), customerEmail, "Demo", "Customer", CustomerType.Registered);
            await context.Customers.AddAsync(customer);
            await context.SaveChangesAsync();

            customerUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = customerEmail,
                Email = customerEmail,
                EmailConfirmed = true,
                CustomerId = customer.Id.Value,
                CreatedAtUtc = DateTime.UtcNow
            };

            var res = await userManager.CreateAsync(customerUser, "Customer123!");
            if (res.Succeeded)
            {
                await userManager.AddToRoleAsync(customerUser, "Customer");
                logger.LogInformation("Seeded default Customer user: {Email}", customerEmail);
            }
        }

        // 4. Sample Products
        if (!await context.Products.AnyAsync())
        {
            var p1 = new Product(
                ProductId.New(),
                "Noise-Cancelling Studio Pods Pro",
                new Slug("studio-pods-pro"),
                new Money(2499.00m, "EGP"),
                "High-fidelity active noise cancellation with 36-hour battery life and spatial audio.",
                category: "Audio",
                categories: ["Audio", "Electronics"],
                tags: ["wireless", "headphones", "bluetooth"]);
            p1.AddImage("https://images.unsplash.com/photo-1505740420928-5e560c06d30e?w=800&q=80");
            p1.AddVariant(new ProductVariant(ProductVariantId.New(), p1.Id, "PODS-BLK", new Money(0m, "EGP"), 100, new Weight(0.3m, WeightUnit.Kg), new Dimensions(15m, 10m, 5m, DimensionUnit.Cm)));
            p1.AddVariant(new ProductVariant(ProductVariantId.New(), p1.Id, "PODS-SLV", new Money(0m, "EGP"), 80, new Weight(0.3m, WeightUnit.Kg), new Dimensions(15m, 10m, 5m, DimensionUnit.Cm)));
            p1.Activate();

            var p2 = new Product(
                ProductId.New(),
                "Custom Mechanical Keyboard RGB",
                new Slug("mechanical-keyboard-rgb"),
                new Money(3850.00m, "EGP"),
                "Hot-swappable tactile switches, anodized aluminum chassis, and per-key RGB backlighting.",
                category: "Accessories",
                categories: ["Accessories", "Gaming"],
                tags: ["mechanical", "rgb", "keyboard"]);
            p2.AddImage("https://images.unsplash.com/photo-1587829741301-dc798b83add3?w=800&q=80");
            p2.AddVariant(new ProductVariant(ProductVariantId.New(), p2.Id, "KB-RED-SW", new Money(0m, "EGP"), 50, new Weight(1.2m, WeightUnit.Kg), new Dimensions(35m, 14m, 4m, DimensionUnit.Cm)));
            p2.AddVariant(new ProductVariant(ProductVariantId.New(), p2.Id, "KB-BRN-SW", new Money(0m, "EGP"), 45, new Weight(1.2m, WeightUnit.Kg), new Dimensions(35m, 14m, 4m, DimensionUnit.Cm)));
            p2.Activate();

            var p3 = new Product(
                ProductId.New(),
                "Ultra-Fitness Smartwatch V2",
                new Slug("smartwatch-v2"),
                new Money(4100.00m, "EGP"),
                "Titanium case, dual-frequency GPS, heart-rate tracking, ECG sensor, and 100m water resistance.",
                category: "Wearables",
                categories: ["Wearables", "Fitness"],
                tags: ["smartwatch", "gps", "fitness"]);
            p3.AddImage("https://images.unsplash.com/photo-1523275335684-37898b6baf30?w=800&q=80");
            p3.AddVariant(new ProductVariant(ProductVariantId.New(), p3.Id, "WATCH-TITANIUM", new Money(0m, "EGP"), 60, new Weight(0.15m, WeightUnit.Kg), new Dimensions(5m, 5m, 2m, DimensionUnit.Cm)));
            p3.Activate();

            var p4 = new Product(
                ProductId.New(),
                "Waterproof Tech Commuter Pack",
                new Slug("commuter-pack"),
                new Money(1250.00m, "EGP"),
                "Dedicated 16\" padded laptop compartment, RFID-blocking security pocket, and ergonomic airflow back panel.",
                category: "Bags",
                categories: ["Bags", "Travel"],
                tags: ["backpack", "waterproof", "laptop"]);
            p4.AddImage("https://images.unsplash.com/photo-1553062407-98eeb64c6a62?w=800&q=80");
            p4.AddVariant(new ProductVariant(ProductVariantId.New(), p4.Id, "PACK-20L-BLK", new Money(0m, "EGP"), 120, new Weight(0.8m, WeightUnit.Kg), new Dimensions(45m, 30m, 15m, DimensionUnit.Cm)));
            p4.Activate();

            await context.Products.AddRangeAsync(p1, p2, p3, p4);
            await context.SaveChangesAsync();
            logger.LogInformation("Seeded 4 featured active catalog products.");
        }
    }
}
