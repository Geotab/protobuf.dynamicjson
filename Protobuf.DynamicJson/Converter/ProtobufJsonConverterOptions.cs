namespace Protobuf.DynamicJson.Converter;

/// <summary>
/// Configuration options for customizing the behavior of <see cref="ProtobufJsonConverter"/>,
/// specifically around descriptor caching policies.
/// </summary>
public class ProtobufJsonConverterOptions
{
    /// <summary>
    /// The maximum number of descriptor entries allowed in the cache.
    /// Defaults to 100.
    /// </summary>
    public long DescriptorCacheSizeLimit { get; init; } = 200;
}