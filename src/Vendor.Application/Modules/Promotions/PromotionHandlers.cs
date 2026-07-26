using MediatR;
using Vendor.Application.Common.Messaging;
using Vendor.Application.Common.Results;
using Vendor.Domain.Aggregates.Promotion;
using Vendor.Domain.Interfaces.Repositories;
using Vendor.Domain.ValueObjects;

namespace Vendor.Application.Modules.Promotions;

public record PromotionDto(Guid Id, string Code, string DiscountType, decimal DiscountValue, decimal? MaxDiscountAmount, DateTime StartUtc, DateTime EndUtc, int? MaxUsageCount, int CurrentUsageCount, bool IsActive)
{
    public static PromotionDto FromDomain(Promotion promo) => new(
        promo.Id.Value,
        promo.Code,
        promo.DiscountType.ToString(),
        promo.DiscountValue,
        promo.MaxDiscountAmount?.Amount,
        promo.Validity.StartUtc,
        promo.Validity.EndUtc,
        promo.MaxUsageCount,
        promo.CurrentUsageCount,
        promo.IsActive);
}

public record CreatePromotionCommand(string Code, string DiscountType, decimal DiscountValue, DateTime StartUtc, DateTime EndUtc, decimal? MaxDiscountAmount = null, int? MaxUsageCount = null) : ICommand<Result<PromotionDto>>;
public record UpdatePromotionCommand(Guid PromotionId, decimal DiscountValue, DateTime StartUtc, DateTime EndUtc, decimal? MaxDiscountAmount = null, int? MaxUsageCount = null) : ICommand<Result<PromotionDto>>;
public record ApplyPromotionCodeCommand(string Code, decimal SubtotalAmount, string Currency) : ICommand<Result<decimal>>, IIdempotentRequest<Result<decimal>>
{
    public string IdempotencyKey => $"CALC-PROMO-{Code}-{SubtotalAmount}";
}
public record RecordPromotionUsageCommand(Guid PromotionId) : ICommand<Result>;
public record DeactivatePromotionCommand(Guid PromotionId) : ICommand<Result>, IIdempotentRequest<Result>
{
    public string IdempotencyKey => $"DEACT-PROMO-{PromotionId}";
}

public record GetPromotionByIdQuery(Guid PromotionId) : IQuery<Result<PromotionDto>>;
public record GetPromotionByCodeQuery(string Code) : IQuery<Result<PromotionDto>>;
public record ListActivePromotionsQuery : IQuery<Result<IReadOnlyList<PromotionDto>>>;

public class CreatePromotionCommandHandler(IPromotionRepository promotionRepository) : IRequestHandler<CreatePromotionCommand, Result<PromotionDto>>
{
    public async Task<Result<PromotionDto>> Handle(CreatePromotionCommand request, CancellationToken ct)
    {
        if (await promotionRepository.GetByCodeAsync(request.Code, ct) != null)
        {
            return Error.Conflict("Promo.Exists", $"Promotion code '{request.Code}' already exists.");
        }

        var discountType = Enum.Parse<DiscountType>(request.DiscountType, true);
        var validity = new DateRange(request.StartUtc, request.EndUtc);
        Money? maxDiscount = request.MaxDiscountAmount.HasValue ? new Money(request.MaxDiscountAmount.Value, "USD") : null;

        var promo = new Promotion(PromotionId.New(), request.Code, discountType, request.DiscountValue, validity, maxDiscountAmount: maxDiscount, maxUsageCount: request.MaxUsageCount);
        await promotionRepository.AddAsync(promo, ct);

        return PromotionDto.FromDomain(promo);
    }
}
