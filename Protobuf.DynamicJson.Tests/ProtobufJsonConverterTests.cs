extern alias protoNet;
using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Contoso.Protobuf;
using Google.Protobuf;
using Protobuf.DynamicJson.Converter;
using Protobuf.DynamicJson.Descriptors;
using Test;

namespace Protobuf.DynamicJson.Tests;

public sealed class ProtobufJsonConverterTests
{
    public static TheoryData<string, string, string> ProtoSpecs
    {
        get
        {
            var data = new TheoryData<string, string, string>();
            foreach (var s in TestDataCatalog.All)
            {
                data.Add(s.Name, s.Proto, s.Json);
            }
            return data;
        }
    }

    static readonly string SimpleProto = """
         syntax = "proto3";
         message MyMessage {
             int32 value = 1;
         }
     """;
    static readonly string SimpleJson = """{ "value": 123 }""";
    static readonly string SimpleProtoMessageName = "MyMessage";
    
    [Theory]
    [MemberData(nameof(ProtoSpecs))]
    public void ConvertJsonToProtoBytes_WithValidProtoSpecs_ProducesEquivalentMessages(string messageName, string protoSpec, string sampleProto)
    {
        ProtoDescriptorHelper.TryCompileProtoToDescriptorSetBytes(protoSpec, out var desc, out _);
        var bytes = ProtobufJsonConverter.ConvertJsonToProtoBytes(sampleProto, messageName, desc);

        AssertParsedMessagesAreEqual(messageName, sampleProto, bytes);
    }

    [Theory]
    [MemberData(nameof(ProtoSpecs))]
    public void ConvertProtoBytesToJson_RoundTrip_PreservesMessage(string messageName, string protoSpec, string sampleProto)
    {
        ProtoDescriptorHelper.TryCompileProtoToDescriptorSetBytes(protoSpec, out var desc, out _);
        var originalBytes = ProtobufJsonConverter.ConvertJsonToProtoBytes(sampleProto, messageName, desc);
        var jsonFromBytes = ProtobufJsonConverter.ConvertProtoBytesToJson(originalBytes, messageName, desc);
        var roundTrippedBytes = ProtobufJsonConverter.ConvertJsonToProtoBytes(jsonFromBytes, messageName, desc);

        AssertParsedMessagesAreEqual(messageName, sampleProto, roundTrippedBytes);

        AssertJsonMessagesAreEqual(sampleProto, jsonFromBytes);
    }

    [Fact]
    public void ConvertProtoBytesToJson_RoundTrip_PreservesMessage_ProtoName()
    {
        var options = new ProtobufJsonConverterOptions
        {
            AcceptBothInputNames = true,
            OutputFieldNaming = JsonFieldNamingPolicy.ProtoName
        };
        ProtobufJsonConverter.ResetForTests(options);

        var messageName = "Mappings";
        var protoSpec = "syntax = \"proto3\";\n\noption csharp_namespace = \"Contoso.Protobuf\";\noption go_package = \"git.contoso.com/dev/GatewayGoProto-golang/configuration/iox\";\n\nmessage UIntPoints {\n    repeated uint32 input_values = 1;\n    repeated uint32 output_values = 2;\n}\n\nenum DigitalAux {\n    DIGITAL_AUX_1 = 0;\n    DIGITAL_AUX_2 = 1;\n    DIGITAL_AUX_3 = 2;\n    DIGITAL_AUX_4 = 3;\n    DIGITAL_AUX_5 = 4;\n    DIGITAL_AUX_6 = 5;\n    DIGITAL_AUX_7 = 6;\n    DIGITAL_AUX_8 = 7;\n}\n\nmessage Input {\n    oneof input {\n        DigitalAux aux = 1;\n        uint32 status_id = 2;\n    }\n}\n\nmessage Output {\n    oneof output {\n        uint32 status_id = 1;\n    }\n}\n\nmessage Transform {\n    oneof transform {\n        UIntPoints uint_points = 1;\n    }\n}\n\nmessage Mapping {\n    Input input = 1;\n    Output output = 2;\n    Transform transform = 3;\n}\n\nmessage Mappings {\n    repeated Mapping mapping = 1;\n}";
        var sampleProto = "{\"mapping\":[{\"input\":{\"status_id\":42},\"output\":{\"status_id\":17},\"transform\":{\"uint_points\":{\"input_values\":[100,200],\"output_values\":[150,250]}}},{\"input\":{\"aux\":\"DIGITAL_AUX_3\"},\"output\":{\"status_id\":33},\"transform\":{\"uint_points\":{\"input_values\":[1,2,3],\"output_values\":[4,5,6]}}}]}";
        
        ProtoDescriptorHelper.TryCompileProtoToDescriptorSetBytes(protoSpec, out var desc, out _);
        var originalBytes = ProtobufJsonConverter.ConvertJsonToProtoBytes(sampleProto, messageName, desc);
        var jsonFromBytes = ProtobufJsonConverter.ConvertProtoBytesToJson(originalBytes, messageName, desc);
        var roundTrippedBytes = ProtobufJsonConverter.ConvertJsonToProtoBytes(jsonFromBytes, messageName, desc);

        AssertParsedMessagesAreEqual(messageName, sampleProto, roundTrippedBytes);

        AssertJsonMessagesAreEqual(sampleProto, jsonFromBytes);
    }

