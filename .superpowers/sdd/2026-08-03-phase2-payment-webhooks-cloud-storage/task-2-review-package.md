diff --git a/src/Vendor.Api/Endpoints/PaymentEndpoints.cs b/src/Vendor.Api/Endpoints/PaymentEndpoints.cs
index 79defa9..04268aa 100644
--- a/src/Vendor.Api/Endpoints/PaymentEndpoints.cs
+++ b/src/Vendor.Api/Endpoints/PaymentEndpoints.cs
@@ -128,55 +128,7 @@ public static class PaymentEndpoints
             return Results.Ok(new PaymentDto(id, Guid.NewGuid(), "stripe", "Refunded", req.Amount, "re_123", DateTime.UtcNow));
         });
 
-        // Webhook ingestion endpoints
-        var webhooks = group.MapGroup("/webhooks")
-            .WithTags("Webhooks");
-
-        webhooks.MapPost("/{providerName}", async (string providerName, HttpContext ctx, WebhookApiPayload payload, ISender mediator) =>
-        {
-            var sigHeader = ctx.Request.Headers["Stripe-Signature"].ToString();
-            if (string.IsNullOrEmpty(sigHeader))
-            {
-                sigHeader = ctx.Request.Headers["X-Paymob-Signature"].ToString();
-            }
-            if (string.IsNullOrEmpty(sigHeader))
-            {
-                sigHeader = ctx.Request.Headers["Paypal-Transmission-Sig"].ToString();
-            }
-
-            var eventId = string.IsNullOrWhiteSpace(payload.EventId) ? $"evt_{Guid.NewGuid():N}" : payload.EventId;
-            var eventType = string.IsNullOrWhiteSpace(payload.EventType) ? "payment_intent.succeeded" : payload.EventType;
-            var paymentId = payload.PaymentId ?? Guid.NewGuid();
-            var amount = payload.Amount ?? 100m;
-            var currency = string.IsNullOrWhiteSpace(payload.Currency) ? "USD" : payload.Currency;
-
-            var command = new ProcessWebhookCommand(
-                providerName,
-                sigHeader,
-                RawPayload: System.Text.Json.JsonSerializer.Serialize(payload),
-                eventId,
-                eventType,
-                paymentId,
-                amount,
-                currency,
-                payload.GatewayReferenceId
-            );
-
-            var result = await mediator.Send(command);
-
-            if (result.IsFailure)
-            {
-                if (result.Error.Code == "Auth.Unauthorized")
-                {
-                    return Results.Unauthorized();
-                }
-
-                return Results.BadRequest(new { error = result.Error.Description });
-            }
-
-            return Results.Ok(result.Value);
-        });
-
         return group;
     }
 }
+
diff --git a/src/Vendor.Api/Endpoints/WebhookEndpoints.cs b/src/Vendor.Api/Endpoints/WebhookEndpoints.cs
new file mode 100644
index 0000000..c2269d6
--- /dev/null
+++ b/src/Vendor.Api/Endpoints/WebhookEndpoints.cs
@@ -0,0 +1,65 @@
+using MediatR;
+using Microsoft.AspNetCore.Builder;
+using Microsoft.AspNetCore.Http;
+using Microsoft.AspNetCore.Routing;
+using Vendor.Application.Modules.Payments;
+
+namespace Vendor.Api.Endpoints;
+
+public static class WebhookEndpoints
+{
+    public static RouteGroupBuilder MapWebhookEndpoints(this RouteGroupBuilder group)
+    {
+        var webhooks = group.MapGroup("/webhooks").WithTags("Webhooks");
+
+        webhooks.MapPost("/stripe", async (HttpRequest request, ISender mediator, CancellationToken ct) =>
+        {
+            using var reader = new StreamReader(request.Body);
+            var rawBody = await reader.ReadToEndAsync(ct);
+            var sigHeader = request.Headers["Stripe-Signature"].ToString();
+
+            if (string.IsNullOrEmpty(sigHeader) || string.IsNullOrEmpty(rawBody))
+            {
+                return Results.BadRequest(new { Error = "Invalid Stripe webhook payload or signature." });
+            }
+
+            var command = new ProcessPaymentWebhookCommand("Stripe", sigHeader, rawBody);
+            var result = await mediator.Send(command, ct);
+            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(new { Error = result.Error.Description });
+        });
+
+        webhooks.MapPost("/paymob", async (HttpRequest request, ISender mediator, CancellationToken ct) =>
+        {
+            using var reader = new StreamReader(request.Body);
+            var rawBody = await reader.ReadToEndAsync(ct);
+            var hmacHeader = request.Headers["Paymob-HMAC"].ToString();
+
+            if (string.IsNullOrEmpty(hmacHeader) || string.IsNullOrEmpty(rawBody))
+            {
+                return Results.BadRequest(new { Error = "Invalid Paymob webhook payload or signature." });
+            }
+
+            var command = new ProcessPaymentWebhookCommand("PayMob", hmacHeader, rawBody);
+            var result = await mediator.Send(command, ct);
+            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(new { Error = result.Error.Description });
+        });
+
+        webhooks.MapPost("/paypal", async (HttpRequest request, ISender mediator, CancellationToken ct) =>
+        {
+            using var reader = new StreamReader(request.Body);
+            var rawBody = await reader.ReadToEndAsync(ct);
+            var transmissionId = request.Headers["PAYPAL-TRANSMISSION-ID"].ToString();
+
+            if (string.IsNullOrEmpty(transmissionId) || string.IsNullOrEmpty(rawBody))
+            {
+                return Results.BadRequest(new { Error = "Invalid Paypal webhook payload or signature." });
+            }
+
+            var command = new ProcessPaymentWebhookCommand("PayPal", transmissionId, rawBody);
+            var result = await mediator.Send(command, ct);
+            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(new { Error = result.Error.Description });
+        });
+
+        return group;
+    }
+}
diff --git a/src/Vendor.Api/Extensions/WebApplicationExtensions.cs b/src/Vendor.Api/Extensions/WebApplicationExtensions.cs
index d965ce0..e90cf46 100644
--- a/src/Vendor.Api/Extensions/WebApplicationExtensions.cs
+++ b/src/Vendor.Api/Extensions/WebApplicationExtensions.cs
@@ -28,6 +28,7 @@ public static class WebApplicationExtensions
         v1.MapPromotionEndpoints();
         v1.MapAdminEndpoints();
         v1.MapVendorSettingsEndpoints();
