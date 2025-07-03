using System.Globalization;
using Google.Protobuf.Reflection;
using ProtoBuf;
using Protobuf.DynamicJson.Descriptors;
using Protobuf.DynamicJson.Utils;

namespace Protobuf.DynamicJson.Converter;

/// <summary>
/// Provides functionality to read a protobuf binary stream and convert it into a
/// CLR object graph (Dictionary{String, Object}) suitable for JSON serialization.
/// </summary>
public static partial class ProtobufJsonConverter
{
    /// <summary>
    /// Contains methods for reading protobuf wire-format data and producing
    /// a dictionary of field names to CLR values.
    /// </summary>
    public static class Reader
    {
        /// <summary>
        /// Reads a top-level message from the given <see cref="ProtoReader.State"/> and
        /// returns a <see cref="Dictionary{String,Object}"/> mapping field names (case-insensitive)
        /// to their CLR representations. Handles scalar, repeated, map, and nested message fields.
        /// </summary>
        /// <param name="state">
        /// A <see cref="ProtoReader.State"/> positioned at the start of a length-delimited message.
        /// </param>
        /// <param name="msgDesc">
        /// The <see cref="DescriptorProto"/> for the message type being read.
        /// </param>
        /// <param name="fds">
        /// The <see cref="FileDescriptorSet"/> containing all message and enum descriptors.
        /// </param>
        /// <returns>
        /// A <see cref="Dictionary{String,Object}"/> whose keys are field names (case-insensitive)
        /// and whose values are the corresponding CLR values (scalars, arrays, nested dictionaries, etc.).
        /// </returns>
        internal static Dictionary<string, object?> ReadMessage(
            ref ProtoReader.State state,
            DescriptorProto msgDesc,
            FileDescriptorSet fds)
        {
            // Create a case-insensitive dictionary to hold field‐name → value mappings
            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            int fieldNumber;

            // Read each field from the wire until there are no more (header returns 0)
            while ((fieldNumber = state.ReadFieldHeader()) > 0)
            {
                // Look up the field descriptor by its tag number
                var field = msgDesc.Field.FirstOrDefault(f => f.Number == fieldNumber);
                if (field is null)
                {
                    // Unknown field: skip it and continue
                    state.SkipField();
                    continue;
                }

                // Read the field value based on its type
                var value = ReadFieldValue(ref state, field, fds);

                // Handle repeated (non-map) fields: accumulate into a list
                if (field.Label == FieldDescriptorProto.Types.Label.Repeated
                    && !IsMapField(field, fds))
                {
                    if (!dict.TryGetValue(field.Name, out var existing))
                    {
                        dict[field.Name] = existing = new List<object?>();
                    }
                    ((List<object?>)existing!).Add(value);
                }
                // Handle map fields: each entry is a sub‐message with "key" and "value"
                else if (IsMapField(field, fds))
                {
                    if (!dict.TryGetValue(field.Name, out var existing))
                    {
                        dict[field.Name] = existing = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    }

                    var map = (Dictionary<string, object?>)existing!;
                    var entry = (Dictionary<string, object?>)value!;
                    var rawKey = entry["key"];

                    // Convert the raw key to its canonical JSON string representation
                    var keyString = rawKey switch
                    {
                        null => string.Empty,
                        string s => s,
                        bool b => b ? "true" : "false",
                        byte[] bytes => Convert.ToBase64String(bytes),
                        IFormattable fmt => fmt.ToString(null, CultureInfo.InvariantCulture),
                        _ => rawKey.ToString()!
                    };

                    // Store the corresponding value under the stringified key
                    map[keyString] = entry["value"];
                }
                // Normal (singular) field: directly assign
                else
                {
                    dict[field.Name] = value;
                }
            }

            return dict;
        }

        /// <summary>
        /// Determines whether the given <paramref name="f"/> of type MESSAGE represents a
        /// protobuf <c>map&lt;K,V&gt;</c> field by checking if its nested message type has the
        /// <c>map_entry</c> option set to true.
        /// </summary>
        /// <param name="f">
        /// The <see cref="FieldDescriptorProto"/> to examine.
        /// </param>
        /// <param name="fds">
        /// The <see cref="FileDescriptorSet"/> containing the nested message descriptor.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if <paramref name="f"/> represents a map field; otherwise, <see langword="false"/>.
        /// </returns>
        static bool IsMapField(FieldDescriptorProto f, FileDescriptorSet fds)
        {
            // Only message‐typed fields can represent a map
            if (f.Type != FieldDescriptorProto.Types.Type.Message)
            {
                return false;
            }

            // Look up the nested message descriptor—map entries are always nested types
            if (!DescriptorSetCache.TryGetMessage(fds, f.TypeName, out var nested))
            {
                return false;
            }

            // A map field’s descriptor will have the MapEntry option set to true
            return nested?.Options?.MapEntry ?? false;
        }

