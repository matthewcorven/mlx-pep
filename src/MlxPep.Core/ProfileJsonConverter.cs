namespace MlxPep.Core;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Custom JSON converter for Profile to handle serialization/deserialization of complex types.
/// Issue #8: core: profile schema records + STJ source-gen + JSONL validation
/// </summary>
public class ProfileJsonConverter : JsonConverter<Profile>
{
    public override Profile Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var jsonDoc = JsonDocument.ParseValue(ref reader);
        var root = jsonDoc.RootElement;

        return new Profile(
            SchemaVersion: root.GetProperty("schemaVersion").GetInt32(),
            Id: root.GetProperty("id").GetString() ?? "",
            ModelHfId: root.GetProperty("modelHfId").GetString() ?? "",
            Tier: root.GetProperty("tier").GetString() ?? "",
            Engine: root.GetProperty("engine").GetString() ?? "",
            System: JsonSerializer.Deserialize<Dictionary<string, object>>(root.GetProperty("system").GetRawText()) ?? new(),
            OMLXSettings: JsonSerializer.Deserialize<Dictionary<string, object>>(root.GetProperty("omlx").GetRawText()) ?? new(),
            Harness: JsonSerializer.Deserialize<Dictionary<string, object>>(root.GetProperty("harness").GetRawText()) ?? new(),
            Provenance: JsonSerializer.Deserialize<ProfileProvenance>(root.GetProperty("provenance").GetRawText()) ?? throw new InvalidOperationException("provenance is required"),
            Hardware: JsonSerializer.Deserialize<HardwareFingerprint>(root.GetProperty("hardware").GetRawText()) ?? throw new InvalidOperationException("hardware is required"),
            Sampler: root.TryGetProperty("sampler", out var samplerElem) && !samplerElem.ValueKind.Equals(JsonValueKind.Null)
                ? JsonSerializer.Deserialize<SamplerSettings>(samplerElem.GetRawText())
                : null
        );
    }

    public override void Write(Utf8JsonWriter writer, Profile value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WriteNumber("schemaVersion", value.SchemaVersion);
        writer.WriteString("id", value.Id);
        writer.WriteString("modelHfId", value.ModelHfId);
        writer.WriteString("tier", value.Tier);
        writer.WriteString("engine", value.Engine);

        writer.WritePropertyName("system");
        JsonSerializer.Serialize(writer, value.System, options);

        writer.WritePropertyName("omlx");
        JsonSerializer.Serialize(writer, value.OMLXSettings, options);

        writer.WritePropertyName("harness");
        JsonSerializer.Serialize(writer, value.Harness, options);

        writer.WritePropertyName("provenance");
        JsonSerializer.Serialize(writer, value.Provenance, options);

        writer.WritePropertyName("hardware");
        JsonSerializer.Serialize(writer, value.Hardware, options);

        if (value.Sampler != null)
        {
            writer.WritePropertyName("sampler");
            JsonSerializer.Serialize(writer, value.Sampler, options);
        }

        writer.WriteEndObject();
    }
}
