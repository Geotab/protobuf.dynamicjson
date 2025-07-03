using BenchmarkDotNet.Attributes;
using Protobuf.DynamicJson.Descriptors;
using static Protobuf.DynamicJson.Tests.ProtobufJsonConverterTests; // reuse ProtoSpecs

namespace Protobuf.DynamicJson.Benchmarks;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

public record ProtoCase(string Name, string Spec, string Json);

[MemoryDiagnoser] // shows allocations
[ThreadingDiagnoser] // optional: see blocking/waits
// ReSharper disable once ClassCanBeSealed.Global
public class JsonToProtoBenchmarks
{
    // ---------- Parameter source -------------------------------------------
    [ParamsSource(nameof(Cases))]
#pragma warning disable CA1051
    public ProtoCase Case;
#pragma warning restore CA1051

    public static IEnumerable<ProtoCase> Cases => ProtoSpecs.Select(r => new ProtoCase((string)r[0], (string)r[1], (string)r[2]));

    // ---------- Per-case setup ---------------------------------------------
    byte[]? descriptor;

    [GlobalSetup] // runs once per case *before* benchmarks
    public void CompileDescriptor() => descriptor = ProtoDescriptorHelper.CompileProtoToDescriptorSetBytes(Case.Spec);

    // ---------- The thing we care about ------------------------------------
    [Benchmark]
    public byte[] ConvertJsonToProto() => Converter.ProtobufJsonConverter.ConvertJsonToProtoBytes(Case.Json, Case.Name, descriptor);
}