    [Theory]
    [MemberData(nameof(ProtoSpecs))]
    public void ConvertJsonToProtoBytes_WithLengthPrefix_ProducesValidPrefixedMessage(string messageName, string protoSpec, string sampleProto)
    {
        ProtoDescriptorHelper.TryCompileProtoToDescriptorSetBytes(protoSpec, out var desc, out _);
        
        // Create length-prefixed bytes
        var prefixedBytes = ProtobufJsonConverter.ConvertJsonToProtoBytes(sampleProto, messageName, desc, useLengthPrefix: true);
        
        // Verify it's actually prefixed (should be longer than non-prefixed)
        var normalBytes = ProtobufJsonConverter.ConvertJsonToProtoBytes(sampleProto, messageName, desc, useLengthPrefix: false);
        var expectedVarintSize = GetVarintSize((uint)normalBytes.Length);
        Assert.Equal(normalBytes.Length + expectedVarintSize, prefixedBytes.Length);
        
        // Read back with prefix flag
        var jsonFromPrefixed = ProtobufJsonConverter.ConvertProtoBytesToJson(prefixedBytes, messageName, desc, hasLengthPrefix: true);
        
        // Should match original
        AssertJsonMessagesAreEqual(sampleProto, jsonFromPrefixed);
    }

    [Theory]
    [MemberData(nameof(ProtoSpecs))]
    public void ConvertProtoBytesToJson_WithLengthPrefix_RoundTrip_PreservesMessage(string messageName, string protoSpec, string sampleProto)
    {
        ProtoDescriptorHelper.TryCompileProtoToDescriptorSetBytes(protoSpec, out var desc, out _);
        
        // Create prefixed bytes from JSON
        var prefixedBytes = ProtobufJsonConverter.ConvertJsonToProtoBytes(sampleProto, messageName, desc, useLengthPrefix: true);
        
        // Convert back to JSON
        var jsonFromBytes = ProtobufJsonConverter.ConvertProtoBytesToJson(prefixedBytes, messageName, desc, hasLengthPrefix: true);
        
        // Round-trip again with prefix
        var roundTrippedPrefixedBytes = ProtobufJsonConverter.ConvertJsonToProtoBytes(jsonFromBytes, messageName, desc, useLengthPrefix: true);
        
        // Convert back to JSON again
        var finalJson = ProtobufJsonConverter.ConvertProtoBytesToJson(roundTrippedPrefixedBytes, messageName, desc, hasLengthPrefix: true);
        
        // All JSON representations should match
        AssertJsonMessagesAreEqual(sampleProto, jsonFromBytes);
        AssertJsonMessagesAreEqual(sampleProto, finalJson);
        
        // Prefixed bytes should match
        Assert.Equal(prefixedBytes, roundTrippedPrefixedBytes);
    }

    [Fact]
    public void ConvertProtoBytesToJson_WithLengthPrefixFlag_ThrowsOnNonPrefixedData()
    {
        ProtoDescriptorHelper.TryCompileProtoToDescriptorSetBytes(SimpleProto, out var desc, out _);
        
        // Create non-prefixed bytes
        var normalBytes = ProtobufJsonConverter.ConvertJsonToProtoBytes(SimpleJson, SimpleProtoMessageName, desc, useLengthPrefix: false);
        
        // Try to read with prefix flag should throw
        var ex = Assert.Throws<InvalidDataException>(() => 
            ProtobufJsonConverter.ConvertProtoBytesToJson(normalBytes, SimpleProtoMessageName, desc, hasLengthPrefix: true));
        
        Assert.Contains("Length prefix mismatch", ex.Message);
    }

