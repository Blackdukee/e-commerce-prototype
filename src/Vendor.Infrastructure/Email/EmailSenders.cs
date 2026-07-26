using MailKit.Net.Smtp;
using MimeKit;
using SendGrid;
using SendGrid.Helpers.Mail;
using Vendor.Domain.Aggregates.Customer;
using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Aggregates.ReturnRequest;
using Vendor.Domain.Interfaces.Adapters;

namespace Vendor.Infrastructure.Email;

public class SendGridEmailSender(string apiKey, string fromEmail, string fromName) : INotificationSender
{
    public async Task SendOrderConfirmationAsync(CustomerId customerId, OrderId orderId, string orderNumber, CancellationToken ct = default)
    {
        var client = new SendGridClient(apiKey);
        var from = new EmailAddress(fromEmail, fromName);
        var to = new EmailAddress("customer@example.com");
        var msg = MailHelper.CreateSingleEmail(from, to, $"Order Confirmation - #{orderNumber}", $"Thank you for your order #{orderNumber}.", $"Thank you for your order #{orderNumber}.");

        await client.SendEmailAsync(msg, ct);
    }

    public async Task SendShipmentNotificationAsync(CustomerId customerId, OrderId orderId, string trackingNumber, string carrierCode, CancellationToken ct = default)
    {
        var client = new SendGridClient(apiKey);
        var from = new EmailAddress(fromEmail, fromName);
        var to = new EmailAddress("customer@example.com");
        var msg = MailHelper.CreateSingleEmail(from, to, $"Shipment Update - {trackingNumber}", $"Your shipment is on the way via {carrierCode}. Tracking: {trackingNumber}", $"Your shipment is on the way via {carrierCode}. Tracking: {trackingNumber}");

        await client.SendEmailAsync(msg, ct);
    }

    public async Task SendReturnConfirmationAsync(CustomerId customerId, ReturnRequestId returnRequestId, CancellationToken ct = default)
    {
        var client = new SendGridClient(apiKey);
        var from = new EmailAddress(fromEmail, fromName);
        var to = new EmailAddress("customer@example.com");
        var msg = MailHelper.CreateSingleEmail(from, to, "Return Request Received", $"We received your return request #{returnRequestId.Value}.", $"We received your return request #{returnRequestId.Value}.");

        await client.SendEmailAsync(msg, ct);
    }
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
