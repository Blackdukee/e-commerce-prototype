using System.Text.Json;
using Vendor.Application.Common.Interfaces;
using Vendor.Domain.ValueObjects;

namespace Vendor.Infrastructure.Security.Resolvers;

public class VaultSecretResolver(HttpClient? httpClient = null, string? vaultAddress = null, string? vaultToken = null) : ISecretResolver
{
    private readonly EnvSecretResolver _fallback = new();

    public async Task<string> ResolveSecretAsync(SecretReference secretRef, CancellationToken ct = default)
    {
        if (httpClient is null || string.IsNullOrWhiteSpace(vaultAddress))
        {
            return await _fallback.ResolveSecretAsync(secretRef, ct);
        }

        try
        {
            var parts = secretRef.Path.Split('#');
            var path = parts[0];
            var key = parts.Length > 1 ? parts[1] : "value";

            using var request = new HttpRequestMessage(HttpMethod.Get, $"{vaultAddress.TrimEnd('/')}/v1/{path}");
            if (!string.IsNullOrWhiteSpace(vaultToken))
            {
                request.Headers.Add("X-Vault-Token", vaultToken);
            }

            var response = await httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                return await _fallback.ResolveSecretAsync(secretRef, ct);

            var body = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("data", out var dataEl) &&
                dataEl.TryGetProperty("data", out var innerData) &&
                innerData.TryGetProperty(key, out var val))
            {
                return val.GetString() ?? secretRef.RawReference;
            }
            return await _fallback.ResolveSecretAsync(secretRef, ct);
        }
        catch
        {
            return await _fallback.ResolveSecretAsync(secretRef, ct);
        }
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
