using System.Collections;
using System.Globalization;
using System.Runtime.CompilerServices;
using Google.Protobuf.Reflection;
using ProtoBuf;
using Protobuf.DynamicJson.Descriptors;
using Protobuf.DynamicJson.Utils;
using ProtoBuf.Meta;

namespace Protobuf.DynamicJson.Converter;

/// <summary>
/// Provides functionality to write a CLR object graph (Dictionary&lt;string, object&gt;) into
/// protobuf wire-format by traversing fields and encoding them according to their protobuf types.
/// </summary>
public static partial class ProtobufJsonConverter
{
    /// <summary>
    /// Contains methods for serializing a dictionary (produced by <see cref="Reader"/>)
    /// into protobuf binary using the ProtoWriter API. Handles scalar, repeated, map, and nested message fields.
    /// </summary>
    public static class Writer
    {
        /// <summary>
        /// Serializes a CLR dictionary representing a protobuf message into binary wire format.
        /// </summary>
        /// <param name="state">The ProtoWriter state to write into.</param>
        /// <param name="data">Dictionary mapping field names to CLR values.</param>
        /// <param name="msgDesc">DescriptorProto for the top-level message.</param>
        /// <param name="rootFds">FileDescriptorSet containing all descriptors.</param>
        /// <param name="model">RuntimeTypeModel used by ProtoBuf.</param>
        internal static void WriteMessage(ref ProtoWriter.State state, Dictionary<string, object?> data, DescriptorProto msgDesc, FileDescriptorSet rootFds, RuntimeTypeModel model)
        {
            // Iterate through each field in the protobuf message descriptor
            foreach (var fieldDesc in msgDesc.Field)
            {
                // If the CLR dictionary does not contain this field or the value is null, skip
                if (!TryGetFieldValue(data, fieldDesc, out var value))
                {
                    continue;
                }

                // Determine the wire type for this field
                var wireType = GetWireType(fieldDesc.Type);

                // Handle repeated fields (including map entries)
                if (fieldDesc.Label == FieldDescriptorProto.Types.Label.Repeated)
                {
                    // Check if this repeated field is actually a map
                    if (IsMapField(fieldDesc, rootFds))
                    {
                        // Map fields are expected to be stored as IDictionary
                        if (value is not IDictionary mapDict)
                        {
                            continue;
                        }

                        // Look up the descriptor for the nested MapEntry type
                        var mapEntryDesc = DescriptorSetCache.GetMessage(rootFds, fieldDesc.TypeName)
                                           ?? throw new InvalidOperationException($"MapEntry '{fieldDesc.TypeName}' not found.");

                        // Use a recyclable memory stream per map entry to assemble the sub‐message
                        using var entryBuf = memoryStreamManager.GetStream();
                        foreach (DictionaryEntry kv in mapDict)
                        {
                            // Reset the buffer for each entry
                            entryBuf.SetLength(0);
                            // Create a new ProtoWriter state for the MapEntry sub‐message
                            var entryState = ProtoWriter.State.Create((Stream)entryBuf, model);
                            WriteMapEntry(ref entryState, kv.Key, kv.Value, mapEntryDesc, rootFds, model);
                            entryState.Flush();
                            // Write the outer field header (length‐delimited)
                            state.WriteFieldHeader(fieldDesc.Number, WireType.String);
                            // Write the serialized MapEntry bytes
                            state.WriteBytes(entryBuf.GetBuffer().AsSpan(0, (int)entryBuf.Length));
                        }
                    }
                    else
                    {
                        // Normal repeated field: expect a CLR IList
                        if (value is not IList list)
                        {
                            continue;
                        }
                        foreach (var item in list)
                        {
                            if (item is null)
                            {
                                continue;
                            }
                            // Write the field header for each element
                            state.WriteFieldHeader(fieldDesc.Number, wireType);
                            WriteFieldValue(ref state, item, fieldDesc, rootFds, model);
                        }
                    }
                }
                else
                {
                    // Singular field: write the header and then the value
                    state.WriteFieldHeader(fieldDesc.Number, wireType);
                    WriteFieldValue(ref state, value, fieldDesc, rootFds, model);
                }
            }
        }

