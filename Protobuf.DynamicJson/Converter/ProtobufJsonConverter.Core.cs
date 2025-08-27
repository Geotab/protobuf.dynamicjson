using System.Collections.Concurrent;
using System.Diagnostics;
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
    
    // Cache instance for storing parsed FileDescriptorSet objects with max cache size
    static MemoryCache descriptorCache;
    
    // Lock object to ensure thread-safe lazy initialization of the descriptor cache
#if NET9_0_OR_GREATER
static readonly Lock initLock = new();
#else
    static readonly object initLock = new();
#endif
    
    // Indicates whether the converter has been initialized with custom or default options
    static bool initialized;
    
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

    // Stores various converter options
    public static ProtobufJsonConverterOptions Options = new();

    /// <summary>
    /// Initializes the <see cref="ProtobufJsonConverter"/> with custom configuration options.
    /// This method allows callers to override default caching behavior for descriptor storage.
    /// Must be called once before any conversion methods, or defaults will be used.
    /// </summary>
    /// <param name="customOptions">
    /// The custom configuration to apply. Cannot be null.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="customOptions"/> is null.
    /// </exception>
    public static void Initialize(ProtobufJsonConverterOptions customOptions)
    {
        lock (initLock)
        {
            if (initialized) return;

            Options = customOptions ?? throw new ArgumentNullException(nameof(customOptions));

            descriptorCache = new MemoryCache(new MemoryCacheOptions
            {
                SizeLimit = Options.DescriptorCacheSizeLimit
            });

            initialized = true;
        }
    }
    
    /// <summary>
    /// Converts canonical proto-JSON into protobuf binary.
    /// </summary>
    /// <param name="protoJson">The JSON string (proto3 JSON format)</param>
    /// <param name="topLevelMessageName">Fully-qualified top-level protobuf message name</param>
    /// <param name="descriptorSetBytes">FileDescriptorSet in binary form</param>
    /// <returns>Protobuf wire-format as byte[]</returns>
    public static byte[] ConvertJsonToProtoBytes(string protoJson, string topLevelMessageName, byte[] descriptorSetBytes)
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
    public static string ConvertProtoBytesToJson(byte[] protoBytes, string topLevelMessageName, byte[] descriptorSetBytes)
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
    
    /// <summary>
    /// Retrieves a parsed <see cref="FileDescriptorSet"/> from the cache based on the input bytes.
    /// If the descriptor is not already cached, it is parsed, cached, and then returned.
    /// </summary>
    /// <param name="descriptorSetBytes">The raw binary representation of a FileDescriptorSet.</param>
    /// <returns>The parsed <see cref="FileDescriptorSet"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="descriptorSetBytes"/> is null.</exception>
    static FileDescriptorSet GetOrAddDescriptor(byte[] descriptorSetBytes)
    {
        // Validate non-null arguments
        ArgumentNullException.ThrowIfNull(descriptorSetBytes);

        // Make sure options/cache exist
        EnsureInitialized();

        var hashKey = Convert.ToHexString(hashProvider.ComputeHash(descriptorSetBytes));

        if (!descriptorCache.TryGetValue(hashKey, out FileDescriptorSet? descriptor) || descriptor is null)
        {
            descriptor = FileDescriptorSet.Parser.ParseFrom(descriptorSetBytes);
            descriptorCache.Set(hashKey, descriptor, new MemoryCacheEntryOptions
            {
                Size = 1
            });
        }

        return descriptor;
    }
    
    /// <summary>
    /// Ensures that the converter is initialized with either default or previously supplied options.
    /// </summary>
    static void EnsureInitialized()
    {
        if (initialized) return;

        // Use default options
        Initialize(new ProtobufJsonConverterOptions());
    }
    
#if DEBUG
    [Conditional("DEBUG")]
    internal static void ResetForTests(ProtobufJsonConverterOptions? customOptions = null)
    {
        lock (initLock)
        {
            descriptorCache?.Dispose();
            Options = customOptions ?? new ProtobufJsonConverterOptions();
            descriptorCache = new MemoryCache(new MemoryCacheOptions
            {
                SizeLimit = Options.DescriptorCacheSizeLimit
            });
            initialized = true;
        }
    }
#endif
}