        /// <summary>
        /// Reads a single field value from <paramref name="state"/>, interpreting the wire-format
        /// bytes according to <paramref name="field"/>'s protobuf type, and returns it as a CLR object.
        /// Supports all scalar types, enums, bytes, and nested messages (including well-known types).
        /// </summary>
        /// <param name="state">
        /// The <see cref="ProtoReader.State"/> currently positioned at a field value.
        /// </param>
        /// <param name="field">
        /// The <see cref="FieldDescriptorProto"/> that describes this field (type, name, number, etc.).
        /// </param>
        /// <param name="fds">
        /// The <see cref="FileDescriptorSet"/> containing descriptors for nested messages and enums.
        /// </param>
        /// <returns>
        /// A CLR object representing the field's value (e.g., <see cref="double"/>, <see cref="string"/>,
        /// <see cref="Dictionary{String,Object}"/> for nested messages, etc.).
        /// </returns>
        static object? ReadFieldValue(
            ref ProtoReader.State state,
            FieldDescriptorProto field,
            FileDescriptorSet fds)
        {
            // Read the next value from the wire according to the field’s protobuf type
            return field.Type switch
            {
                FieldDescriptorProto.Types.Type.Double => state.ReadDouble(),
                FieldDescriptorProto.Types.Type.Float => state.ReadSingle(),
                FieldDescriptorProto.Types.Type.Int64 => state.ReadInt64(),
                FieldDescriptorProto.Types.Type.Uint64 => state.ReadUInt64(),
                FieldDescriptorProto.Types.Type.Int32 => state.ReadInt32(),
                FieldDescriptorProto.Types.Type.Fixed64 => state.ReadUInt64(),
                FieldDescriptorProto.Types.Type.Fixed32 => state.ReadUInt32(),
                FieldDescriptorProto.Types.Type.Bool => state.ReadBoolean(),
                FieldDescriptorProto.Types.Type.String => state.ReadString(),
                FieldDescriptorProto.Types.Type.Bytes => state.AppendBytes(ReadOnlyMemory<byte>.Empty).ToArray(),
                FieldDescriptorProto.Types.Type.Uint32 => state.ReadUInt32(),
                FieldDescriptorProto.Types.Type.Sfixed32 => state.ReadInt32(),
                FieldDescriptorProto.Types.Type.Sfixed64 => state.ReadInt64(),
                FieldDescriptorProto.Types.Type.Sint32 => ZigZag.Decode32(state.ReadUInt32()),
                FieldDescriptorProto.Types.Type.Sint64 => ZigZag.Decode64(state.ReadUInt64()),
                FieldDescriptorProto.Types.Type.Enum => state.ReadInt32(),
                FieldDescriptorProto.Types.Type.Message => ReadEmbeddedMessage(ref state, field, fds),
                _ => throw new NotSupportedException($"Unsupported field type: {field.Type}")
            };
        }

        /// <summary>
        /// Reads a nested message (length-delimited) from <paramref name="state"/>, then parses it either
        /// as a well-known type (if <paramref name="field"/>'s type name matches a known WKT) or recursively
        /// via <see cref="ReadMessage"/>.
        /// </summary>
        /// <param name="state">
        /// The <see cref="ProtoReader.State"/> currently positioned at the start of the nested message.
        /// </param>
        /// <param name="field">
        /// The <see cref="FieldDescriptorProto"/> for this nested message field.
        /// </param>
        /// <param name="fds">
        /// The <see cref="FileDescriptorSet"/> containing the nested message descriptor.
        /// </param>
        /// <returns>
        /// Either a canonical JSON literal (for well-known types) or a <see cref="Dictionary{String,Object}"/>
        /// representing the nested message’s fields.
        /// </returns>
        static object ReadEmbeddedMessage(
            ref ProtoReader.State state,
            FieldDescriptorProto field,
            FileDescriptorSet fds)
        {
            // Read the raw length‐delimited bytes for this nested message
            var payload = state.AppendBytes(ReadOnlyMemory<byte>.Empty);

            // Look up the nested message descriptor by fully‐qualified name
            var nestedDesc = DescriptorSetCache.GetMessage(fds, field.TypeName)
                             ?? throw new InvalidOperationException($"Descriptor '{field.TypeName}' not found.");

            // If this is a well‐known type, invoke its specialized parser
            if (WellKnownTypes.Parsers.TryGetValue(field.TypeName, out var parser))
            {
                return parser(payload);
            }

            // Otherwise, treat as a regular nested message: parse recursively
            using var ms = new MemoryStream(payload.ToArray(), writable: false);
            var nestedState = ProtoReader.State.Create(ms, Converter.ProtobufJsonConverter.sharedModel.Value);
            var result = ReadMessage(ref nestedState, nestedDesc, fds);
            nestedState.Dispose();
            return result;
        }
    }
}