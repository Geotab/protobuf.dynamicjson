using System.Globalization;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace Protobuf.DynamicJson.Utils;

/// <summary>
/// Provides serialization and deserialization support for Protobuf well-known types
/// (e.g., Timestamp, Duration, wrapper types).
/// Contains lookup dictionaries for converting between JSON literals and the corresponding
/// well-known Protobuf message binary representations, as well as reverse parsing.
/// </summary>
internal static class WellKnownTypes
{
    // Fully qualified names for well-known protobuf types
    const string TsFull = ".google.protobuf.Timestamp";
    const string DurFull = ".google.protobuf.Duration";
    const string StrFull = ".google.protobuf.StringValue";
    const string BytesFull = ".google.protobuf.BytesValue";
    const string BoolFull = ".google.protobuf.BoolValue";
    const string I32Full = ".google.protobuf.Int32Value";
    const string I64Full = ".google.protobuf.Int64Value";
    const string U32Full = ".google.protobuf.UInt32Value";
    const string U64Full = ".google.protobuf.UInt64Value";
    const string FltFull = ".google.protobuf.FloatValue";
    const string DblFull = ".google.protobuf.DoubleValue";

    /// <summary>
    /// Maps well-known type names to functions that serialize a canonical JSON literal into the
    /// corresponding protobuf binary payload.
    /// Keys: full type name (e.g. ".google.protobuf.Timestamp"),
    /// Values: Func that accepts the JSON string (without quotes) and returns the byte[] payload.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, Func<string, byte[]>>
        Serializers = new Dictionary<string, Func<string, byte[]>>(StringComparer.Ordinal)
        {
            // Timestamp: parse the JSON literal and return its binary representation
            [TsFull] = j => Timestamp.Parser.ParseJson($"\"{j}\"").ToByteArray(),
            // Duration: parse the JSON literal and return its binary representation
            [DurFull] = j => Duration.Parser.ParseJson($"\"{j}\"").ToByteArray(),

            // Wrapper types: use a generic helper to parse the scalar and wrap it
            [StrFull] = j => SerializeWrapper<StringValue, string>(j, s => s, v => new StringValue { Value = v }),
            [BytesFull] = j => SerializeWrapper<BytesValue, ByteString>(j, s => ByteString.CopyFrom(Convert.FromBase64String(s)),
                v => new BytesValue { Value = v }),
            [BoolFull] = j => SerializeWrapper<BoolValue, bool>(j, bool.Parse, v => new BoolValue { Value = v }),
            [I32Full] = j => SerializeWrapper<Int32Value, int>(j, s => int.Parse(s, CultureInfo.InvariantCulture),
                v => new Int32Value { Value = v }),
            [I64Full] = j => SerializeWrapper<Int64Value, long>(j, s => long.Parse(s, CultureInfo.InvariantCulture),
                v => new Int64Value { Value = v }),
            [U32Full] = j => SerializeWrapper<UInt32Value, uint>(j, s => uint.Parse(s, CultureInfo.InvariantCulture),
                v => new UInt32Value { Value = v }),
            [U64Full] = j => SerializeWrapper<UInt64Value, ulong>(j, s => ulong.Parse(s, CultureInfo.InvariantCulture),
                v => new UInt64Value { Value = v }),
            [FltFull] = j => SerializeWrapper<FloatValue, float>(j, s => float.Parse(s, CultureInfo.InvariantCulture),
                v => new FloatValue { Value = v }),
            [DblFull] = j => SerializeWrapper<DoubleValue, double>(j, s => double.Parse(s, CultureInfo.InvariantCulture),
                v => new DoubleValue { Value = v })
        };

