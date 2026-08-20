using System.Text.Json;
using System.Text.Json.Nodes;

namespace EnterpriseManagement.Infrastructure.Auditing;

/// <summary>
/// Strips credential-like values from audit metadata before it is stored.
/// </summary>
/// <remarks>
/// <para>
/// The audit log is the worst possible place to leak a credential: it is
/// deliberately long-lived, widely readable by administrators, and frequently
/// exported. "Do not pass passwords to the audit service" is a rule people
/// follow until the day someone logs a whole request object.
/// </para>
/// <para>
/// This enforces it structurally instead. Any property whose NAME suggests a
/// secret is replaced with a marker, at any depth. Matching on the key rather
/// than the value is deliberate: a hash, a token and a password are
/// indistinguishable as strings, but their field names are not.
/// </para>
/// <para>
/// This is a safety net, not a licence. Callers should still pass only what the
/// trail needs.
/// </para>
/// </remarks>
public static class AuditMetadataSanitiser
{
    public const string RedactedMarker = "[REDACTED]";

    /// <summary>
    /// Substrings that mark a property as sensitive. Matched case-insensitively
    /// anywhere in the name, so "PasswordHash", "newPassword" and
    /// "current_password" are all caught.
    /// </summary>
    private static readonly string[] SensitiveKeyFragments =
    [
        "password",
        "passwd",
        "secret",
        "token",
        "authorization",
        "apikey",
        "api_key",
        "credential",
        "privatekey",
        "private_key",
        "connectionstring",
        "salt",
        "hash"
    ];

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // Depth guard: a cyclic or absurdly nested object would otherwise turn
        // an audit write into a stack overflow.
        MaxDepth = 16
    };

    /// <summary>
    /// Serialises metadata to JSON with sensitive properties redacted.
    /// Returns null when there is nothing to record.
    /// </summary>
    public static string? Sanitise(object? metadata)
    {
        if (metadata is null)
        {
            return null;
        }

        JsonNode? node;

        try
        {
            node = JsonSerializer.SerializeToNode(metadata, SerializerOptions);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            // Metadata that cannot be serialised must not break the operation
            // being audited. Record that fact rather than throwing.
            return """{"error":"metadata could not be serialised"}""";
        }

        if (node is null)
        {
            return null;
        }

        Redact(node);

        return node.ToJsonString();
    }

    private static void Redact(JsonNode node)
    {
        switch (node)
        {
            case JsonObject obj:
                // Materialised first: the collection is modified while iterating.
                foreach (var property in obj.ToList())
                {
                    if (IsSensitive(property.Key))
                    {
                        obj[property.Key] = RedactedMarker;
                    }
                    else if (property.Value is not null)
                    {
                        Redact(property.Value);
                    }
                }
                break;

            case JsonArray array:
                foreach (var item in array.Where(i => i is not null))
                {
                    Redact(item!);
                }
                break;
        }
    }

    private static bool IsSensitive(string propertyName) =>
        SensitiveKeyFragments.Any(fragment =>
            propertyName.Contains(fragment, StringComparison.OrdinalIgnoreCase));
}
