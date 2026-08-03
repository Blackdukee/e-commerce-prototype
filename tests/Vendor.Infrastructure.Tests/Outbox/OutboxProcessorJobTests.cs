using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Vendor.Domain.Abstractions;
using Vendor.Infrastructure.Outbox;
using Vendor.Infrastructure.Persistence;
using Xunit;

namespace Vendor.Infrastructure.Tests.Outbox;

public class OutboxProcessorJobTests
{
    private static VendorDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<VendorDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new VendorDbContext(options);
    }

    public record TestDomainEvent(Guid Id) : DomainEvent;

    [Fact]
    public async Task ProcessOutboxMessagesAsync_DispatchesEvents_And_MarksProcessed()
    {
        using var context = CreateInMemoryDbContext();
        var publisherMock = new Mock<IPublisher>();

        var evt = new TestDomainEvent(Guid.NewGuid());
        var json = System.Text.Json.JsonSerializer.Serialize(evt, evt.GetType());

        var message = new OutboxMessage(
            Guid.NewGuid(),
            evt.GetType().AssemblyQualifiedName!,
            json,
            DateTime.UtcNow);

        await context.OutboxMessages.AddAsync(message);
        await context.SaveChangesAsync();

        var job = new OutboxProcessorJob(context, publisherMock.Object);
        await job.ProcessOutboxMessagesAsync(CancellationToken.None);

        var updated = await context.OutboxMessages.FindAsync(message.Id);
        Assert.NotNull(updated);
        Assert.Equal(OutboxMessageStatus.Processed, updated.Status);
        Assert.NotNull(updated.ProcessedAtUtc);
        publisherMock.Verify(p => p.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessOutboxMessagesAsync_WhenTypeNotFound_MarksAsFailed()
    {
        using var context = CreateInMemoryDbContext();
        var publisherMock = new Mock<IPublisher>();

        var message = new OutboxMessage(
            Guid.NewGuid(),
            "NonExistentType, NonExistentAssembly",
            "{}",
            DateTime.UtcNow);

        await context.OutboxMessages.AddAsync(message);
        await context.SaveChangesAsync();

        var job = new OutboxProcessorJob(context, publisherMock.Object);
        await job.ProcessOutboxMessagesAsync(CancellationToken.None);

        var updated = await context.OutboxMessages.FindAsync(message.Id);
        Assert.NotNull(updated);
        Assert.Equal(1, updated.RetryCount);
        Assert.Contains("could not be loaded", updated.Error);
    }

    [Fact]
    public async Task ProcessOutboxMessagesAsync_WhenPublishingThrows_IncrementsRetryCountAndSetsError()
    {
        using var context = CreateInMemoryDbContext();
        var publisherMock = new Mock<IPublisher>();

        var evt = new TestDomainEvent(Guid.NewGuid());
        var json = System.Text.Json.JsonSerializer.Serialize(evt, evt.GetType());

        var message = new OutboxMessage(
            Guid.NewGuid(),
            evt.GetType().AssemblyQualifiedName!,
            json,
            DateTime.UtcNow);

        await context.OutboxMessages.AddAsync(message);
        await context.SaveChangesAsync();

        publisherMock
            .Setup(p => p.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Publishing failed"));

        var job = new OutboxProcessorJob(context, publisherMock.Object);
        await job.ProcessOutboxMessagesAsync(CancellationToken.None);

        var updated = await context.OutboxMessages.FindAsync(message.Id);
        Assert.NotNull(updated);
        Assert.Equal(1, updated.RetryCount);
        Assert.Equal("Publishing failed", updated.Error);
        Assert.Equal(OutboxMessageStatus.Pending, updated.Status);
    }

    [Fact]
    public async Task ProcessOutboxMessagesAsync_WhenRetryCountReaches5_MarksAsDeadLetter()
    {
        using var context = CreateInMemoryDbContext();
        var publisherMock = new Mock<IPublisher>();

        var evt = new TestDomainEvent(Guid.NewGuid());
        var json = System.Text.Json.JsonSerializer.Serialize(evt, evt.GetType());

        var message = new OutboxMessage(
            Guid.NewGuid(),
            evt.GetType().AssemblyQualifiedName!,
            json,
            DateTime.UtcNow)
        {
            RetryCount = 4
        };

        await context.OutboxMessages.AddAsync(message);
        await context.SaveChangesAsync();

        publisherMock
            .Setup(p => p.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Publishing failed again"));

        var job = new OutboxProcessorJob(context, publisherMock.Object);
        await job.ProcessOutboxMessagesAsync(CancellationToken.None);

        var updated = await context.OutboxMessages.FindAsync(message.Id);
        Assert.NotNull(updated);
        Assert.Equal(5, updated.RetryCount);
        Assert.Equal(OutboxMessageStatus.DeadLetter, updated.Status);
    }
}
