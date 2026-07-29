using System.Net.Http.Headers;
using System.Net.Http.Json;
using MailKit.Net.Smtp;
using MimeKit;
using Vendor.Domain.Aggregates.Customer;
using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Aggregates.ReturnRequest;
using Vendor.Domain.Interfaces.Adapters;

namespace Vendor.Infrastructure.Email;

public class MailtrapEmailSender(HttpClient httpClient, string apiToken, string fromEmail, string fromName) : INotificationSender
{
    private async Task SendMailAsync(string subject, string body, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://send.api.mailtrap.io/api/send");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

        var payload = new
        {
            from = new { email = fromEmail, name = fromName },
            to = new[] { new { email = "customer@example.com" } },
            subject,
            text = body
        };

        request.Content = JsonContent.Create(payload);
        var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    public Task SendOrderConfirmationAsync(CustomerId customerId, OrderId orderId, string orderNumber, CancellationToken ct = default)
        => SendMailAsync($"Order Confirmation - #{orderNumber}", $"Thank you for your order #{orderNumber}.", ct);

    public Task SendShipmentNotificationAsync(CustomerId customerId, OrderId orderId, string trackingNumber, string carrierCode, CancellationToken ct = default)
        => SendMailAsync($"Shipment Update - {trackingNumber}", $"Your shipment is on the way via {carrierCode}. Tracking: {trackingNumber}", ct);

    public Task SendReturnConfirmationAsync(CustomerId customerId, ReturnRequestId returnRequestId, CancellationToken ct = default)
        => SendMailAsync("Return Request Received", $"We received your return request #{returnRequestId.Value}.", ct);
}

public class SmtpEmailSender(string host, int port, string username, string password, string fromEmail, string fromName) : INotificationSender
{
    private async Task SendMailAsync(string subject, string body, CancellationToken ct)
    {
        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(new MailboxAddress(fromName, fromEmail));
        mimeMessage.To.Add(new MailboxAddress("Customer", "customer@example.com"));
        mimeMessage.Subject = subject;
        mimeMessage.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        await client.ConnectAsync(host, port, MailKit.Security.SecureSocketOptions.Auto, ct);
        if (!string.IsNullOrEmpty(username))
        {
            await client.AuthenticateAsync(username, password, ct);
        }
        await client.SendAsync(mimeMessage, ct);
        await client.DisconnectAsync(true, ct);
    }

    public Task SendOrderConfirmationAsync(CustomerId customerId, OrderId orderId, string orderNumber, CancellationToken ct = default)
        => SendMailAsync($"Order Confirmation - #{orderNumber}", $"Thank you for your order #{orderNumber}.", ct);

    public Task SendShipmentNotificationAsync(CustomerId customerId, OrderId orderId, string trackingNumber, string carrierCode, CancellationToken ct = default)
        => SendMailAsync($"Shipment Update - {trackingNumber}", $"Your shipment is on the way via {carrierCode}. Tracking: {trackingNumber}", ct);

    public Task SendReturnConfirmationAsync(CustomerId customerId, ReturnRequestId returnRequestId, CancellationToken ct = default)
        => SendMailAsync("Return Request Received", $"We received your return request #{returnRequestId.Value}.", ct);
}
