using System.Globalization;
using System.Text.Json.Nodes;
using Protobuf.DynamicJson.Utils;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Protobuf.DynamicJson.Tests;

public sealed class JsonUtilitiesTests
{
    [Fact]
    public void ConvertJsonToDictionary_WithScalarValues_ReturnsClrScalars()
    {
        const string Json = """
        {
          "str"   : "hello",
          "i32"   : 123,
          "u32"   : 4294967295,
          "i64"   : 9223372036854775807,
          "u64"   : 18446744073709551615,
          "dbl"   : 3.14159,
          "dec"   : 79228162514264337593543950335,
          "true"  : true,
          "false" : false,
          "null"  : null
        }
        """;

        var dict = JsonUtilities.ConvertJsonToDictionary(JsonNode.Parse(Json));

        Assert.Equal("hello", dict["str"]);
        Assert.Equal(123, dict["i32"]);
        Assert.Equal(uint.MaxValue, dict["u32"]);
        Assert.Equal(long.MaxValue, dict["i64"]);
        Assert.Equal(ulong.MaxValue, dict["u64"]);
        Assert.Equal(3.14159m, Assert.IsType<decimal>(dict["dbl"]));
        Assert.Equal(decimal.Parse("79228162514264337593543950335", CultureInfo.InvariantCulture), dict["dec"]);
        Assert.True((bool)dict["true"]!);
        Assert.False((bool)dict["false"]!);
        Assert.Null(dict["null"]);
    }

    [Fact]
    public void ConvertJsonToDictionary_WithArray_ReturnsObjectArray()
    {
        const string Json = """
        { "arr": [ 1, "two", {"x":3} ] }
        """;
        var dict = JsonUtilities.ConvertJsonToDictionary(JsonNode.Parse(Json));

        var arr = Assert.IsType<object[]>(dict["arr"]);
        Assert.Equal(3, arr.Length);
        Assert.Equal(1, arr[0]);
        Assert.Equal("two", arr[1]);

        var nested = Assert.IsType<Dictionary<string, object?>>(arr[2]);
        Assert.Equal(3, nested["x"]);
    }

    [Fact]
    public void ConvertJsonToDictionary_WithNestedObjects_ParsesRecursively()
    {
        const string Json = """ { "a": { "b": { "c": 42 } } } """;
        var dict = JsonUtilities.ConvertJsonToDictionary(JsonNode.Parse(Json));

        var a = Assert.IsType<Dictionary<string, object?>>(dict["a"]);
        var b = Assert.IsType<Dictionary<string, object?>>(a["b"]);
        Assert.Equal(42, b["c"]);
    }

    [Fact]
    public void ConvertJsonToDictionary_WithMixedCaseKeys_IsCaseInsensitive()
    {
        const string Json = """ { "MiXeD": 1 } """;
        var dict = JsonUtilities.ConvertJsonToDictionary(JsonNode.Parse(Json));

        Assert.Equal(1, dict["mixed"]);
        Assert.Equal(1, dict["MIXED"]);
    }

    [Fact]
    public void ConvertJsonToDictionary_WhenDepthExceeded_PrunesNestedObjects()
    {
        const string Json = """ { "a": { "b": 1 } } """;

        var dict = JsonUtilities.ConvertJsonToDictionary(
            JsonNode.Parse(Json), maxDepth: 1);

        var a = Assert.IsType<Dictionary<string, object?>>(dict["a"]);
        Assert.Empty(a);                    // inner object pruned
    }

    [Theory]
    [InlineData("[1,2,3]")]
    [InlineData("\"just a string\"")]
    [InlineData("42")]
    public void ConvertJsonToDictionary_WhenRootIsNotObject_ReturnsEmptyDictionary(string json)
    {
        var dict = JsonUtilities.ConvertJsonToDictionary(JsonNode.Parse(json));
        Assert.Empty(dict);
    }

    [Fact]
    public void ConvertJsonToDictionary_WithNonInvariantCulture_ParsesNumbersInvariantly()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            var dict = JsonUtilities.ConvertJsonToDictionary(
                JsonNode.Parse("""{ "pi": 3.14 }"""));

            Assert.Equal(3.14m, Assert.IsType<decimal>(dict["pi"]));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void ConvertJsonToDictionary_WhenDepthExceededInsideArray_PrunesNestedObjects()
    {
        var json = """{ "a": [ { "b": 1 } ] }""";
        var dict = JsonUtilities.ConvertJsonToDictionary(JsonNode.Parse(json), maxDepth: 1);

        var topArr = Assert.IsType<object[]>(dict["a"]);
        var pruned = Assert.IsType<Dictionary<string, object?>>(topArr[0]);
        Assert.Empty(pruned);
    }

    [Fact]
    public void ToJsonNode_WithClrScalars_ReturnsCorrespondingJsonValues()
    {
        Assert.Equal("123", JsonUtilities.ToJsonNode(123)!.ToJsonString());
        Assert.Equal("true", JsonUtilities.ToJsonNode(true)!.ToJsonString());
        Assert.Equal("\"hi\"", JsonUtilities.ToJsonNode("hi")!.ToJsonString());

        // decimal is written as number, not string
        Assert.Equal("3.5", JsonUtilities.ToJsonNode(3.5m)!.ToJsonString());
    }

    [Fact]
    public void ToJsonNode_WithByteArray_EncodesBase64String()
    {
        var node = JsonUtilities.ToJsonNode(new byte[] { 1, 2, 3 })!;
        Assert.Equal("\"AQID\"", node.ToJsonString());   // "AQID" == base-64(0x01,0x02,0x03)
    }

    [Fact]
    public void ToJsonNode_WithDictionaryAndArray_RoundTripsToOriginalJson()
    {
        const string Json = """
                            { "nums":[1,2], "cfg":{ "enabled":true } }
                            """;

        // JSON → Dict
        var dict = JsonUtilities.ConvertJsonToDictionary(JsonNode.Parse(Json));

        // Dict → JsonNode
        var node = JsonUtilities.ToJsonNode(dict)!;

        // Compare structural equality (ignores whitespace / order)
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(Json)!, node));
    }

    [Fact]
    public void ConvertJsonToDictionary_WithHugeNumberBeyondUInt64_ProducesDecimal()
    {
        // bigger than UInt64 but within decimal range
        const string Json = """{ "huge": 184467440737095516160 }""";
        var dict = JsonUtilities.ConvertJsonToDictionary(JsonNode.Parse(Json));

        Assert.IsType<decimal>(dict["huge"]);
        Assert.Equal(184467440737095516160m, dict["huge"]);
    }

    [Fact]
    public void ToJsonNode_WithNull_ReturnsNull()
    {
        Assert.Null(JsonUtilities.ToJsonNode(null));
    }
}