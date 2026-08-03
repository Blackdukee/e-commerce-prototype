using MediatR;
using Vendor.Application.Common.Interfaces;
using Vendor.Application.Common.Models;
using Vendor.Domain.Events;
using Vendor.Domain.Interfaces.Repositories;

namespace Vendor.Application.Modules.Products;

public class ProductIndexedEventHandler(
    IProductSearchService searchService,
    IProductRepository productRepository) : INotificationHandler<ProductActivatedEvent>
{
    public async Task Handle(ProductActivatedEvent notification, CancellationToken cancellationToken)
    {
        var product = await productRepository.GetByIdAsync(notification.ProductId, cancellationToken);
        if (product is null) return;

        var doc = new ProductSearchDoc(
            product.Id.Value.ToString(),
            product.Name,
            product.Slug.Value,
            product.Description,
            product.BasePrice.Amount,
            product.BasePrice.Currency,
            product.Status.ToString(),
            product.CreatedAtUtc);

        await searchService.IndexProductAsync(doc, cancellationToken);
    }
}
