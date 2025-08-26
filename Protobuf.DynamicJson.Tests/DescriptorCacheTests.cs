extern alias protoNet;
using System.Reflection;
using Microsoft.Extensions.Caching.Memory;
using Protobuf.DynamicJson.Converter;
using Protobuf.DynamicJson.Descriptors;

namespace Protobuf.DynamicJson.Tests;

[Collection("NonParallel")]
public sealed class DescriptorCacheTests
{
    static readonly string SimpleProto = """
         syntax = "proto3";
         message MyMessage {
             int32 value = 1;
         }
     """;
    static readonly string SimpleJson = """{ "value": 123 }""";
    static readonly string SimpleProtoMessageName = "MyMessage";
    
    [Fact]
    public void ConvertJsonToProtoBytes_ShouldReuseDescriptorCache_ForEquivalentDescriptorBytes()
    {
        // Arrange
        ProtobufJsonConverter.ResetForTests();
        
        ProtoDescriptorHelper.TryCompileProtoToDescriptorSetBytes(SimpleProto, out var descriptorBytes1, out _);
        ProtoDescriptorHelper.TryCompileProtoToDescriptorSetBytes(SimpleProto, out var descriptorBytes2, out _); // New instance with same content

        // Act - First conversion (will populate the cache)
        var result1 = ProtobufJsonConverter.ConvertJsonToProtoBytes(SimpleJson, SimpleProtoMessageName, descriptorBytes1);

        // Act - Second conversion (should hit the cache)
        var result2 = ProtobufJsonConverter.ConvertJsonToProtoBytes(SimpleJson, SimpleProtoMessageName, descriptorBytes2);

        // Clear cache
        var cache = GetCacheInstance();
        
        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Equal(1, cache.Count);
    }
    
    [Fact]
    public void ConvertJsonToProtoBytes_ShouldNotReuseDescriptorCache_ForDifferentDescriptorBytes()
    {
        // Arrange
        ProtobufJsonConverter.ResetForTests();
        
        var simpleProto2 = """
             syntax = "proto3";
             message MyMessage2 {
                 int32 value2 = 1;
             }
         """;
        var simpleJson2 = """{ "value2": 123 }""";
        var simpleProtoMessageName2 = "MyMessage2";

        ProtoDescriptorHelper.TryCompileProtoToDescriptorSetBytes(SimpleProto, out var descriptorBytes1, out _);
        ProtoDescriptorHelper.TryCompileProtoToDescriptorSetBytes(simpleProto2, out var descriptorBytes2, out _);

        // Act
        var result1 = ProtobufJsonConverter.ConvertJsonToProtoBytes(SimpleJson, SimpleProtoMessageName, descriptorBytes1);
        var result2 = ProtobufJsonConverter.ConvertJsonToProtoBytes(simpleJson2, simpleProtoMessageName2, descriptorBytes2);

        var cache = GetCacheInstance();
        
        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Equal(2, cache.Count);
    }
    
    [Fact]
    public void Initialize_Applies_CustomCacheSizeLimit()
    {
        // Arrange
        ProtobufJsonConverter.ResetForTests();
        
        var options = new ProtobufJsonConverterOptions
        {
            DescriptorCacheSizeLimit = 1
        };

        ProtobufJsonConverter.Initialize(options);

        var cache = GetCacheInstance();
        
        ProtoDescriptorHelper.TryCompileProtoToDescriptorSetBytes(SimpleProto, out var desc1, out _);
        ProtoDescriptorHelper.TryCompileProtoToDescriptorSetBytes(SimpleProto, out var desc2, out _); // Same content, different instance

        // These insert the descriptor twice
        ProtobufJsonConverter.ConvertJsonToProtoBytes(SimpleJson, SimpleProtoMessageName, desc1);
        ProtobufJsonConverter.ConvertJsonToProtoBytes(SimpleJson, SimpleProtoMessageName, desc2);

        Assert.Equal(1, cache.Count); // Only one retained due to size limit
    }

    [Fact]
    public void Initialize_Twice_Does_Not_Override_ExistingConfig()
    {
        // Arrange
        var initial = new ProtobufJsonConverterOptions
        {
            DescriptorCacheSizeLimit = 1
        };

        var second = new ProtobufJsonConverterOptions
        {
            DescriptorCacheSizeLimit = 999 // Should be ignored
        };

        // Arrange
        ProtobufJsonConverter.ResetForTests(initial);
        ProtobufJsonConverter.Initialize(second);
        
        ProtoDescriptorHelper.TryCompileProtoToDescriptorSetBytes(SimpleProto, out var desc1, out _);
        ProtoDescriptorHelper.TryCompileProtoToDescriptorSetBytes(SimpleProto, out var desc2, out _); // Same content, different instance

        var cache = GetCacheInstance();
        
        // These insert the descriptor twice
        ProtobufJsonConverter.ConvertJsonToProtoBytes(SimpleJson, SimpleProtoMessageName, desc1);
        ProtobufJsonConverter.ConvertJsonToProtoBytes(SimpleJson, SimpleProtoMessageName, desc2);

        Assert.Equal(1, cache.Count); // Still respects first config
    }
    
    static MemoryCache? GetCacheInstance()
    {
        var cacheField = typeof(ProtobufJsonConverter)
            .GetField("descriptorCache", BindingFlags.Static | BindingFlags.NonPublic);

        return (MemoryCache?)cacheField?.GetValue(null);
    }
}