    [Fact]
    public void ConvertProtoBytesToJson_WithoutLengthPrefixFlag_WorksOnNonPrefixedData()
    {
        ProtoDescriptorHelper.TryCompileProtoToDescriptorSetBytes(SimpleProto, out var desc, out _);
        
        // Create non-prefixed bytes
        var normalBytes = ProtobufJsonConverter.ConvertJsonToProtoBytes(SimpleJson, SimpleProtoMessageName, desc, useLengthPrefix: false);
        
        // Read without prefix flag should work
        var json = ProtobufJsonConverter.ConvertProtoBytesToJson(normalBytes, SimpleProtoMessageName, desc, hasLengthPrefix: false);
        
        AssertJsonMessagesAreEqual(SimpleJson, json);
    }

    [Fact]
    public void ConvertJsonToProtoBytes_LengthPrefix_CreatesCorrectFormat()
    {
        ProtoDescriptorHelper.TryCompileProtoToDescriptorSetBytes(SimpleProto, out var desc, out _);

        // Create prefixed bytes
        var prefixedBytes =
            ProtobufJsonConverter.ConvertJsonToProtoBytes(SimpleJson, SimpleProtoMessageName, desc, useLengthPrefix: true);
        var normalBytes =
            ProtobufJsonConverter.ConvertJsonToProtoBytes(SimpleJson, SimpleProtoMessageName, desc, useLengthPrefix: false);

        // Manually verify the prefix format
        // First byte(s) should be a varint representing the length of normalBytes
        int varIntSize = 0;
        uint decodedLength = 0;
        int shift = 0;

        for (int i = 0; i < Math.Min(5, prefixedBytes.Length); i++)
        {
            byte b = prefixedBytes[i];
            varIntSize++;
            decodedLength |= (uint)(b & 0x7F) << shift;

            if ((b & 0x80) == 0)
                break;

            shift += 7;
        }

        // Decoded length should match the normal bytes length
        Assert.Equal((uint)normalBytes.Length, decodedLength);

        // Total length should be varint size + message size
        Assert.Equal(varIntSize + normalBytes.Length, prefixedBytes.Length);

        // The message portion should match exactly
        var messagePortionFromPrefixed = prefixedBytes.Skip(varIntSize).ToArray();
        Assert.Equal(normalBytes, messagePortionFromPrefixed);
    }

    [Fact]
    public async Task ConvertJsonToProtoBytes_IsThreadSafe_UnderConcurrency()
    {
        // Arrange: minimal schema + JSON
        const string protoSpec = @"
            syntax = ""proto3"";
            package test;

            message Root {
              int32 a = 1;
              string b = 2;
            }
        ";

        const string rootMessageName = "test.Root";
        const string desiredConfigJson = @"{ ""a"": 123, ""b"": ""hello"" }";

        if (!ProtoDescriptorHelper.TryCompileProtoToDescriptorSetBytes(
                protoSpec,
                out var descriptorSetBytes,
                out var errors,
                includeImports: true))
        {
            throw new InvalidOperationException("Proto compilation failed: " + string.Join("; ", errors));
        }

        var exceptions = new ConcurrentQueue<Exception>();

        // Tune these to make it more/less aggressive
        const int workers = 32;
        const int iterationsPerWorker = 200;

        // Optional: start all workers at the same time
        var startGate = new ManualResetEventSlim(false);

        // Act
        var tasks = Enumerable.Range(0, workers).Select(_ => Task.Run(() =>
        {
            startGate.Wait();

            for (int i = 0; i < iterationsPerWorker; i++)
            {
                try
                {
                    var bytes = ProtobufJsonConverter.ConvertJsonToProtoBytes(
                        desiredConfigJson,
                        rootMessageName,
                        descriptorSetBytes,
                        true);

                    // Basic sanity: should produce some bytes
                    Assert.NotNull(bytes);
                    Assert.NotEmpty(bytes);
                }
                catch (Exception ex)
                {
                    exceptions.Enqueue(ex);
                }
            }
        })).ToArray();

        startGate.Set();
        await Task.WhenAll(tasks);

        // Assert
        if (!exceptions.IsEmpty)
        {
            var first = exceptions.First();
            var allMsgs = string.Join(Environment.NewLine, exceptions.Take(10).Select(e => e.ToString()));
            throw new Xunit.Sdk.XunitException(
                $"Concurrency test saw {exceptions.Count} exceptions. First: {first.GetType().Name}: {first.Message}{Environment.NewLine}{allMsgs}");
        }
    }

    static int GetVarintSize(uint value)
    {
        return value switch
        {
            < 0x80 => 1,
            < 0x4000 => 2,
            < 0x200000 => 3,
            < 0x10000000 => 4,
            _ => 5
        };
    }