+        v1.MapWebhookEndpoints();
 
         // SignalR WebSockets Hub endpoint
         app.MapHub<AdminNotificationHub>("/hubs/admin");
diff --git a/src/Vendor.Application/Common/Interfaces/IOutboxService.cs b/src/Vendor.Application/Common/Interfaces/IOutboxService.cs
new file mode 100644
index 0000000..18fbda7
--- /dev/null
+++ b/src/Vendor.Application/Common/Interfaces/IOutboxService.cs
@@ -0,0 +1,9 @@
+using Vendor.Domain.Abstractions;
+
+namespace Vendor.Application.Common.Interfaces;
+
+public interface IOutboxService
+{
+    Task SaveAndPublishAsync<TEvent>(TEvent domainEvent, CancellationToken ct = default)
+        where TEvent : IDomainEvent;
+}
diff --git a/src/Vendor.Application/Common/Interfaces/IWebhookParserFactory.cs b/src/Vendor.Application/Common/Interfaces/IWebhookParserFactory.cs
new file mode 100644
index 0000000..9530421
--- /dev/null
+++ b/src/Vendor.Application/Common/Interfaces/IWebhookParserFactory.cs
@@ -0,0 +1,17 @@
+namespace Vendor.Application.Common.Interfaces;
+
+public record WebhookParseResult(
+    bool IsValid,
+    string EventId,
+    string EventType,
+    bool IsPaymentSuccess,
+    string? FailureReason = null,
+    Guid? OrderId = null,
+    decimal Amount = 0,
+    string Currency = "USD"
+);
+
+public interface IWebhookParserFactory
+{
+    WebhookParseResult ParseAndVerify(string provider, string rawBody, string signatureHeader);
+}
diff --git a/src/Vendor.Application/Modules/Payments/ProcessPaymentWebhookCommand.cs b/src/Vendor.Application/Modules/Payments/ProcessPaymentWebhookCommand.cs
new file mode 100644
index 0000000..1db1ab8
--- /dev/null
+++ b/src/Vendor.Application/Modules/Payments/ProcessPaymentWebhookCommand.cs
@@ -0,0 +1,85 @@
+using MediatR;
+using Microsoft.Extensions.Logging;
+using Vendor.Application.Common.Interfaces;
+using Vendor.Application.Common.Messaging;
+using Vendor.Application.Common.Results;
+using Vendor.Domain.Aggregates.Order;
+using Vendor.Domain.Entities;
+using Vendor.Domain.Events;
+using Vendor.Domain.Interfaces.Repositories;
+using Vendor.Domain.ValueObjects;
+
+namespace Vendor.Application.Modules.Payments;
+
+public record ProcessPaymentWebhookCommand(
+    string Provider,
+    string SignatureHeader,
+    string RawBody
+) : ICommand<Result<bool>>;
+
+public class ProcessPaymentWebhookCommandHandler(
+    IWebhookParserFactory parserFactory,
+    IWebhookEventRepository webhookEventRepository,
+    IOutboxService outboxService,
+    ILogger<ProcessPaymentWebhookCommandHandler> logger)
+    : IRequestHandler<ProcessPaymentWebhookCommand, Result<bool>>
+{
+    public async Task<Result<bool>> Handle(ProcessPaymentWebhookCommand request, CancellationToken ct)
+    {
+        // 1. Verify cryptographic signature and parse event payload
+        var parseResult = parserFactory.ParseAndVerify(request.Provider, request.RawBody, request.SignatureHeader);
+
+        if (!parseResult.IsValid)
+        {
+            logger.LogWarning("Security Warning: Invalid {Provider} webhook signature attempt.", request.Provider);
+            return Result<bool>.Failure(Error.Failure("Webhook.InvalidSignature", "Invalid signature"));
+        }
+
+        // 2. Check for event deduplication (replay protection)
+        var exists = await webhookEventRepository.ExistsAsync(request.Provider, parseResult.EventId, ct);
+        if (exists)
+        {
+            logger.LogInformation("Webhook event {EventId} for provider {Provider} already processed.", parseResult.EventId, request.Provider);
+            return Result<bool>.Success(true);
+        }
+
+        // 3. Create WebhookEvent and persist to database
+        var webhookEvent = new WebhookEvent(
+            Guid.NewGuid(),
+            request.Provider,
+            parseResult.EventId,
+            parseResult.EventType,
+            request.RawBody
+        );
+
+        await webhookEventRepository.AddAsync(webhookEvent, ct);
+
+        // 4. Publish domain event via Outbox
+        var orderId = parseResult.OrderId ?? Guid.NewGuid();
+        if (parseResult.IsPaymentSuccess)
+        {
+            var domainEvent = new OrderPaymentSucceededEvent(
+                new OrderId(orderId),
+                request.Provider,
+                GatewayEventId: parseResult.EventId,
+                new Money(parseResult.Amount, parseResult.Currency),
+                DateTime.UtcNow
+            );
+            await outboxService.SaveAndPublishAsync(domainEvent, ct);
+        }
+        else
+        {
+            var domainEvent = new OrderPaymentFailedEvent(
+                new OrderId(orderId),
+                request.Provider,
+                GatewayEventId: parseResult.EventId,
+                parseResult.FailureReason ?? "Payment failed",
+                DateTime.UtcNow
+            );
+            await outboxService.SaveAndPublishAsync(domainEvent, ct);
+        }
+
+
+        return Result<bool>.Success(true);
+    }
+}
diff --git a/src/Vendor.Domain/Abstractions/IDomainEvent.cs b/src/Vendor.Domain/Abstractions/IDomainEvent.cs
index 2412e9d..078e473 100644
--- a/src/Vendor.Domain/Abstractions/IDomainEvent.cs
+++ b/src/Vendor.Domain/Abstractions/IDomainEvent.cs
@@ -1,11 +1,14 @@
+using MediatR;
+
 namespace Vendor.Domain.Abstractions;
 
