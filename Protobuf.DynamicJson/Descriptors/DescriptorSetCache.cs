using System.Runtime.CompilerServices;
using Google.Protobuf.Reflection;

namespace Protobuf.DynamicJson.Descriptors;

/// <summary>
/// Caches and retrieves Protobuf descriptor information (message and enum definitions)
/// from a FileDescriptorSet. Uses a ConditionalWeakTable to store parsed descriptors,
/// enabling fast lookups of DescriptorProto and EnumDescriptorProto by fully-qualified name.
/// </summary>
internal static class DescriptorSetCache
{
    // Cache mapping FileDescriptorSet instances to their parsed descriptor lookups
    static readonly ConditionalWeakTable<FileDescriptorSet, Holder> table = new();

    /// <summary>
    /// Retrieves a message descriptor by its fully qualified name from the given FileDescriptorSet.
    /// Throws if not found.
    /// </summary>
    /// <param name="set">The FileDescriptorSet</param>
    /// <param name="fullName">The fully qualified name of the message to find</param>
    /// <returns>The DescriptorProto for the message</returns>
    internal static DescriptorProto GetMessage(FileDescriptorSet set, string fullName)
        => GetHolder(set).Messages.TryGetValue(Trim(fullName), out var d)
           ? d : throw new InvalidOperationException($"Message “{fullName}” not found");

    /// <summary>
    /// Retrieves an enum descriptor by its fully qualified name from the given FileDescriptorSet.
    /// Throws if not found.
    /// </summary>
    /// <param name="set">The FileDescriptorSet</param>
    /// <param name="fullName">The fully qualified name of the enum to find</param>
    /// <returns>The EnumDescriptorProto for the enum</returns>
    internal static EnumDescriptorProto GetEnum(FileDescriptorSet set, string fullName)
        => GetHolder(set).Enums.TryGetValue(Trim(fullName), out var d)
           ? d : throw new InvalidOperationException($"Enum “{fullName}” not found");

    /// <summary>
    /// Attempts to look up a message descriptor by name. Returns false if not present.
    /// </summary>
    /// <param name="set">The FileDescriptorSet</param>
    /// <param name="fullName">The fully qualified name of the message to find</param>
    /// <param name="desc">The DescriptorProto for the message</param>
    /// <returns>True if the message was found in the FileDescriptorSet, false otherwise</returns>
    internal static bool TryGetMessage(FileDescriptorSet set, string fullName, out DescriptorProto? desc)
    {
        if (table.TryGetValue(set, out var holder) && holder.Messages.TryGetValue(Trim(fullName), out var hit))
        {
            desc = hit;
            return true;
        }
        desc = null;
        return false;
    }

    // Retrieves or builds the Holder for a given FileDescriptorSet
    static Holder GetHolder(FileDescriptorSet set) => table.GetValue(set, Build);

    // Builds the Holder by iterating through all files, capturing top-level and nested messages/enums
    static Holder Build(FileDescriptorSet set)
    {
        var msg = new Dictionary<string, DescriptorProto>(StringComparer.Ordinal);
        var enm = new Dictionary<string, EnumDescriptorProto>(StringComparer.Ordinal);

        foreach (var file in set.File)
        {
            // Prefix namespace if package is specified
            var pkgPrefix = string.IsNullOrEmpty(file.Package) ? "" : file.Package + ".";

            // Add top-level enums with their package prefix
            foreach (var e in file.EnumType)
            {
                enm[pkgPrefix + e.Name] = e;
            }

            // Recurse into top-level messages (including nested types)
            foreach (var m in file.MessageType)
            {
                AddMessageRecursive(m, pkgPrefix, msg, enm);
            }
        }
        return new Holder(msg, enm);
    }

    // Recursively adds a DescriptorProto and its nested types/enums to the dictionaries
    static void AddMessageRecursive(DescriptorProto m, string prefix, IDictionary<string, DescriptorProto> msg, IDictionary<string, EnumDescriptorProto> enm)
    {
        var full = prefix + m.Name;
        msg[full] = m;

        // Add any enums declared directly inside this message
        foreach (var e in m.EnumType)
        {
            enm[full + "." + e.Name] = e;
        }

        // Recurse into nested messages, updating prefix to include current message name
        foreach (var nested in m.NestedType)
        {
            AddMessageRecursive(nested, full + ".", msg, enm);
        }
    }

    // Removes a leading dot from a fully qualified name, if present
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static string Trim(string fullName)
        => fullName.StartsWith('.') ? fullName[1..] : fullName;

    // Holder keeps dictionaries of message and enum descriptors for a FileDescriptorSet
    sealed record Holder(
        IReadOnlyDictionary<string, DescriptorProto> Messages,
        IReadOnlyDictionary<string, EnumDescriptorProto> Enums);
}