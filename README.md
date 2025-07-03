# Protobuf.DynamicJson

A high-performance .NET library for dynamic, runtime conversion between canonical proto3 JSON and binary protobuf formats.

This library allows you to serialize JSON into protobuf binary and deserialize binary back into JSON, **without needing to compile static C# classes from your `.proto` files**.

It's ideal for systems where protobuf schemas are not known at compile time, such as dynamic message processors, gateways, or data migration tools.

---

## 🔑 Key Features

- **Dynamic Conversion**: No statically-generated code or C# reflection required.
- **High Performance**: Uses `protobuf-net` for fast serialization and `RecyclableMemoryStreamManager` to reduce GC pressure.
- **Schema Caching**: `FileDescriptorSet` schemas are automatically cached for reuse.
- **Canonical JSON**: Adheres to the official [proto3 JSON mapping](https://protobuf.dev/programming-guides/proto3/#json).
- **Well-Known Types**: Built-in support for Google's well-known types like `Timestamp`, `Duration`, etc.

---

## 📦 Installation

Install the package from NuGet:

```bash
dotnet add package Protobuf.DynamicJson
```

---

## 🚀 Getting Started

The library exposes two main classes:

- `ProtoDescriptorHelper`: Compiles `.proto` schema definitions into a binary descriptor set at runtime.
- `ProtobufJsonConverter`: Converts between JSON and protobuf binary using the descriptor set.

---

## ✨ Usage Example

### Sample schema: `person.proto`

```proto
syntax = "proto3";
package acme.people;

message Person {
  string name = 1;
  int32 id = 2;
  string email = 3;
}
```

### C# code

```csharp
using Protobuf.DynamicJson;
using System;
using System.IO;

// 1. Load your .proto schema definition from a file or string.
string protoSchema = File.ReadAllText("person.proto");

// 2. Compile the schema into a descriptor set at runtime.
// This is a one-time operation per schema; the result is cached internally.
byte[] descriptorSetBytes = ProtoDescriptorHelper.CompileProtoToDescriptorSetBytes(protoSchema);

// 3. Define the fully-qualified name of your top-level message.
string messageName = "acme.people.Person";

// -------------------------------------------------------
// Example 1: Convert JSON to Protobuf Binary
// -------------------------------------------------------

string personJson = """
{
  "name": "Jane Doe",
  "id": 1234,
  "email": "jane.doe@example.com"
}
""";

// Convert the JSON string into a protobuf byte array.
byte[] protoBytes = ProtobufJsonConverter.ConvertJsonToProtoBytes(personJson, messageName, descriptorSetBytes);

Console.WriteLine($"\nSuccessfully converted JSON to {protoBytes.Length} bytes of Protobuf data.");
// Output:
// Successfully converted JSON to 41 bytes of Protobuf data.

// =======================================================
//  Example 2: Convert Protobuf Binary back to JSON
// =======================================================

// Use the same byte array from the previous step.
string convertedJson = ProtobufJsonConverter.ConvertProtoBytesToJson(protoBytes, messageName, descriptorSetBytes);

Console.WriteLine($"\nConverted Protobuf back to JSON:\n{convertedJson}");
// Output:
// Converted Protobuf back to JSON:
// {"name":"Jane Doe","id":1234,"email":"jane.doe@example.com"}
```

## 📚 Dependencies

This library is built on top of the highly-performant [protobuf-net](https://www.nuget.org/packages/protobuf-net) by Marc Gravell, and uses [Google.Protobuf](https://www.nuget.org/packages/google.protobuf/) for descriptor parsing.