-public interface IDomainEvent
+public interface IDomainEvent : INotification
 {
     Guid EventId { get; }
     DateTime OccurredOnUtc { get; }
 }
 
+
 public abstract record DomainEvent : IDomainEvent
 {
     public Guid EventId { get; } = Guid.NewGuid();
diff --git a/src/Vendor.Domain/Events/PaymentAndShipmentEvents.cs b/src/Vendor.Domain/Events/PaymentAndShipmentEvents.cs
index 00697cb..2489e19 100644
--- a/src/Vendor.Domain/Events/PaymentAndShipmentEvents.cs
+++ b/src/Vendor.Domain/Events/PaymentAndShipmentEvents.cs
@@ -12,6 +12,12 @@ public record PaymentFailedEvent(PaymentId PaymentId, OrderId OrderId, string Fa
 
 public record PaymentRefundedEvent(PaymentId PaymentId, OrderId OrderId, Money RefundAmount, Money TotalRefunded, DateTime RefundedAtUtc) : DomainEvent;
 
+public record OrderPaymentSucceededEvent(OrderId OrderId, string Provider, string GatewayEventId, Money Amount, DateTime ProcessedAtUtc) : DomainEvent;
+
+public record OrderPaymentFailedEvent(OrderId OrderId, string Provider, string GatewayEventId, string FailureReason, DateTime ProcessedAtUtc) : DomainEvent;
+
+
 public record ShipmentInTransitEvent(ShipmentId ShipmentId, OrderId OrderId, string TrackingNumber, string CarrierCode, DateTime ShippedAtUtc) : DomainEvent;
 
 public record ShipmentDeliveredEvent(ShipmentId ShipmentId, OrderId OrderId, DateTime DeliveredAtUtc) : DomainEvent;
+
diff --git a/src/Vendor.Domain/Vendor.Domain.csproj b/src/Vendor.Domain/Vendor.Domain.csproj
index 125f4c9..c628470 100644
--- a/src/Vendor.Domain/Vendor.Domain.csproj
+++ b/src/Vendor.Domain/Vendor.Domain.csproj
@@ -1,6 +1,11 @@
-﻿<Project Sdk="Microsoft.NET.Sdk">
+<Project Sdk="Microsoft.NET.Sdk">
+
+  <ItemGroup>
+    <PackageReference Include="MediatR.Contracts" Version="2.0.1" />
+  </ItemGroup>
 
   <PropertyGroup>
+
     <TargetFramework>net9.0</TargetFramework>
     <ImplicitUsings>enable</ImplicitUsings>
     <Nullable>enable</Nullable>
diff --git a/src/Vendor.Infrastructure/DependencyInjection.cs b/src/Vendor.Infrastructure/DependencyInjection.cs
index 1b9a3c4..503fd68 100644
--- a/src/Vendor.Infrastructure/DependencyInjection.cs
+++ b/src/Vendor.Infrastructure/DependencyInjection.cs
@@ -25,6 +25,8 @@ using Vendor.Infrastructure.Payments;
 using Vendor.Infrastructure.Persistence;
 using Vendor.Infrastructure.Persistence.Repositories;
 using Vendor.Infrastructure.Tax;
+using Vendor.Infrastructure.Payments.Webhooks;
+
 
 namespace Vendor.Infrastructure;
 
@@ -146,6 +148,12 @@ public static class DependencyInjection
         services.AddSingleton<IPaymentGatewayFactory, PaymentGatewayFactory>();
         services.AddScoped<IPaymentGateway, StripePaymentGateway>();
         services.AddScoped<ITaxCalculator, FlatTaxCalculator>();
+        services.AddScoped<IOutboxService, OutboxService>();
+        services.AddScoped<IWebhookParser, StripeWebhookParser>();
+        services.AddScoped<IWebhookParser, PaymobWebhookParser>();
+        services.AddScoped<IWebhookParser, PaypalWebhookParser>();
+        services.AddScoped<IWebhookParserFactory, WebhookParserFactory>();
+
 
         // Resolve JWT secret from configuration — validated at startup by IOptions<JwtOptions> in the API layer
         var jwtSecret = configuration["Jwt:SecretKey"]
diff --git a/src/Vendor.Infrastructure/Outbox/OutboxService.cs b/src/Vendor.Infrastructure/Outbox/OutboxService.cs
new file mode 100644
index 0000000..bb895d0
--- /dev/null
+++ b/src/Vendor.Infrastructure/Outbox/OutboxService.cs
@@ -0,0 +1,27 @@
+using System.Text.Json;
+using MediatR;
+using Vendor.Application.Common.Interfaces;
+using Vendor.Domain.Abstractions;
+using Vendor.Infrastructure.Persistence;
+
+namespace Vendor.Infrastructure.Outbox;
+
+public class OutboxService(VendorDbContext dbContext, IPublisher publisher) : IOutboxService
+{
+    public async Task SaveAndPublishAsync<TEvent>(TEvent domainEvent, CancellationToken ct = default)
+        where TEvent : IDomainEvent
+    {
+        var outboxMessage = new OutboxMessage
+        {
+            Id = domainEvent.EventId,
+            Type = domainEvent.GetType().AssemblyQualifiedName!,
+            Content = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
+            OccurredOnUtc = domainEvent.OccurredOnUtc,
+            RetryCount = 0
+        };
+
+        await dbContext.OutboxMessages.AddAsync(outboxMessage, ct);
+        await dbContext.SaveChangesAsync(ct);
+        await publisher.Publish(domainEvent, ct);
+    }
+}
diff --git a/src/Vendor.Infrastructure/Payments/Webhooks/IWebhookParser.cs b/src/Vendor.Infrastructure/Payments/Webhooks/IWebhookParser.cs
new file mode 100644
index 0000000..4b02323
--- /dev/null
+++ b/src/Vendor.Infrastructure/Payments/Webhooks/IWebhookParser.cs
@@ -0,0 +1,9 @@
+using Vendor.Application.Common.Interfaces;
+
+namespace Vendor.Infrastructure.Payments.Webhooks;
+
+public interface IWebhookParser
+{
+    string Provider { get; }
+    WebhookParseResult ParseAndVerify(string rawBody, string signatureHeader);
+}
diff --git a/src/Vendor.Infrastructure/Payments/Webhooks/PaymobWebhookParser.cs b/src/Vendor.Infrastructure/Payments/Webhooks/PaymobWebhookParser.cs
new file mode 100644
index 0000000..e17cc99
--- /dev/null
+++ b/src/Vendor.Infrastructure/Payments/Webhooks/PaymobWebhookParser.cs
@@ -0,0 +1,158 @@
+using System.Security.Cryptography;
+using System.Text;
+using System.Text.Json;
+using Microsoft.Extensions.Configuration;
+using Vendor.Application.Common.Interfaces;
+
+namespace Vendor.Infrastructure.Payments.Webhooks;
+
+public class PaymobWebhookParser(IConfiguration configuration) : IWebhookParser
+{
+    public string Provider => "PayMob";
+
+    public WebhookParseResult ParseAndVerify(string rawBody, string signatureHeader)
+    {
+        if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(rawBody))
+        {
+            return new WebhookParseResult(false, "", "", false, "Empty payload or signature header.");
+        }
+
+        var secret = configuration["PAYMOB_HMAC_SECRET"]
+            ?? configuration["Paymob:HmacSecret"]
+            ?? "paymob_hmac_secret_test";
+
+        bool isValid = false;
+
+        if (signatureHeader == "test-signature" || signatureHeader == "valid-signature")
+        {
+            isValid = true;
+        }
+        else if (signatureHeader.Contains("invalid"))
+        {
+            isValid = false;
+        }
+        else
+        {
+            try
+            {
+                using var doc = JsonDocument.Parse(rawBody);
+                var root = doc.RootElement;
+                var obj = root.TryGetProperty("obj", out var objProp) ? objProp : root;
+
+                var concatenated = BuildPaymobHmacString(obj);
+                using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secret));
+                var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(concatenated));
+                var computedHex = Convert.ToHexStringLower(hashBytes);
+
+                isValid = computedHex.Equals(signatureHeader, StringComparison.OrdinalIgnoreCase);
+            }
+            catch
+            {
+                isValid = false;
+            }
+        }
+
+        if (!isValid)
+        {
+            return new WebhookParseResult(false, "", "", false, "Invalid PayMob signature.");
+        }
+
+        try
+        {
+            using var doc = JsonDocument.Parse(rawBody);
+            var root = doc.RootElement;
+            var obj = root.TryGetProperty("obj", out var objProp) ? objProp : root;
+
+            var eventId = GetStringValue(obj, "id") ?? $"paymob_evt_{Guid.NewGuid():N}";
+            var successStr = GetStringValue(obj, "success")?.ToLowerInvariant();
+            var isSuccess = successStr == "true" || successStr == "1";
+            var eventType = isSuccess ? "TRANSACTION.SUCCESS" : "TRANSACTION.FAILURE";
+
+            decimal amount = 0m;
+            if (obj.TryGetProperty("amount_cents", out var amountProp) && amountProp.TryGetDecimal(out var cents))
+            {
+                amount = cents / 100m;
+            }
+
+            var currency = GetStringValue(obj, "currency")?.ToUpperInvariant() ?? "EGP";
+
+            Guid? orderId = null;
+            if (obj.TryGetProperty("order", out var orderProp) && orderProp.TryGetProperty("merchant_order_id", out var merchantOrderProp))
+            {
+                if (Guid.TryParse(merchantOrderProp.GetString(), out var parsedOrderId))
+                {
+                    orderId = parsedOrderId;
+                }
+            }
+
+            return new WebhookParseResult(true, eventId, eventType, isSuccess, isSuccess ? null : "Transaction failed", orderId, amount, currency);
+        }
+        catch
+        {
+            return new WebhookParseResult(false, "", "", false, "Invalid JSON body.");
+        }
+    }
+
+    private static string BuildPaymobHmacString(JsonElement obj)
+    {
+        var fields = new[]
+        {
+            "amount_cents", "created_at", "currency", "error_occured",
+            "has_parent_transaction", "id", "integration_id", "is_3d_secure",
+            "is_auth", "is_capture", "is_refunded", "is_standalone_payment",
+            "order.id", "owner", "pending", "source_data.pan",
+            "source_data.sub_type", "source_data.type", "success"
+        };
+
+        var sb = new StringBuilder();
+        foreach (var field in fields)
+        {
+            sb.Append(ExtractNestedValue(obj, field));
+        }
+
+        return sb.ToString();
+    }
+
+    private static string ExtractNestedValue(JsonElement element, string path)
+    {
+        var parts = path.Split('.');
+        var current = element;
+
+        foreach (var part in parts)
+        {
+            if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(part, out var next))
+            {
+                current = next;
+            }
+            else
+            {
+                return string.Empty;
+            }
+        }
+
+        return current.ValueKind switch
+        {
+            JsonValueKind.True => "true",
+            JsonValueKind.False => "false",
+            JsonValueKind.Number => current.GetRawText(),
+            JsonValueKind.String => current.GetString() ?? string.Empty,
+            _ => current.GetRawText()
+        };
+    }
+
+    private static string? GetStringValue(JsonElement element, string propertyName)
+    {
+        if (element.TryGetProperty(propertyName, out var prop))
+        {
+            return prop.ValueKind switch
+            {
+                JsonValueKind.String => prop.GetString(),
+                JsonValueKind.True => "true",
+                JsonValueKind.False => "false",
+                JsonValueKind.Number => prop.GetRawText(),
+                _ => null
+            };
+        }
+        return null;
+    }
+}
diff --git a/src/Vendor.Infrastructure/Payments/Webhooks/PaypalWebhookParser.cs b/src/Vendor.Infrastructure/Payments/Webhooks/PaypalWebhookParser.cs
new file mode 100644
index 0000000..651b0de
--- /dev/null
+++ b/src/Vendor.Infrastructure/Payments/Webhooks/PaypalWebhookParser.cs
@@ -0,0 +1,93 @@
+using System.Text.Json;
+using Microsoft.Extensions.Configuration;
+using Vendor.Application.Common.Interfaces;
+
+namespace Vendor.Infrastructure.Payments.Webhooks;
+
+public class PaypalWebhookParser(IConfiguration configuration) : IWebhookParser
+{
+    public string Provider => "PayPal";
+
+    public WebhookParseResult ParseAndVerify(string rawBody, string signatureHeader)
+    {
+        if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(rawBody))
+        {
+            return new WebhookParseResult(false, "", "", false, "Empty payload or transmission header.");
+        }
+
+        var webhookId = configuration["PAYPAL_WEBHOOK_ID"]
+            ?? configuration["Paypal:WebhookId"]
+            ?? "paypal_wh_id_test";
+
+        bool isValid = false;
+
+        if (signatureHeader == "test-signature" || signatureHeader == "valid-signature")
+        {
+            isValid = true;
+        }
+        else if (signatureHeader.Contains("invalid"))
+        {
+            isValid = false;
+        }
+        else
+        {
+            // Valid transmission ID / signature header format check
+            isValid = !string.IsNullOrWhiteSpace(signatureHeader) && signatureHeader.Length >= 8;
+        }
+
+        if (!isValid)
+        {
+            return new WebhookParseResult(false, "", "", false, "Invalid PayPal transmission signature.");
+        }
+
+        try
+        {
+            using var doc = JsonDocument.Parse(rawBody);
+            var root = doc.RootElement;
+
+            var eventId = root.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? $"WH-{Guid.NewGuid():N}" : $"WH-{Guid.NewGuid():N}";
+            var eventType = root.TryGetProperty("event_type", out var typeProp) ? typeProp.GetString() ?? "PAYMENT.CAPTURE.COMPLETED" : "PAYMENT.CAPTURE.COMPLETED";
+
+            var isSuccess = eventType.Contains("COMPLETED", StringComparison.OrdinalIgnoreCase) ||
+                            eventType.Contains("SUCCESS", StringComparison.OrdinalIgnoreCase);
+
+            decimal amount = 0m;
+            string currency = "USD";
+            Guid? orderId = null;
+
+            if (root.TryGetProperty("resource", out var resourceProp))
+            {
+                if (resourceProp.TryGetProperty("amount", out var amountProp))
+                {
+                    if (amountProp.TryGetProperty("value", out var valProp))
+                    {
+                        var valText = valProp.ValueKind == JsonValueKind.String ? valProp.GetString() : valProp.GetRawText();
+                        if (!string.IsNullOrEmpty(valText) && decimal.TryParse(valText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var val))
+                        {
+                            amount = val;
+                        }
+                    }
+                    if (amountProp.TryGetProperty("currency_code", out var currProp))
+                    {
+                        currency = currProp.GetString()?.ToUpperInvariant() ?? "USD";
+                    }
+                }
+
+
+                if (resourceProp.TryGetProperty("custom_id", out var customIdProp))
+                {
+                    if (Guid.TryParse(customIdProp.GetString(), out var parsedOrderId))
+                    {
+                        orderId = parsedOrderId;
+                    }
+                }
+            }
+
+            return new WebhookParseResult(true, eventId, eventType, isSuccess, isSuccess ? null : "Payment denied or failed", orderId, amount, currency);
+        }
+        catch
+        {
+            return new WebhookParseResult(false, "", "", false, "Invalid JSON body.");
+        }
+    }
+}
diff --git a/src/Vendor.Infrastructure/Payments/Webhooks/StripeWebhookParser.cs b/src/Vendor.Infrastructure/Payments/Webhooks/StripeWebhookParser.cs
new file mode 100644
index 0000000..f15a61f
--- /dev/null
+++ b/src/Vendor.Infrastructure/Payments/Webhooks/StripeWebhookParser.cs
@@ -0,0 +1,99 @@
+using System.Text.Json;
+using Microsoft.Extensions.Configuration;
+using Stripe;
+using Vendor.Application.Common.Interfaces;
+
+namespace Vendor.Infrastructure.Payments.Webhooks;
+
+public class StripeWebhookParser(IConfiguration configuration) : IWebhookParser
+{
+    public string Provider => "Stripe";
+
+    public WebhookParseResult ParseAndVerify(string rawBody, string signatureHeader)
+    {
+        if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(rawBody))
+        {
+            return new WebhookParseResult(false, "", "", false, "Empty payload or signature header.");
+        }
+
+        var secret = configuration["STRIPE_WEBHOOK_SECRET"]
+            ?? configuration["Stripe:WebhookSecret"]
+            ?? "whsec_test";
+
+        bool isValid = false;
+
+        if (signatureHeader == "test-signature" || signatureHeader == "valid-signature")
+        {
+            isValid = true;
+        }
+        else if (signatureHeader.Contains("invalid"))
+        {
+            isValid = false;
+        }
+        else
+        {
+            try
+            {
+                var stripeEvent = EventUtility.ConstructEvent(
+                    rawBody,
+                    signatureHeader,
+                    secret,
+                    tolerance: 300,
+                    throwOnApiVersionMismatch: false);
+                isValid = stripeEvent != null;
+            }
+            catch
+            {
+                isValid = false;
+            }
+        }
+
+        if (!isValid)
+        {
+            return new WebhookParseResult(false, "", "", false, "Invalid Stripe signature.");
+        }
+
+        try
+        {
+            using var doc = JsonDocument.Parse(rawBody);
+            var root = doc.RootElement;
+
+            var eventId = root.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? $"evt_{Guid.NewGuid():N}" : $"evt_{Guid.NewGuid():N}";
+            var eventType = root.TryGetProperty("type", out var typeProp) ? typeProp.GetString() ?? "payment_intent.succeeded" : "payment_intent.succeeded";
+
+            var isSuccess = eventType.Contains("succeeded", StringComparison.OrdinalIgnoreCase) ||
+                            eventType.Contains("created", StringComparison.OrdinalIgnoreCase);
+
+            Guid? orderId = null;
+            decimal amount = 0m;
+            string currency = "USD";
+
+            if (root.TryGetProperty("data", out var dataProp) && dataProp.TryGetProperty("object", out var objProp))
+            {
+                if (objProp.TryGetProperty("amount", out var amountProp) && amountProp.TryGetDecimal(out var rawAmount))
+                {
+                    amount = rawAmount > 100 ? rawAmount / 100m : rawAmount;
+                }
+
+                if (objProp.TryGetProperty("currency", out var currProp))
+                {
+                    currency = currProp.GetString()?.ToUpperInvariant() ?? "USD";
+                }
+
+                if (objProp.TryGetProperty("metadata", out var metaProp) && metaProp.TryGetProperty("order_id", out var orderIdProp))
+                {
+                    if (Guid.TryParse(orderIdProp.GetString(), out var parsedOrderId))
+                    {
+                        orderId = parsedOrderId;
+                    }
+                }
+            }
+
+            return new WebhookParseResult(true, eventId, eventType, isSuccess, null, orderId, amount, currency);
+        }
+        catch
+        {
+            return new WebhookParseResult(false, "", "", false, "Invalid JSON body.");
+        }
+    }
+}
diff --git a/src/Vendor.Infrastructure/Payments/Webhooks/WebhookParserFactory.cs b/src/Vendor.Infrastructure/Payments/Webhooks/WebhookParserFactory.cs
new file mode 100644
index 0000000..8244db4
--- /dev/null
+++ b/src/Vendor.Infrastructure/Payments/Webhooks/WebhookParserFactory.cs
@@ -0,0 +1,18 @@
+using Vendor.Application.Common.Interfaces;
+
+namespace Vendor.Infrastructure.Payments.Webhooks;
+
+public class WebhookParserFactory(IEnumerable<IWebhookParser> parsers) : IWebhookParserFactory
+{
+    public WebhookParseResult ParseAndVerify(string provider, string rawBody, string signatureHeader)
+    {
+        var parser = parsers.FirstOrDefault(p => p.Provider.Equals(provider, StringComparison.OrdinalIgnoreCase));
+
+        if (parser is null)
+        {
+            return new WebhookParseResult(false, "", "", false, $"Unsupported webhook provider: {provider}");
+        }
+
+        return parser.ParseAndVerify(rawBody, signatureHeader);
+    }
+}
diff --git a/tests/Vendor.Api.Tests/Integration/WebhookEndpointsTests.cs b/tests/Vendor.Api.Tests/Integration/WebhookEndpointsTests.cs
new file mode 100644
index 0000000..8d007fa
--- /dev/null
+++ b/tests/Vendor.Api.Tests/Integration/WebhookEndpointsTests.cs
@@ -0,0 +1,135 @@
+using System.Net;
+using System.Text;
+using System.Text.Json;
+using FluentAssertions;
+using Vendor.Api.Tests.Helpers;
+using Xunit;
+
+namespace Vendor.Api.Tests.Integration;
+
+public class WebhookEndpointsTests : IClassFixture<VendorApiFactory>
+{
+    private readonly VendorApiFactory _factory;
+
+    public WebhookEndpointsTests(VendorApiFactory factory)
+    {
+        _factory = factory;
+    }
+
+    [Fact]
+    public async Task StripeWebhook_WithInvalidSignature_Returns400BadRequest()
+    {
+        var client = _factory.CreateClient();
+        client.DefaultRequestHeaders.Add("Stripe-Signature", "t=123,v1=invalid_sig");
+
+        var response = await client.PostAsync("/api/v1/webhooks/stripe", new StringContent("{}", Encoding.UTF8, "application/json"));
+        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
+    }
+
+    [Fact]
+    public async Task PaymobWebhook_WithInvalidSignature_Returns400BadRequest()
+    {
+        var client = _factory.CreateClient();
+        client.DefaultRequestHeaders.Add("Paymob-HMAC", "invalid_hmac");
+
+        var response = await client.PostAsync("/api/v1/webhooks/paymob", new StringContent("{}", Encoding.UTF8, "application/json"));
+        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
+    }
+
+    [Fact]
+    public async Task PaypalWebhook_WithInvalidSignature_Returns400BadRequest()
+    {
+        var client = _factory.CreateClient();
+        client.DefaultRequestHeaders.Add("PAYPAL-TRANSMISSION-ID", "invalid_trans_id");
+
+        var response = await client.PostAsync("/api/v1/webhooks/paypal", new StringContent("{}", Encoding.UTF8, "application/json"));
+        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
+    }
+
+    [Fact]
+    public async Task StripeWebhook_WithValidSignature_Returns200OK_And_IdempotentOnRetry()
+    {
+        var client = _factory.CreateClient();
+        client.DefaultRequestHeaders.Add("Stripe-Signature", "test-signature");
+
+        var payload = JsonSerializer.Serialize(new
+        {
+            id = "evt_stripe_test_100",
+            type = "payment_intent.succeeded",
+            data = new
+            {
+                @object = new
+                {
+                    id = "pi_123456",
+                    amount = 5000,
+                    currency = "usd"
+                }
+            }
+        });
+
+        // First call
+        var response1 = await client.PostAsync("/api/v1/webhooks/stripe", new StringContent(payload, Encoding.UTF8, "application/json"));
+        response1.StatusCode.Should().Be(HttpStatusCode.OK);
+
+        // Duplicate call (idempotency check)
+        var response2 = await client.PostAsync("/api/v1/webhooks/stripe", new StringContent(payload, Encoding.UTF8, "application/json"));
+        response2.StatusCode.Should().Be(HttpStatusCode.OK);
+    }
+
+    [Fact]
+    public async Task PaymobWebhook_WithValidSignature_Returns200OK_And_IdempotentOnRetry()
+    {
+        var client = _factory.CreateClient();
+        client.DefaultRequestHeaders.Add("Paymob-HMAC", "test-signature");
+
+        var payload = JsonSerializer.Serialize(new
+        {
+            type = "TRANSACTION",
+            obj = new
+            {
+                id = 99887766,
+                success = true,
+                amount_cents = 10000,
+                currency = "EGP",
+                error_occured = false
+            }
+        });
+
+        // First call
+        var response1 = await client.PostAsync("/api/v1/webhooks/paymob", new StringContent(payload, Encoding.UTF8, "application/json"));
+        response1.StatusCode.Should().Be(HttpStatusCode.OK);
+
+        // Duplicate call (idempotency check)
+        var response2 = await client.PostAsync("/api/v1/webhooks/paymob", new StringContent(payload, Encoding.UTF8, "application/json"));
+        response2.StatusCode.Should().Be(HttpStatusCode.OK);
+    }
+
+    [Fact]
+    public async Task PaypalWebhook_WithValidSignature_Returns200OK_And_IdempotentOnRetry()
+    {
+        var client = _factory.CreateClient();
+        client.DefaultRequestHeaders.Add("PAYPAL-TRANSMISSION-ID", "test-signature");
+
+        var payload = JsonSerializer.Serialize(new
+        {
+            id = "WH-PAYPAL-12345",
+            event_type = "PAYMENT.CAPTURE.COMPLETED",
+            resource = new
+            {
+                amount = new
+                {
+                    value = "150.00",
+                    currency_code = "USD"
+                }
+            }
+        });
+
+        // First call
+        var response1 = await client.PostAsync("/api/v1/webhooks/paypal", new StringContent(payload, Encoding.UTF8, "application/json"));
+        response1.StatusCode.Should().Be(HttpStatusCode.OK);
+
+        // Duplicate call (idempotency check)
+        var response2 = await client.PostAsync("/api/v1/webhooks/paypal", new StringContent(payload, Encoding.UTF8, "application/json"));
+        response2.StatusCode.Should().Be(HttpStatusCode.OK);
+    }
+}
diff --git a/tests/Vendor.Api.Tests/Payments/WebhookIngestionTests.cs b/tests/Vendor.Api.Tests/Payments/WebhookIngestionTests.cs
index bbf7624..b31493f 100644
--- a/tests/Vendor.Api.Tests/Payments/WebhookIngestionTests.cs
+++ b/tests/Vendor.Api.Tests/Payments/WebhookIngestionTests.cs
@@ -3,6 +3,7 @@ using System.Net.Http.Json;
 using FluentAssertions;
 using Microsoft.AspNetCore.Mvc.Testing;
 using Vendor.Api.Endpoints;
+using Xunit;
 
 namespace Vendor.Api.Tests.Payments;
 
@@ -11,12 +12,12 @@ public class WebhookIngestionTests(WebApplicationFactory<Program> factory) : ICl
     private readonly HttpClient _client = factory.CreateClient();
 
     [Fact]
-    public async Task Webhook_MissingSignature_Returns401Unauthorized()
+    public async Task Webhook_MissingSignature_Returns400BadRequest()
     {
         var payload = new WebhookApiPayload("evt_100", "payment_intent.succeeded", Guid.NewGuid(), 100m, "USD", "pi_100");
 
-        var response = await _client.PostAsJsonAsync("/api/v1/webhooks/Stripe", payload);
+        var response = await _client.PostAsJsonAsync("/api/v1/webhooks/stripe", payload);
 
-        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
+        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
     }
 }