        /// <summary>
        /// Writes a 32-bit unsigned integer as a varint directly to a stream.
        /// </summary>
        /// <param name="stream">The stream to write to</param>
        /// <param name="value">The value to encode as a varint</param>
        internal static void WriteVarint32(Stream stream, uint value)
        {
            while (value >= 0x80)
            {
                stream.WriteByte((byte)(value | 0x80));
                value >>= 7;
            }
            stream.WriteByte((byte)value);
        }

        /// <summary>
        /// Serializes a single map entry (key/value pair) into the nested MapEntry message.
        /// </summary>
        /// <param name="state">The ProtoWriter.State to write to</param>
        /// <param name="key">The key to write</param>
        /// <param name="value">The value to write</param>
        /// <param name="mapEntryDesc">The DescriptorProto for the map entry</param>
        /// <param name="rootFds">The FileDescriptorSet</param>
        /// <param name="model">The RuntimeTypeModel</param>
        static void WriteMapEntry(ref ProtoWriter.State state, object key, object? value, DescriptorProto mapEntryDesc, FileDescriptorSet rootFds, RuntimeTypeModel model)
        {
            foreach (var f in mapEntryDesc.Field)
            {
                switch (f.Number)
                {
                    case 1:
                        // Field number 1 is always the "key" in a MapEntry
                        state.WriteFieldHeader(1, GetWireType(f.Type));
                        WriteFieldValue(ref state, key, f, rootFds, model);
                        break;
                    case 2 when value is not null:
                        // Field number 2 is always the "value" in a MapEntry
                        state.WriteFieldHeader(2, GetWireType(f.Type));
                        WriteFieldValue(ref state, value, f, rootFds, model);
                        break;
                }
            }
        }

