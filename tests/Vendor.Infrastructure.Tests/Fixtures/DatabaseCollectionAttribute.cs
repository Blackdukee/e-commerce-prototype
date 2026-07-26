using Xunit;

namespace Vendor.Infrastructure.Tests.Fixtures;

[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<MsSqlFixture>
{
}
