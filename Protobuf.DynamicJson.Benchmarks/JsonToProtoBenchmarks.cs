using BenchmarkDotNet.Attributes;
using Protobuf.DynamicJson.Descriptors;
using Protobuf.DynamicJson.Tests;

namespace Protobuf.DynamicJson.Benchmarks;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

[MemoryDiagnoser] // shows allocations
[ThreadingDiagnoser] // optional: see blocking/waits
// ReSharper disable once ClassCanBeSealed.Global
public class JsonToProtoBenchmarks
{
    // ---------- Parameter source -------------------------------------------
    [ParamsSource(nameof(Cases))]
#pragma warning disable CA1051
    //public ProtoCase Case;
    public TestData Case;
#pragma warning restore CA1051

    public IEnumerable<TestData> Cases => TestDataCatalog.All;

    //public static IEnumerable<ProtoCase> Cases => TestData.ProtoSpecs.Select(r => new ProtoCase((string)r[0], (string)r[1], (string)r[2]));

    // ---------- Per-case setup ---------------------------------------------
    byte[]? descriptor;

    [GlobalSetup] // runs once per case *before* benchmarks
    public void CompileDescriptor()
    {
        ProtoDescriptorHelper.TryCompileProtoToDescriptorSetBytes(
            Case.Proto,
            out var descriptorBytes,
            out _);

        descriptor = descriptorBytes!;
    }

    // ---------- The thing we care about ------------------------------------
    [Benchmark]
    public byte[] ConvertJsonToProto() => Converter.ProtobufJsonConverter.ConvertJsonToProtoBytes(Case.Json, Case.Name, descriptor);
}