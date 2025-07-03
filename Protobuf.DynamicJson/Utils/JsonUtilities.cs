using System.Collections;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Protobuf.DynamicJson.Utils;

/// <summary>
/// Provides helper methods for converting between System.Text.Json JsonNode trees and
/// CLR data structures. Includes functionality to transform a JsonNode into a
/// case-insensitive Dictionary&lt;string, object?&gt; (recursively handling nested objects, arrays,
/// and scalar types), as well as to construct a JsonNode from arbitrary CLR values.
/// </summary>
internal static class JsonUtilities
{
    const int DefaultMaxDepth = 256;

    /// <summary>
    /// Recursively converts a <see cref="JsonNode"/> (expected to be a <see cref="JsonObject"/>)
    /// into a <see cref="Dictionary{String, Object}"/>. Limits recursion to a specified maximum depth
    /// to prevent stack overflows or infinite loops.
    /// </summary>
    /// <param name="node">The JSON node to convert</param>
    /// <param name="maxDepth">The maximum depth of nested objects to process</param>
    /// <returns>Dictionary representation of the JSON node</returns>
    public static Dictionary<string, object?> ConvertJsonToDictionary(JsonNode? node, int maxDepth = DefaultMaxDepth)
    {
        // If the node is not an object or we've exceeded max depth, return an empty dictionary
        if (node is not JsonObject obj || maxDepth <= 0)
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        // Preallocate dictionary with case-insensitive keys
        var dict = new Dictionary<string, object?>(obj.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, valueNode) in obj)
        {
            // Recurse into each value, reducing depth by one
            dict[key] = ConvertNode(valueNode, maxDepth - 1);
        }

        return dict;
    }

    /// <summary>
    /// Converts a CLR object (scalar, byte[], IDictionary, IEnumerable, or other) into a JsonNode.
    /// Handles base types, arrays/lists, and nested dictionaries.
    /// </summary>
    /// <param name="value">The value to convert</param>
    /// <returns>The JSON node</returns>
    public static JsonNode? ToJsonNode(object? value)
        => value switch
        {
            null => null,
            string s => JsonValue.Create(s),
            bool b => JsonValue.Create(b),
            int i => JsonValue.Create(i),
            uint u => JsonValue.Create(u),
            long l => JsonValue.Create(l),
            ulong ul => JsonValue.Create(ul),
            float f => JsonValue.Create(f),
            double d => JsonValue.Create(d),
            decimal m => JsonValue.Create(m),
            // Byte arrays are base64‐encoded as JSON strings
            byte[] bytes => JsonValue.Create(Convert.ToBase64String(bytes)),
            // Nested dictionaries become JsonObject with recursive calls
            IDictionary<string, object?> map => new JsonObject(map.ToDictionary(k => k.Key, v => ToJsonNode(v.Value), StringComparer.Ordinal)),
            // Enumerables become JsonArray of elements via recursion
            IEnumerable list => new JsonArray(list.Cast<object?>().Select(ToJsonNode).ToArray()),
            // Fallback: call ToString() and wrap in a JSON string
            _ => JsonValue.Create(value.ToString())
        };

    /// <summary>
    /// Internal helper to dispatch based on JsonNode type.
    /// Prunes nodes when depthLeft ≤ 0 by letting ConvertJsonToDictionary return empty dictionaries.
    /// </summary>
    /// <param name="node">The JSON node to convert</param>
    /// <param name="depthLeft">The maximum depth of nested objects to process</param>
    /// <returns>The converted JSON node</returns>
    static object? ConvertNode(JsonNode? node, int depthLeft)
    {
        return node switch
        {
            null => null,
            // JsonValue holds a primitive (string/number/bool/null)
            JsonValue v => ConvertValue(v),
            // JsonObject: recurse into ConvertJsonToDictionary
            JsonObject o => ConvertJsonToDictionary(o, depthLeft),
            // JsonArray: recurse into each element via ConvertArray
            JsonArray a => ConvertArray(a, depthLeft),
            // Fallback: return the raw JSON text
            _ => node.ToJsonString()
        };
    }

    /// <summary>
    /// Converts a JsonValue (primitive) into the best CLR type: string, number types, bool, or null.
    /// </summary>
    /// <param name="value">The JSON value to convert</param>
    /// <returns>The converted JSON value</returns>
    static object? ConvertValue(JsonValue value)
    {
        var el = value.GetValue<JsonElement>();
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => ParseNumber(el),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            // Other kinds (e.g., comments) return raw JSON text
            _ => el.ToString()
        };
    }

    /// <summary>
    /// Attempts to parse a JsonElement number into the smallest possible CLR numeric type.
    /// Falls back to double or raw text if unrepresentable.
    /// </summary>
    /// <param name="el">The JSON element to parse to a number</param>
    /// <returns>The parsed number</returns>
    static object ParseNumber(in JsonElement el)
    {
        if (el.TryGetInt32(out var i32))
        {
            return i32;
        }
        if (el.TryGetUInt32(out var u32))
        {
            return u32;
        }
        if (el.TryGetInt64(out var i64))
        {
            return i64;
        }
        if (el.TryGetUInt64(out var u64))
        {
            return u64;
        }
        if (el.TryGetDecimal(out var dec))
        {
            return dec;
        }

        // Fallback: attempt double parse, otherwise return raw JSON text
        var raw = el.GetRawText();
        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var dbl))
        {
            return dbl;
        }
        return raw;
    }

    /// <summary>
    /// Converts a JsonArray into a CLR object[] by recursively calling ConvertNode on each element.
    /// Depth is decremented per element to enforce pruning rules.
    /// </summary>
    /// <param name="arr">The JSON array to convert</param>
    /// <param name="depthLeft">The maximum depth of nested objects to process</param>
    /// <returns>The converted JSON array</returns>
    static object?[] ConvertArray(JsonArray arr, int depthLeft)
    {
        // Allocate an object[] of the same length
        var output = new object?[arr.Count];
        for (var i = 0; i < arr.Count; i++)
        {
            // Even if depthLeft ≤ 0, ConvertNode will prune nested objects to empty dictionaries
            output[i] = ConvertNode(arr[i], depthLeft - 1);
        }
        return output;
    }
}