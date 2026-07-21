using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Ledger.Idempotency;

public static class CanonicalJson
{
    public static string Sha256Hash(string jsonBody)
    {
        var node = JsonNode.Parse(jsonBody);
        var canonical = Canonicalize(node);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexStringLower(bytes);
    }

    private static string Canonicalize(JsonNode? node) => node switch
    {
        null => "null",
        JsonObject obj => "{" + string.Join(",", obj
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"\"{kv.Key}\":{Canonicalize(kv.Value)}")) + "}",
        JsonArray arr => "[" + string.Join(",", arr.Select(Canonicalize)) + "]",
        JsonValue v => v.ToJsonString(),
        _ => throw new InvalidOperationException($"Unsupported JsonNode type: {node.GetType()}")
    };
}