        /// <summary>
        /// Serializes a single field value based on its protobuf type.
        /// </summary>
        /// <param name="state">The ProtoWriter.State to write to</param>
        /// <param name="value">The value to write</param>
        /// <param name="field">The FieldDescriptorProto</param>
        /// <param name="rootFds">The FileDescriptorSet</param>
        /// <param name="model">The RuntimeTypeModel</param>
        static void WriteFieldValue(ref ProtoWriter.State state, object value, FieldDescriptorProto field, FileDescriptorSet rootFds, RuntimeTypeModel model)
        {
            switch (field.Type)
            {
                case FieldDescriptorProto.Types.Type.Double:
                    state.WriteDouble(Convert.ToDouble(value, CultureInfo.InvariantCulture));
                    break;
                case FieldDescriptorProto.Types.Type.Float:
                    state.WriteSingle(Convert.ToSingle(value, CultureInfo.InvariantCulture));
                    break;
                case FieldDescriptorProto.Types.Type.Int64:
                    state.WriteInt64(Convert.ToInt64(value, CultureInfo.InvariantCulture));
                    break;
                case FieldDescriptorProto.Types.Type.Uint64:
                    state.WriteUInt64(Convert.ToUInt64(value, CultureInfo.InvariantCulture));
                    break;
                case FieldDescriptorProto.Types.Type.Int32:
                    state.WriteInt32(Convert.ToInt32(value, CultureInfo.InvariantCulture));
                    break;
                case FieldDescriptorProto.Types.Type.Fixed64:
                    state.WriteUInt64(Convert.ToUInt64(value, CultureInfo.InvariantCulture));
                    break;
                case FieldDescriptorProto.Types.Type.Fixed32:
                    state.WriteUInt32(Convert.ToUInt32(value, CultureInfo.InvariantCulture));
                    break;
                case FieldDescriptorProto.Types.Type.Bool:
                    state.WriteBoolean(Convert.ToBoolean(value, CultureInfo.InvariantCulture));
                    break;
                case FieldDescriptorProto.Types.Type.String:
                    // Protobuf string is UTF-8, so just convert the CLR string
                    state.WriteString(Convert.ToString(value, CultureInfo.InvariantCulture)!);
                    break;
                case FieldDescriptorProto.Types.Type.Bytes:
                    // Bytes field may be provided as byte[] or base64 string
                    if (value is byte[] arr)
                    {
                        state.WriteBytes(arr);
                    }
                    else if (value is string b64)
                    {
                        state.WriteBytes(Convert.FromBase64String(b64));
                    }
                    else
                    {
                        throw new ArgumentException($"Invalid bytes field '{field.Name}'.");
                    }
                    break;
                case FieldDescriptorProto.Types.Type.Uint32:
                    state.WriteUInt32(Convert.ToUInt32(value, CultureInfo.InvariantCulture));
                    break;
                case FieldDescriptorProto.Types.Type.Sfixed32:
                    state.WriteInt32(Convert.ToInt32(value, CultureInfo.InvariantCulture));
                    break;
                case FieldDescriptorProto.Types.Type.Sfixed64:
                    state.WriteInt64(Convert.ToInt64(value, CultureInfo.InvariantCulture));
                    break;
                case FieldDescriptorProto.Types.Type.Sint32:
                    // ZigZag encoding for signed integers
                    state.WriteUInt32(ZigZag.Encode32(Convert.ToInt32(value, CultureInfo.InvariantCulture)));
                    break;
                case FieldDescriptorProto.Types.Type.Sint64:
                    state.WriteUInt64(ZigZag.Encode64(Convert.ToInt64(value, CultureInfo.InvariantCulture)));
                    break;
                case FieldDescriptorProto.Types.Type.Message:
                    // For nested messages, either handle a well-known type or recurse
                    if (value is string literal
                        && WellKnownTypes.Serializers.TryGetValue(field.TypeName, out var fn))
                    {
                        // Well-known type: use its specialized serializer
                        var wktBytes = fn(literal);
                        state.WriteBytes(wktBytes);
                        return;
                    }
                    else if (value is Dictionary<string, object?> nestedDict)
                    {
                        // Regular nested message: recursively write contents
                        var nestedDesc = DescriptorSetCache.GetMessage(rootFds, field.TypeName)
                                         ?? throw new InvalidOperationException($"Desc '{field.TypeName}' not found.");
                        WriteNestedPayload(ref state, nestedDict, nestedDesc, rootFds, model);
                        return;
                    }
                    else
                    {
                        throw new ArgumentException($"Unsupported message field '{field.Name}'.");
                    }
                case FieldDescriptorProto.Types.Type.Enum:
                    // Enum can be a string name or numeric value
                    var enumNumber = ResolveEnumValue(value, field, rootFds);
                    state.WriteInt32(enumNumber);
                    break;
                default:
                    throw new NotSupportedException($"Field type {field.Type} not supported.");
            }
        }

        /// <summary>
        /// Serializes a nested message by writing its length‐delimited payload.
        /// </summary>
        /// <param name="parentState">The ProtoWriter.State to write to</param>
        /// <param name="nestedData">The nested message to serialize</param>
        /// <param name="nestedDesc">The DescriptorProto</param>
        /// <param name="rootFds">The FileDescriptorSet</param>
        /// <param name="model">The RuntimeTypeModel</param>
        static void WriteNestedPayload(ref ProtoWriter.State parentState, Dictionary<string, object?> nestedData, DescriptorProto nestedDesc, FileDescriptorSet rootFds, RuntimeTypeModel model)
        {
            using var ms = memoryStreamManager.GetStream();
            var nestedState = ProtoWriter.State.Create((Stream)ms, model);
            WriteMessage(ref nestedState, nestedData, nestedDesc, rootFds, model);
            nestedState.Flush();
            // Write the raw bytes of the nested message
            parentState.WriteBytes(ms.GetBuffer().AsSpan(0, (int)ms.Length));
        }

