using System;
using System.Text.RegularExpressions;

namespace Vendor.Domain.ValueObjects;

public readonly partial record struct Slug
{
    [GeneratedRegex("^[a-z0-9\\-]+$")]
    private static partial Regex SlugRegex();

    public string Value { get; }

    public Slug(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(value));
        var normalized = value.Trim().ToLowerInvariant();

        if (!SlugRegex().IsMatch(normalized))
        {
            throw new ArgumentException(
                $"Slug '{value}' is invalid. Only lowercase alphanumeric characters and hyphens are allowed.",
                nameof(value));
        }

        Value = normalized;
    }

    public override string ToString() => Value;
}
