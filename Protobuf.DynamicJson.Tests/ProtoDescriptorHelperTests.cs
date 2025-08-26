using Protobuf.DynamicJson.Descriptors;

namespace Protobuf.DynamicJson.Tests;

public sealed class ProtoDescriptorHelperTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryCompile_ReturnsFalse_AndError_WhenProtoContentIsNullOrWhitespace(string? proto)
    {
        var ok = ProtoDescriptorHelper.TryCompileProtoToDescriptorSetBytes(
            proto!,
            out var bytes,
            out var errors);

        Assert.False(ok);
        Assert.Null(bytes);
        Assert.Single(errors);
        Assert.Equal("protoContent is null or empty.", errors[0]);
    }

    [Fact]
    public void TryCompile_IncludeImports_AffectsDescriptorSize()
    {
        // imports a well-known type; both should compile, but size with imports should be larger
        const string withImport = """
                                      syntax = "proto3";
                                      import "google/protobuf/timestamp.proto";
                                      package test;

                                      message UsesWkt {
                                        google.protobuf.Timestamp when = 1;
                                      }
                                  """;

        var ok1 = ProtoDescriptorHelper.TryCompileProtoToDescriptorSetBytes(
            withImport, out var bytesNoImports, out var errorsNoImports, includeImports: false);

        var ok2 = ProtoDescriptorHelper.TryCompileProtoToDescriptorSetBytes(
            withImport, out var bytesWithImports, out var errorsWithImports, includeImports: true);

        Assert.True(ok1, string.Join("\n", errorsNoImports));
        Assert.True(ok2, string.Join("\n", errorsWithImports));
        Assert.NotNull(bytesNoImports);
        Assert.NotNull(bytesWithImports);

        // with imports should include timestamp.proto descriptors -> typically larger
        Assert.True(bytesWithImports!.Length > bytesNoImports!.Length,
            $"Expected descriptor with imports to be larger. no-imports={bytesNoImports.Length}, with-imports={bytesWithImports.Length}");
    }

    [Fact]
    public void MissingSyntaxLine_Fails()
    {
        const string proto = """
            message Foo { int32 id = 1; }
            """;
        AssertFails(proto, "syntax");
    }

    [Fact]
    public void UnknownTypeReference_Fails()
    {
        const string proto = """
            syntax = "proto3";
            message Foo { Bar missing = 1; }
            """;
        AssertFails(proto, "bar");
    }

    [Fact]
    public void DuplicateFieldNumber_Fails()
    {
        const string proto = """
            syntax = "proto3";
            message D {
              int32 a = 1;
              string b = 1;
            }
            """;
        AssertFails(proto, "is already in use by");
    }

    [Fact]
    public void DuplicateFieldName_Fails()
    {
        const string proto = """
            syntax = "proto3";
            message D {
              int32 value = 1;
              string value = 2;
            }
            """;
        AssertFails(proto, "value");
    }

    [Fact]
    public void OneofSharesExistingFieldNumber_Fails()
    {
        const string proto = """
            syntax = "proto3";
            message M {
              string a = 1;
              oneof x {
                int32 b = 1;
              }
            }
            """;
        AssertFails(proto, "is already in use by");
    }

    [Fact]
    public void ImportNotFound_Fails()
    {
        const string proto = """
            syntax = "proto3";
            import "does_not_exist.proto";
            message M { int32 a = 1; }
            """;
        AssertFails(proto, "import");
    }

    [Fact]
    public void InvalidMapKeyType_Fails()
    {
        const string proto = """
            syntax = "proto3";
            message M {
              map<float, string> bad = 1;
            }
            """;
        AssertFails(proto, "map");
    }

    [Fact]
    public void ReservedNumberUsedByField_Fails()
    {
        const string proto = """
            syntax = "proto3";
            message M {
              reserved 1, 3 to 5;
              int32 x = 1;
            }
            """;
        AssertFails(proto, "reserved");
    }

    [Fact]
    public void UnknownOption_Fails()
    {
        const string proto = """
            syntax = "proto3";
            option i_do_not_exist = true;
            message M { int32 a = 1; }
            """;
        AssertFails(proto, "option");
    }

    [Fact]
    public void BadBraces_Unbalanced_Fails()
    {
        const string proto = """
            syntax = "proto3";
            message M {
              int32 a = 1;
            // missing closing brace
            """;
        AssertFails(proto, "Unexpected end of file");
    }

    private static void AssertFails(string proto, string? mustContain = null)
    {
        var ok = ProtoDescriptorHelper.TryCompileProtoToDescriptorSetBytes(
            proto, out var bytes, out var errors);

        Assert.False(ok);
        Assert.Null(bytes);
        Assert.NotEmpty(errors);

        if (!string.IsNullOrWhiteSpace(mustContain))
        {
            var joined = string.Join("\n", errors).ToLowerInvariant();
            Assert.Contains(mustContain.ToLowerInvariant(), joined);
        }
    }
}