using Ledger.Idempotency;

namespace Ledger.Idempotency.Tests;

public class CanonicalJsonTests
{
    [Fact]
    public void Same_Content_Different_Key_Order_Produces_Same_Hash()
    {
        var a = """{"amount": 100, "currency": "BRL"}""";
        var b = """{"currency": "BRL", "amount": 100}""";

        Assert.Equal(CanonicalJson.Sha256Hash(a), CanonicalJson.Sha256Hash(b));
    }

    [Fact]
    public void Different_Content_Produces_Different_Hash()
    {
        var a = """{"amount": 100}""";
        var b = """{"amount": 200}""";

        Assert.NotEqual(CanonicalJson.Sha256Hash(a), CanonicalJson.Sha256Hash(b));
    }

    [Fact]
    public void Nested_Objects_Are_Canonicalized_Recursively()
    {
        var a = """{"outer": {"a": 1, "b": 2}}""";
        var b = """{"outer": {"b": 2, "a": 1}}""";

        Assert.Equal(CanonicalJson.Sha256Hash(a), CanonicalJson.Sha256Hash(b));
    }

    [Fact]
    public void Arrays_Preserve_Order()
    {
        var a = """{"list": [1, 2, 3]}""";
        var b = """{"list": [3, 2, 1]}""";

        Assert.NotEqual(CanonicalJson.Sha256Hash(a), CanonicalJson.Sha256Hash(b));
    }

    [Fact]
    public void Hash_Is_64_Char_Hex_Lowercase()
    {
        var hash = CanonicalJson.Sha256Hash("""{"a":1}""");
        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9a-f]+$", hash);
    }
}
