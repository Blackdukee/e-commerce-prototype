using System.Net.Http.Headers;
using System.Net.Http.Json;
using MailKit.Net.Smtp;
using Mailtrap;
using Mailtrap.Source.Models;
using MimeKit;
using Vendor.Domain.Aggregates.Customer;
using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Aggregates.ReturnRequest;
using Vendor.Domain.Interfaces.Adapters;
using Vendor.Domain.Interfaces.Repositories;

namespace Vendor.Infrastructure.Email;

public class MailtrapEmailSender(string apiToken, string fromEmail, string fromName, ICustomerRepository? customerRepository = null) : INotificationSender
{
    private async Task<string> ResolveCustomerEmailAsync(CustomerId customerId, CancellationToken ct)
    {
        if (customerRepository != null)
        {
            var customer = await customerRepository.GetByIdAsync(customerId, ct);
            if (!string.IsNullOrWhiteSpace(customer?.Email))
            {
                return customer.Email;
            }
        }
        return "customer@example.com";
    }

    private async Task SendMailAsync(string toEmail, string subject, string body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(apiToken) || apiToken.StartsWith("ref:"))
        {
            System.Diagnostics.Debug.WriteLine($"[MailtrapEmailSender] Mailtrap API key unconfigured. Email subject: '{subject}' to: '{toEmail}'");
            return;
        }

        try
        {
            var senderEmail = string.IsNullOrWhiteSpace(fromEmail) || !fromEmail.Contains("@") ? "hello@demomailtrap.co" : fromEmail;
            var targetEmail = string.IsNullOrWhiteSpace(toEmail) ? "customer@example.com" : toEmail;

            var sender = new MailtrapSender("api", apiToken, 587);
            var mail = new Mailtrap.Source.Models.Email(targetEmail, senderEmail, subject, body, false);

            await sender.SendAsync(mail, ct);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MailtrapEmailSender] Exception while sending email: {ex.Message}");
        }
    }

    public async Task SendOrderConfirmationAsync(CustomerId customerId, OrderId orderId, string orderNumber, CancellationToken ct = default)
        => await SendMailAsync(await ResolveCustomerEmailAsync(customerId, ct), $"Order Confirmation - #{orderNumber}", $"Thank you for your order #{orderNumber}.", ct);

    public async Task SendShipmentNotificationAsync(CustomerId customerId, OrderId orderId, string trackingNumber, string carrierCode, CancellationToken ct = default)
        => await SendMailAsync(await ResolveCustomerEmailAsync(customerId, ct), $"Shipment Update - {trackingNumber}", $"Your shipment is on the way via {carrierCode}. Tracking: {trackingNumber}", ct);

    public async Task SendReturnConfirmationAsync(CustomerId customerId, ReturnRequestId returnRequestId, CancellationToken ct = default)
        => await SendMailAsync(await ResolveCustomerEmailAsync(customerId, ct), "Return Request Received", $"We received your return request #{returnRequestId.Value}.", ct);

    public Task SendPasswordResetAsync(string email, string token, CancellationToken ct = default)
        => SendMailAsync(email, "Password Reset Request", $"You requested a password reset. Use this token to reset your password: {token}", ct);
}

public class SmtpEmailSender(string host, int port, string username, string password, string fromEmail, string fromName, ICustomerRepository? customerRepository = null) : INotificationSender
{
    private async Task<string> ResolveCustomerEmailAsync(CustomerId customerId, CancellationToken ct)
    {
        if (customerRepository != null)
        {
            var customer = await customerRepository.GetByIdAsync(customerId, ct);
            if (!string.IsNullOrWhiteSpace(customer?.Email))
            {
                return customer.Email;
            }
        }
        return "customer@example.com";
    }

    private async Task SendMailAsync(string toEmail, string subject, string body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            System.Diagnostics.Debug.WriteLine($"[SmtpEmailSender] Host is unconfigured. Email subject: '{subject}' to: '{toEmail}'");
            return;
        }

        try
        {
            var mimeMessage = new MimeMessage();
            mimeMessage.From.Add(new MailboxAddress(string.IsNullOrWhiteSpace(fromName) ? "Vendor Store" : fromName, string.IsNullOrWhiteSpace(fromEmail) ? "noreply@vendor.com" : fromEmail));
            mimeMessage.To.Add(new MailboxAddress("Customer", string.IsNullOrWhiteSpace(toEmail) ? "customer@example.com" : toEmail));
            mimeMessage.Subject = subject;
            mimeMessage.Body = new TextPart("plain") { Text = body };

            using var client = new SmtpClient();
            if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) || string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) || port == 1025)
            {
                client.ServerCertificateValidationCallback = (s, c, h, e) => true;
            }
            await client.ConnectAsync(host, port, MailKit.Security.SecureSocketOptions.Auto, ct);
            if (!string.IsNullOrEmpty(username))
            {
                await client.AuthenticateAsync(username, password, ct);
            }
            await client.SendAsync(mimeMessage, ct);
            await client.DisconnectAsync(true, ct);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SmtpEmailSender] Exception while sending email: {ex.Message}");
        }
    }

    public async Task SendOrderConfirmationAsync(CustomerId customerId, OrderId orderId, string orderNumber, CancellationToken ct = default)
        => await SendMailAsync(await ResolveCustomerEmailAsync(customerId, ct), $"Order Confirmation - #{orderNumber}", $"Thank you for your order #{orderNumber}.", ct);

    public async Task SendShipmentNotificationAsync(CustomerId customerId, OrderId orderId, string trackingNumber, string carrierCode, CancellationToken ct = default)
        => await SendMailAsync(await ResolveCustomerEmailAsync(customerId, ct), $"Shipment Update - {trackingNumber}", $"Your shipment is on the way via {carrierCode}. Tracking: {trackingNumber}", ct);

    public async Task SendReturnConfirmationAsync(CustomerId customerId, ReturnRequestId returnRequestId, CancellationToken ct = default)
        => await SendMailAsync(await ResolveCustomerEmailAsync(customerId, ct), "Return Request Received", $"We received your return request #{returnRequestId.Value}.", ct);

    public Task SendPasswordResetAsync(string email, string token, CancellationToken ct = default)
        => SendMailAsync(email, "Password Reset Request", $"You requested a password reset. Use this token to reset your password: {token}", ct);
}
