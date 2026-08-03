using Vendor.Domain.ValueObjects;

namespace Vendor.Application.Common.Interfaces;

public interface ISecretResolver
{
    Task<string> ResolveSecretAsync(SecretReference secretRef, CancellationToken ct = default);
    Task<string> ResolveSecretAsync(string rawReference, CancellationToken ct = default);
}