        /// <summary>
        /// Determines if a field represents a protobuf map type.
        /// </summary>
        /// <param name="f">The FieldDescriptorProto</param>
        /// <param name="fds">The FileDescriptorSet</param>
        static bool IsMapField(FieldDescriptorProto f, FileDescriptorSet fds)
        {
            // Only message‐typed fields can represent a map
            if (f.Type != FieldDescriptorProto.Types.Type.Message)
            {
                return false;
            }
            // Look up the nested type—map entries always have MapEntry=true
            if (!DescriptorSetCache.TryGetMessage(fds, f.TypeName, out var nested))
            {
                return false;
            }
            return nested?.Options?.MapEntry ?? false;
        }

        /// <summary>
        /// Resolves an enum field value, accepting either the enum name (string) or numeric value.
        /// </summary>
        /// <param name="value">The enum field value to resolve</param>
        /// <param name="field">The FieldDescriptorProto</param>
        /// <param name="fds">The FileDescriptorSet</param>
        static int ResolveEnumValue(object value, FieldDescriptorProto field, FileDescriptorSet fds)
        {
            // If caller passed numeric already, convert directly
            if (value is not string literal)
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }

            // Otherwise, check cache for previously resolved (typeName,name) pairs
            if (enumNumberCache.TryGetValue((field.TypeName, literal), out var cached))
            {
                return cached;
            }

            // Look up the enum descriptor by full name
            var enumDesc = DescriptorSetCache.GetEnum(fds, field.TypeName)
                           ?? throw new InvalidOperationException($"Enum '{field.TypeName}' not found.");

            // Try to match by name
            if (enumDesc.Value.FirstOrDefault(ev => ev.Name == literal) is { Number: var num })
            {
                enumNumberCache[(field.TypeName, literal)] = num;
                return num;
            }

            // Fallback: parse the literal as an integer
            if (!int.TryParse(literal, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                throw new ArgumentException($"\"{literal}\" is not a valid enum for {field.TypeName}.");
            }

            enumNumberCache[(field.TypeName, literal)] = parsed;
            return parsed;
        }

        // Lookup table for mapping FieldDescriptorProto.Types.Type → WireType
        static readonly WireType[] wireLookup =
        [
            WireType.None, // 0  (unused)
            WireType.Fixed64, // 1  TYPE_DOUBLE
            WireType.Fixed32, // 2  TYPE_FLOAT
            WireType.Varint, // 3  TYPE_INT64
            WireType.Varint, // 4  TYPE_UINT64
            WireType.Varint, // 5  TYPE_INT32
            WireType.Fixed64, // 6  TYPE_FIXED64
            WireType.Fixed32, // 7  TYPE_FIXED32
            WireType.Varint, // 8  TYPE_BOOL
            WireType.String, // 9  TYPE_STRING
            WireType.StartGroup, // 10 TYPE_GROUP   (deprecated, keep slot)
            WireType.String, // 11 TYPE_MESSAGE
            WireType.String, // 12 TYPE_BYTES
            WireType.Varint, // 13 TYPE_UINT32
            WireType.Varint, // 14 TYPE_ENUM
            WireType.Fixed32, // 15 TYPE_SFIXED32
            WireType.Fixed64, // 16 TYPE_SFIXED64
            WireType.Varint, // 17 TYPE_SINT32
            WireType.Varint // 18 TYPE_SINT64
        ];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static WireType GetWireType(FieldDescriptorProto.Types.Type t) => wireLookup[(int)t];

        private static bool TryGetFieldValue(
            IDictionary<string, object?> data,
            FieldDescriptorProto field,
            out object? value)
        {
            // Prefer exact proto name
            if (data.TryGetValue(field.Name, out value) && value is not null) return true;

            // Optionally accept jsonName too
            if (Options.AcceptBothInputNames)
            {
                var jsonName = field.JsonName;
                if (!string.IsNullOrEmpty(jsonName) &&
                    data.TryGetValue(jsonName, out value) &&
                    value is not null) return true;
            }

            value = null;
            return false;
        }
    }
}