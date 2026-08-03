using Vendor.Application.Common.Interfaces;
using Vendor.Domain.Enums;
using Vendor.Domain.ValueObjects;

namespace Vendor.Infrastructure.Security.Resolvers;

public class CompositeSecretResolver : ISecretResolver
{
    private readonly EnvSecretResolver _envResolver = new();
    private readonly VaultSecretResolver _vaultResolver;
    private readonly AwsSsmSecretResolver _awsSsmResolver = new();

    public CompositeSecretResolver(VaultSecretResolver? vaultResolver = null)
    {
        _vaultResolver = vaultResolver ?? new VaultSecretResolver();
    }

    public Task<string> ResolveSecretAsync(SecretReference secretRef, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(secretRef);
        return secretRef.Backend switch
        {
            SecretBackend.Env => _envResolver.ResolveSecretAsync(secretRef, ct),
            SecretBackend.Vault => _vaultResolver.ResolveSecretAsync(secretRef, ct),
            SecretBackend.AwsSsm => _awsSsmResolver.ResolveSecretAsync(secretRef, ct),
            _ => Task.FromResult(secretRef.RawReference)
        };
    }

    public Task<string> ResolveSecretAsync(string rawReference, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawReference) || !rawReference.StartsWith("ref:"))
            return Task.FromResult(rawReference ?? "");

        try
        {
            var secretRef = new SecretReference(rawReference);
            return ResolveSecretAsync(secretRef, ct);
        }
        catch
        {
            return Task.FromResult(rawReference);
        }
    }
}
