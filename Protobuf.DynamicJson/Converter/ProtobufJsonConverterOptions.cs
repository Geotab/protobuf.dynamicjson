namespace Protobuf.DynamicJson.Converter;

/// <summary>
/// Configuration options for customizing the behavior of <see cref="ProtobufJsonConverter"/>,
/// specifically around descriptor caching policies.
/// </summary>
public class ProtobufJsonConverterOptions
{
    /// <summary>
    /// The maximum number of descriptor entries allowed in the cache.
    /// Defaults to 200.
    /// </summary>
    public long DescriptorCacheSizeLimit { get; init; } = 200;
    
    /// <summary>
    /// Controls which field name is used when emitting JSON.
    /// </summary>
    public JsonFieldNamingPolicy OutputFieldNaming { get; init; } = JsonFieldNamingPolicy.JsonName;

    /// <summary>
    /// If true, the writer accepts both snake_case (proto field name) and camelCase (json_name) on input.
    /// </summary>
    public bool AcceptBothInputNames { get; init; } = true;
}

public enum JsonFieldNamingPolicy
{
    ProtoName, // snake_case (field.Name)
    JsonName   // camelCase (field.JsonName)
}