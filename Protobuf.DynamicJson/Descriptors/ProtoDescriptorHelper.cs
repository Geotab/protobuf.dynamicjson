extern alias protoNet;
using ProtoBuf;
using ProtoNetReflection = protoNet::Google.Protobuf.Reflection;

namespace Protobuf.DynamicJson.Descriptors;

/// <summary>
/// Provider for compiling proto messages to a FileDescriptorSet in byte format
/// </summary>
internal static class ProtoDescriptorHelper
{
    /// <summary>
    /// Compiles a proto message(s) to a FileDescriptorSet in byte format
    /// </summary>
    /// <param name="protoContent">The proto message(s)</param>
    /// <returns>FileDescriptorSet in byte format</returns>
    public static byte[] CompileProtoToDescriptorSetBytes(string protoContent)
    {
        var set = new ProtoNetReflection.FileDescriptorSet();
        set.Add("schema.proto", source: new StringReader(protoContent));
        set.Process();

        using var memoryStream = new MemoryStream();
        set.Serialize(
            static (fds, state) =>
            {
                var stream = (Stream)state!;
                Serializer.Serialize(stream, fds);
                return true;
            },
            includeImports: true,
            state: memoryStream);

        return memoryStream.ToArray();
    }
}