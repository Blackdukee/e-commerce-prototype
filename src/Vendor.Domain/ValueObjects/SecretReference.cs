using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Vendor.Domain.Enums;

namespace Vendor.Domain.ValueObjects;

[JsonConverter(typeof(SecretReferenceJsonConverter))]
public sealed record SecretReference
{
    private static readonly Regex RefPattern = new(@"^ref:(env|vault|aws-ssm):(.+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string RawReference { get; }
    public SecretBackend Backend { get; }
    public string Path { get; }

    public SecretReference(string rawReference)
    {
        if (string.IsNullOrWhiteSpace(rawReference))
        {
            throw new ArgumentException("Secret reference string cannot be null or empty.", nameof(rawReference));
        }

        var match = RefPattern.Match(rawReference);
        if (!match.Success)
        {
            throw new ArgumentException(
                $"Invalid secret reference format '{rawReference}'. Secret references must use the format ref:env:VAR, ref:vault:path, or ref:aws-ssm:path.",
                nameof(rawReference));
        }

        RawReference = rawReference;
        var backendStr = match.Groups[1].Value.ToLowerInvariant();
        Backend = backendStr switch
        {
            "env" => SecretBackend.Env,
            "vault" => SecretBackend.Vault,
            "aws-ssm" => SecretBackend.AwsSsm,
            _ => throw new ArgumentOutOfRangeException(nameof(rawReference), $"Unsupported secret backend '{backendStr}'.")
        };
        Path = match.Groups[2].Value;
    }

    public static SecretReference Create(string rawReference) => new(rawReference);

    public override string ToString() => "ref:***";
}

public class SecretReferenceJsonConverter : JsonConverter<SecretReference>
{
    public override SecretReference? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString();
        return string.IsNullOrWhiteSpace(str) ? null : new SecretReference(str.StartsWith("ref:***") ? "ref:env:MASKED" : str);
    }

    public override void Write(Utf8JsonWriter writer, SecretReference value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
