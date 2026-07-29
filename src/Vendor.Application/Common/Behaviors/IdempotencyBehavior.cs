using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Vendor.Application.Common.Interfaces;
using Vendor.Application.Common.Messaging;
using Vendor.Application.Common.Results;
using Vendor.Application.Interfaces;
using Vendor.Domain.Aggregates.Payment;
using Vendor.Domain.Aggregates.Payment.Enums;
using Vendor.Domain.Interfaces.Repositories;

namespace Vendor.Application.Common.Behaviors;

public sealed class IdempotencyBehavior<TRequest, TResponse>(
    IIdempotencyStore idempotencyStore,
    IPaymentIdempotencyRepository? idempotencyRepository = null,
    IIdempotencyLockManager? lockManager = null)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IIdempotentRequest<TResponse>
    where TResponse : IResult
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            return await next();
        }

        // If repository and lock manager are not provided, fall back to simple store
        if (idempotencyRepository is null || lockManager is null)
        {
            var cached = await idempotencyStore.GetResultAsync<TResponse>(request.IdempotencyKey, cancellationToken);
            if (cached is not null)
            {
                return cached;
            }

            var res = await next();
            if (res.IsSuccess)
            {
                await idempotencyStore.SaveResultAsync(request.IdempotencyKey, res, cancellationToken);
            }
            return res;
        }

        // Advanced payment idempotency flow with UUID validation, lock, and hash matching
        if (!Guid.TryParse(request.IdempotencyKey, out var keyUuid) || keyUuid == Guid.Empty)
        {
            return ResultFactory.CreateFailure<TResponse>(Error.Failure("INVALID_IDEMPOTENCY_KEY", "The Idempotency-Key header must be a valid non-empty UUID v4."));
        }

        var requestHash = ComputeHash(request);

        using var lockRelease = await lockManager.AcquireLockAsync(keyUuid, TimeSpan.FromSeconds(10), cancellationToken);
        if (lockRelease is null)
        {
            return ResultFactory.CreateFailure<TResponse>(Error.Failure("IDEMPOTENCY_LOCK_TIMEOUT", "Concurrent request lock acquisition timed out."));
        }

        var existingKey = await idempotencyRepository.GetByKeyUuidAsync(keyUuid, cancellationToken);
        if (existingKey is not null)
        {
            if (!existingKey.MatchesHash(requestHash))
            {
                return ResultFactory.CreateFailure<TResponse>(Error.Failure(
                    "IDEMPOTENCY_PAYLOAD_MISMATCH",
                    $"The payload parameters for idempotency key '{keyUuid}' do not match the original request payload."));
            }

            if (existingKey.Status == IdempotencyStatus.Completed || existingKey.Status == IdempotencyStatus.Failed)
            {
                if (!string.IsNullOrEmpty(existingKey.ResponseBody))
                {
                    try
                    {
                        var deserialized = JsonSerializer.Deserialize<TResponse>(existingKey.ResponseBody);
                        if (deserialized is not null)
                        {
                            return deserialized;
                        }
                    }
                    catch
                    {
                        // Fallback if deserialization fails
                    }
                }
            }
        }
        else
        {
            existingKey = new PaymentIdempotencyKey(keyUuid, requestHash);
            await idempotencyRepository.AddAsync(existingKey, cancellationToken);
        }

        var response = await next();

        var responseJson = JsonSerializer.Serialize(response);
        if (response.IsSuccess)
        {
            existingKey.MarkCompleted(200, responseJson);
        }
        else
        {
            existingKey.MarkFailed(400, responseJson);
        }

        await idempotencyRepository.UpdateAsync(existingKey, cancellationToken);

        return response;
    }

    private static string ComputeHash(TRequest req)
    {
        var json = JsonSerializer.Serialize(req);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexStringLower(bytes);
    }
}
