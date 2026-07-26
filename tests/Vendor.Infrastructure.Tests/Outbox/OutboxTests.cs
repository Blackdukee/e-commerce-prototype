using System.Text.Json;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Vendor.Domain.Aggregates.Product;
using Vendor.Domain.Events;
using Vendor.Domain.ValueObjects;
using Vendor.Infrastructure.Outbox;
using Vendor.Infrastructure.Persistence;
using Vendor.Infrastructure.Tests.Fixtures;

namespace Vendor.Infrastructure.Tests.Outbox;

[Collection("Database")]
public class OutboxTests : IAsyncLifetime
{
    private readonly MsSqlFixture _fixture;

    public OutboxTests(MsSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task OutboxProcessorHostedService_UnprocessedMessages_PublishesAndUpdatesProcessedOnUtc()
    {
        using var dbContext = new VendorDbContext(_fixture.DbContextOptions);
        var publisherMock = new Mock<IPublisher>();

        var domainEvent = new ProductActivatedEvent(ProductId.New(), "Sample Product", new Money(100m, "USD"));
        var outboxMessage = new OutboxMessage
        {
            Id = domainEvent.EventId,
            Type = domainEvent.GetType().AssemblyQualifiedName!,
            Content = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
            OccurredOnUtc = domainEvent.OccurredOnUtc,
            RetryCount = 0
        };

        dbContext.OutboxMessages.Add(outboxMessage);
        await dbContext.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddSingleton(dbContext);
        services.AddSingleton(publisherMock.Object);
        var provider = services.BuildServiceProvider();

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(provider);
        scopeFactoryMock.Setup(s => s.CreateScope()).Returns(scopeMock.Object);

        var service = new OutboxProcessorHostedService(scopeFactoryMock.Object, NullLogger<OutboxProcessorHostedService>.Instance);
        await service.ProcessNextBatchAsync(CancellationToken.None);

        publisherMock.Verify(p => p.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
        outboxMessage.ProcessedOnUtc.Should().NotBeNull();
    }
}
