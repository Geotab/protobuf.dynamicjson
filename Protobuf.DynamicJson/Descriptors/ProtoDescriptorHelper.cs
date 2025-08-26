extern alias protoNet;
using ProtoBuf;
using ProtoNetReflection = protoNet::Google.Protobuf.Reflection;

namespace Protobuf.DynamicJson.Descriptors;

/// <summary>
/// Provider for compiling proto messages to a FileDescriptorSet in byte format
/// </summary>
public static class ProtoDescriptorHelper
{
    /// <summary>
    /// Attempts to compile the given proto message(s) into a serialized FileDescriptorSet 
    /// represented as a byte array.
    /// </summary>
    /// <param name="protoContent">The raw proto definition text to compile.</param>
    /// <param name="descriptorSetBytes">
    /// When this method returns <c>true</c>, contains the compiled FileDescriptorSet 
    /// as a byte array. Otherwise, <c>null</c>.
    /// </param>
    /// <param name="errors">
    /// When this method returns <c>false</c>, contains a list of error messages describing why 
    /// compilation failed. If successful, this will be empty.
    /// </param>
    /// <param name="includeImports">
    /// If <c>true</c>, imported proto files are included in the serialized descriptor set; 
    /// otherwise only the primary file is included.
    /// </param>
    /// <returns>
    /// <c>true</c> if compilation succeeded and <paramref name="descriptorSetBytes"/> contains a valid 
    /// descriptor set; otherwise, <c>false</c>.
    /// </returns>
    public static bool TryCompileProtoToDescriptorSetBytes(
        string protoContent,
        out byte[]? descriptorSetBytes,
        out IReadOnlyList<string> errors,
        bool includeImports = true)
    {
        descriptorSetBytes = null;

        if (string.IsNullOrWhiteSpace(protoContent))
        {
            errors = ["protoContent is null or empty."];
            return false;
        }

        var set = new ProtoNetReflection.FileDescriptorSet();
        set.Add("schema.proto", source: new StringReader(protoContent));
        set.Process();

        var errList = (set.GetErrors() ?? Array.Empty<object>())
            .Select(e => e.ToString() ?? "Unknown error")
            .ToList();

        if (errList.Count > 0)
        {
            errors = errList;
            return false;
        }

        using var ms = new MemoryStream();
        set.Serialize(
            static (fds, state) =>
            {
                var stream = (Stream)state!;
                Serializer.Serialize(stream, fds);
                return true;
            },
            includeImports: includeImports,
            state: ms
        );

        descriptorSetBytes = ms.ToArray();
        errors = [];
        return true;
    }
}