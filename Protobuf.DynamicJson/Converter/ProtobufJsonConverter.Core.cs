using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Google.Protobuf.Reflection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IO;
using ProtoBuf;
using Protobuf.DynamicJson.Descriptors;
using Protobuf.DynamicJson.Utils;
using ProtoBuf.Meta;

namespace Protobuf.DynamicJson.Converter;

/// <summary>
/// Provides functionality to convert between Protocol Buffer messages and their
/// canonical JSON representations. Supports:
/// - Parsing proto3 JSON into a CLR dictionary and serializing it to protobuf wire-format bytes.
/// - Deserializing protobuf wire-format bytes into a CLR dictionary and converting it back to
///   canonical proto3 JSON.
/// This class relies on a cached FileDescriptorSet for message/enum descriptors and a shared
/// RuntimeTypeModel for protobuf I/O. It delegates reading and writing of individual fields to
/// nested Reader and Writer classes.
/// </summary>
public static partial class ProtobufJsonConverter
{
    // Static SHA256 provider for efficient, thread-safe hashing
    static readonly SHA256 hashProvider = SHA256.Create(); 
    // Manages pooled MemoryStreams to reduce allocations during (de)serialization
    static readonly RecyclableMemoryStreamManager memoryStreamManager = new();
    // Cache for FileDescriptorSet instances by their serialized bytes to avoid reparsing
    static readonly MemoryCache descriptorCache = new(new MemoryCacheOptions
    {
        SizeLimit = 100 // max number of descriptors to keep
    });
    // Caches resolved enum numeric values by (typeName, literal) pairs to speed up lookups
    static readonly ConcurrentDictionary<(string TypeName, string Literal), int> enumNumberCache = new();
    // Lazy-initialized RuntimeTypeModel shared across all (de)serialization calls
    static readonly Lazy<RuntimeTypeModel> sharedModel = new(
        () =>
        {
            var m = RuntimeTypeModel.Create();
            m.AutoAddMissingTypes = false;
            m.AutoCompile = false;
            return m;
        },
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Converts canonical proto-JSON into protobuf binary.
    /// </summary>
    /// <param name="protoJson">The JSON string (proto3 JSON format)</param>
    /// <param name="topLevelMessageName">Fully-qualified top-level protobuf message name</param>
    /// <param name="descriptorSetBytes">FileDescriptorSet in binary form</param>
    /// <returns>Protobuf wire-format as byte[]</returns>
    public static byte[] ConvertJsonToProtoBytes(string protoJson, string topLevelMessageName, byte[]? descriptorSetBytes)
    {
        // Validate non-null arguments
        ArgumentNullException.ThrowIfNull(protoJson);
        ArgumentNullException.ThrowIfNull(topLevelMessageName);
        ArgumentNullException.ThrowIfNull(descriptorSetBytes);

        // Parse or retrieve cached FileDescriptorSet
        var descriptor = GetOrAddDescriptor(descriptorSetBytes);
        // Look up the top-level message descriptor by name
        var messageDescriptor = DescriptorSetCache.GetMessage(descriptor, topLevelMessageName)
            ?? throw new ArgumentException($"Message descriptor '{topLevelMessageName}' not found.");

        // Retrieve the shared runtime model for ProtoBuf.Meta
        var runtimeModel = sharedModel.Value;
        // Parse incoming JSON into a JsonNode tree
        var configJsonNode = JsonNode.Parse(protoJson);
        // Convert JsonNode tree into a dictionary of CLR values
        var configDictionary = JsonUtilities.ConvertJsonToDictionary(configJsonNode);

        // Obtain a recycled MemoryStream for writing proto bytes
        using var stream = memoryStreamManager.GetStream();
        // Create a ProtoWriter state bound to the stream and model
        var state = ProtoWriter.State.Create((Stream)stream, runtimeModel);
        try
        {
            // Delegate actual field-by-field writing to Writer helper
            Writer.WriteMessage(ref state, configDictionary, messageDescriptor, descriptor, runtimeModel);
            state.Flush();
        }
        finally
        {
            // Ensure resources are disposed even if writing fails
            state.Dispose();
        }

        // Return the serialized protobuf payload
        return stream.ToArray();
    }

    /// <summary>
    /// Converts binary protobuf into canonical proto-JSON.
    /// </summary>
    /// <param name="protoBytes">Wire-format payload</param>
    /// <param name="topLevelMessageName">Fully-qualified top-level protobuf message name</param>
    /// <param name="descriptorSetBytes">FileDescriptorSet in binary form</param>
    /// <returns>JSON string in official proto-JSON syntax</returns>
    public static string ConvertProtoBytesToJson(byte[] protoBytes, string topLevelMessageName, byte[]? descriptorSetBytes)
    {
        // Validate non-null arguments
        ArgumentNullException.ThrowIfNull(protoBytes);
        ArgumentNullException.ThrowIfNull(topLevelMessageName);
        ArgumentNullException.ThrowIfNull(descriptorSetBytes);

        // Parse or retrieve cached FileDescriptorSet
        var descriptor = GetOrAddDescriptor(descriptorSetBytes);
        // Look up the top-level message descriptor by name
        var msgDescriptor = DescriptorSetCache.GetMessage(descriptor, topLevelMessageName)
                            ?? throw new ArgumentException($"Message descriptor '{topLevelMessageName}' not found.");

        // Retrieve the shared runtime model for ProtoBuf.Meta
        var model = sharedModel.Value;

        // Wrap incoming proto bytes in a MemoryStream (read-only)
        using var ms = new MemoryStream(protoBytes, writable: false);
        // Create a ProtoReader state bound to the stream and model
        var state = ProtoReader.State.Create(ms, model);
        // Recursively read fields into a CLR dictionary
        var rootDict = Reader.ReadMessage(ref state, msgDescriptor, descriptor);
        state.Dispose();

        // Convert the CLR dictionary back into a JsonNode tree
        var jsonNode = JsonUtilities.ToJsonNode(rootDict);
        // Serialize JsonNode to a compact JSON string with relaxed escaping
        return jsonNode!.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = false,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }
    
    private static FileDescriptorSet GetOrAddDescriptor(byte[] descriptorSetBytes)
    {
        ArgumentNullException.ThrowIfNull(descriptorSetBytes);

        var hashKey = Convert.ToHexString(hashProvider.ComputeHash(descriptorSetBytes));

        if (!descriptorCache.TryGetValue(hashKey, out FileDescriptorSet? descriptor) || descriptor is null)
        {
            descriptor = FileDescriptorSet.Parser.ParseFrom(descriptorSetBytes);
            descriptorCache.Set(hashKey, descriptor, new MemoryCacheEntryOptions
            {
                Size = 1,
                SlidingExpiration = TimeSpan.FromHours(24)
            });
        }

        return descriptor;
    }
}