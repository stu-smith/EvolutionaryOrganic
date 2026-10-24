using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using EvolutionaryOrganic.Core.Genetics;

namespace EvolutionaryOrganic.Core.Serialization;

/// <summary>Reads and writes genomes as human-readable JSON.</summary>
public static class GenomeSerializer
{
    public const string FileExtension = ".genome.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new Vector3Converter() },
    };

    private static readonly JsonSerializerOptions CompactOptions = new(Options) { WriteIndented = false };

    public static string Serialize(Genome genome) => JsonSerializer.Serialize(genome, Options);

    /// <summary>Serializes any part of a genome on one line, for use as a content key.</summary>
    public static string SerializeCompact(object value, Type type) => JsonSerializer.Serialize(value, type, CompactOptions);

    public static Genome Deserialize(string json)
    {
        var genome = JsonSerializer.Deserialize<Genome>(json, Options)
                     ?? throw new InvalidDataException("The file does not contain a genome.");
        if (genome.FormatVersion > Genome.CurrentFormatVersion)
        {
            throw new InvalidDataException(
                $"This genome uses format version {genome.FormatVersion}, but only versions up to {Genome.CurrentFormatVersion} are supported.");
        }
        if (genome.Root is null) throw new InvalidDataException("The genome has no root node.");
        if (genome.FormatVersion < 2) MigrateFromVersion1(genome, json);
        genome.Textures ??= [];
        genome.BumpMaps ??= [];
        return genome;
    }

    /// <summary>Version 1 had a single roughness for the whole creation; it now belongs to each sphere.</summary>
    private static void MigrateFromVersion1(Genome genome, string json)
    {
        if (JsonNode.Parse(json)?["surface"]?["roughness"] is JsonValue value && value.TryGetValue(out float roughness))
        {
            foreach (var sphere in genome.Root.DescendantsAndSelf().OfType<SphereNode>()) sphere.Roughness = roughness;
        }
        genome.FormatVersion = Genome.CurrentFormatVersion;
    }

    public static void Save(Genome genome, string path) => File.WriteAllText(path, Serialize(genome));

    public static Genome Load(string path) => Deserialize(File.ReadAllText(path));

    /// <summary>Writes vectors compactly as <c>[x, y, z]</c>.</summary>
    private sealed class Vector3Converter : JsonConverter<Vector3>
    {
        public override Vector3 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("Expected [x, y, z].");
            Span<float> values = stackalloc float[3];
            for (int i = 0; i < 3; i++)
            {
                if (!reader.Read() || reader.TokenType != JsonTokenType.Number) throw new JsonException("Expected [x, y, z].");
                values[i] = reader.GetSingle();
            }
            if (!reader.Read() || reader.TokenType != JsonTokenType.EndArray) throw new JsonException("Expected [x, y, z].");
            return new Vector3(values[0], values[1], values[2]);
        }

        public override void Write(Utf8JsonWriter writer, Vector3 value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(value.X);
            writer.WriteNumberValue(value.Y);
            writer.WriteNumberValue(value.Z);
            writer.WriteEndArray();
        }
    }
}