    /// <summary>
    /// Maps well-known type names to functions that parse a binary payload into a canonical JSON literal.
    /// Keys: full type name (e.g. ".google.protobuf.Timestamp"),
    /// Values: Func that accepts the byte[] payload and returns the JSON string (without quotes).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, Func<ReadOnlyMemory<byte>, string>>
        Parsers = new Dictionary<string, Func<ReadOnlyMemory<byte>, string>>(StringComparer.Ordinal)
        {
            // Timestamp: format using JsonFormatter, then remove the surrounding quotes
            [TsFull] = b => JsonFormatter.Default.Format(Timestamp.Parser.ParseFrom(b.Span)).Trim('"'),
            // Duration: format using JsonFormatter, then remove the surrounding quotes
            [DurFull] = b => JsonFormatter.Default.Format(Duration.Parser.ParseFrom(b.Span)).Trim('"'),
            // Wrapper types: use a generic helper to merge from bytes and extract the scalar value
            [StrFull] = b => ParseWrapper<StringValue, string>(b, m => m.Value, s => s),
            [BytesFull] = b => ParseWrapper<BytesValue, ByteString>(b, m => m.Value, bs => Convert.ToBase64String(bs.Span)),
            [BoolFull] = b => ParseWrapper<BoolValue, bool>(b, m => m.Value, v => v.ToString(CultureInfo.InvariantCulture).ToLowerInvariant()),
            [I32Full] = b => ParseWrapper<Int32Value, int>(b, m => m.Value, v => v.ToString(CultureInfo.InvariantCulture)),
            [I64Full] = b => ParseWrapper<Int64Value, long>(b, m => m.Value, v => v.ToString(CultureInfo.InvariantCulture)),
            [U32Full] = b => ParseWrapper<UInt32Value, uint>(b, m => m.Value, v => v.ToString(CultureInfo.InvariantCulture)),
            [U64Full] = b => ParseWrapper<UInt64Value, ulong>(b, m => m.Value, v => v.ToString(CultureInfo.InvariantCulture)),
            [FltFull] = b => ParseWrapper<FloatValue, float>(b, m => m.Value, v => v.ToString("R", CultureInfo.InvariantCulture)),
            [DblFull] = b => ParseWrapper<DoubleValue, double>(b, m => m.Value, v => v.ToString("R", CultureInfo.InvariantCulture)),
        };

    /// <summary>
    /// Helper to serialize a wrapper type TWrapper (e.g. Int32Value) from its scalar value.
    /// </summary>
    /// <typeparam name="TWrapper">The protobuf wrapper message type</typeparam>
    /// <typeparam name="TScalar">The underlying scalar CLR type</typeparam>
    /// <param name="json">The JSON string representing the scalar (no quotes for wrappers)</param>
    /// <param name="parse">Function to parse the JSON string into TScalar</param>
    /// <param name="ctor">Function to construct a new TWrapper from TScalar</param>
    /// <returns>Byte array containing the serialized TWrapper message</returns>
    static byte[] SerializeWrapper<TWrapper, TScalar>(string json, Func<string, TScalar> parse, Func<TScalar, TWrapper> ctor)
        where TWrapper : IMessage
    {
        return ctor(parse(json)).ToByteArray();
    }

    /// <summary>
    /// Helper to parse a wrapper type TWrapper (e.g. BoolValue) from its binary payload.
    /// </summary>
    /// <typeparam name="TWrapper">The protobuf wrapper message type</typeparam>
    /// <typeparam name="TScalar">The underlying scalar CLR type</typeparam>
    /// <param name="bytes">The binary payload containing a TWrapper message</param>
    /// <param name="getValue">Function to extract the scalar value from a TWrapper instance</param>
    /// <param name="format">Function to convert TScalar to its JSON string representation</param>
    /// <returns>The JSON string representing the scalar (without quotes for wrappers)</returns>
    static string ParseWrapper<TWrapper, TScalar>(in ReadOnlyMemory<byte> bytes, Func<TWrapper, TScalar> getValue, Func<TScalar, string> format)
        where TWrapper : IMessage, new()
    {
        var msg = new TWrapper();
        msg.MergeFrom(bytes.Span);
        return format(getValue(msg));
    }
}