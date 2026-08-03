using Elastic.Clients.Elasticsearch;
using FluentAssertions;
using Vendor.Infrastructure.Search;
using Xunit;

namespace Vendor.Infrastructure.Tests.Search;

public class ElasticsearchProductSearchServiceTests
{
    [Fact]
    public void Constructor_WithNullClient_ThrowsArgumentNullException()
    {
        var act = () => new ElasticsearchProductSearchService(null!, "products");
        act.Should().Throw<ArgumentNullException>().WithParameterName("client");
    }

    [Fact]
    public void Constructor_WithNullIndexName_ThrowsArgumentNullException()
    {
        var settings = new ElasticsearchClientSettings(new Uri("http://localhost:9200"));
        var client = new ElasticsearchClient(settings);
        var act = () => new ElasticsearchProductSearchService(client, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("indexName");
    }

    [Fact]
    public void Constructor_WithWhitespaceIndexName_ThrowsArgumentNullException()
    {
        var settings = new ElasticsearchClientSettings(new Uri("http://localhost:9200"));
        var client = new ElasticsearchClient(settings);
        var act = () => new ElasticsearchProductSearchService(client, "   ");
        act.Should().Throw<ArgumentNullException>().WithParameterName("indexName");
    }
}
