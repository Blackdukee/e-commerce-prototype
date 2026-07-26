using System.Threading;
using System.Threading.Tasks;
using Vendor.Domain.ValueObjects;

namespace Vendor.Domain.Interfaces;

public interface ISecretResolver
{
    Task<string> ResolveAsync(SecretReference reference, CancellationToken cancellationToken = default);
}