    static void AssertJsonMessagesAreEqual(string firstJson, string secondJson)
    {
        var jsonNode1 = JsonNode.Parse(firstJson);
        var jsonNode2 = JsonNode.Parse(secondJson);
        
        Assert.True(JsonNode.DeepEquals(jsonNode1, jsonNode2));
    }

    static void AssertParsedMessagesAreEqual(string messageName, string sampleProtoJson, byte[] bytesToVerify)
    {
        switch (messageName)
        {
            case "contoso.protobuf.configuration.DeviceConfiguration":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<DeviceConfiguration>(sampleProtoJson), DeviceConfiguration.Parser.ParseFrom(bytesToVerify));
                break;

            case "Mappings":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<Mappings>(sampleProtoJson), Mappings.Parser.ParseFrom(bytesToVerify));
                break;

            case "Mappings_old":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<Mappings_old>(sampleProtoJson), Mappings_old.Parser.ParseFrom(bytesToVerify));
                break;

            case "test.DeviceConfigurationExtended":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<DeviceConfigurationExtended>(sampleProtoJson), DeviceConfigurationExtended.Parser.ParseFrom(bytesToVerify));
                break;

            case "test.DeviceInfo":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<DeviceInfo>(sampleProtoJson), DeviceInfo.Parser.ParseFrom(bytesToVerify));
                break;

            case "test.TelemetryData":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<TelemetryData>(sampleProtoJson), TelemetryData.Parser.ParseFrom(bytesToVerify));
                break;

            case "test.SensorReading":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<SensorReading>(sampleProtoJson), SensorReading.Parser.ParseFrom(bytesToVerify));
                break;

            case "AlarmStatus":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<AlarmStatus>(sampleProtoJson), AlarmStatus.Parser.ParseFrom(bytesToVerify));
                break;

            case "test.DeviceSettings":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<DeviceSettings>(sampleProtoJson), DeviceSettings.Parser.ParseFrom(bytesToVerify));
                break;

            case "test.FirmwareUpdate":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<FirmwareUpdate>(sampleProtoJson), FirmwareUpdate.Parser.ParseFrom(bytesToVerify));
                break;

            case "test.NetworkInfo":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<NetworkInfo>(sampleProtoJson), NetworkInfo.Parser.ParseFrom(bytesToVerify));
                break;

            case "test.TripSummary":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<TripSummary>(sampleProtoJson), TripSummary.Parser.ParseFrom(bytesToVerify));
                break;

            case "test.LocationEvent":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<LocationEvent>(sampleProtoJson), LocationEvent.Parser.ParseFrom(bytesToVerify));
                break;

            case "test.BatteryStatus":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<BatteryStatus>(sampleProtoJson), BatteryStatus.Parser.ParseFrom(bytesToVerify));
                break;

            case "test.ComplexMessage":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<ComplexMessage>(sampleProtoJson), ComplexMessage.Parser.ParseFrom(bytesToVerify));
                break;

            case "test.TreeNode":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<TreeNode>(sampleProtoJson), TreeNode.Parser.ParseFrom(bytesToVerify));
                break;

            case "test.Level1":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<Level1>(sampleProtoJson), Level1.Parser.ParseFrom(bytesToVerify));
                break;

            case "test.OneOfExample":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<OneOfExample>(sampleProtoJson), OneOfExample.Parser.ParseFrom(bytesToVerify));
                break;

            case "test.EmptyMessage":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<EmptyMessage>(sampleProtoJson), EmptyMessage.Parser.ParseFrom(bytesToVerify));
                break;

            case "test.PackedRepeated":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<PackedRepeated>(sampleProtoJson), PackedRepeated.Parser.ParseFrom(bytesToVerify));
                break;

            case "test.MapWithMessage":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<MapWithMessage>(sampleProtoJson), MapWithMessage.Parser.ParseFrom(bytesToVerify));
                break;

            case "test.WithNestedEnum":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<WithNestedEnum>(sampleProtoJson), WithNestedEnum.Parser.ParseFrom(bytesToVerify));
                break;

            case "test.SignedTypes":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<SignedTypes>(sampleProtoJson), SignedTypes.Parser.ParseFrom(bytesToVerify));
                break;

            case "test.OptionalString":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<OptionalString>(sampleProtoJson), OptionalString.Parser.ParseFrom(bytesToVerify));
                break;

            case "test.LargeRepeated":
                Assert.Equal(new JsonParser(JsonParser.Settings.Default).Parse<LargeRepeated>(sampleProtoJson), LargeRepeated.Parser.ParseFrom(bytesToVerify));
                break;

            default:
                throw new NotSupportedException($"No parser mapped for '{messageName}'.");
        }
    }
}
