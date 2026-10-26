using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Morphogen.Core.Genetics;
using Morphogen.Core.Genetics.Imaging;

namespace Morphogen.Core.Serialization;

/// <summary>Reads and writes genomes as human-readable JSON.</summary>
public static class GenomeSerializer
{
    public const string FileExtension = ".genome.json";

    public const string ProjectFileExtension = ".morphogen.json";

    /// <summary>The project extension used before the app was renamed to Morphogen; such files still open.</summary>
    public const string LegacyProjectFileExtension = ".organic.json";

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

    public static Genome Deserialize(string json) => Deserialize(JsonNode.Parse(json));

    private static Genome Deserialize(JsonNode? json)
    {
        var genome = JsonSerializer.Deserialize<Genome>(json, Options)
                     ?? throw new InvalidDataException("The file does not contain a genome.");
        if (genome.FormatVersion > Genome.CurrentFormatVersion)
        {
            throw new InvalidDataException(
                $"This genome uses format version {genome.FormatVersion}, but only versions up to {Genome.CurrentFormatVersion} are supported.");
        }
        if (genome.Root is null) throw new InvalidDataException("The genome has no root node.");
        genome.Textures ??= [];
        genome.BumpMaps ??= [];
        if (genome.FormatVersion < 2) MigrateFromVersion1(genome, json);
        // Version 3 only added genes whose absence means "off", so nothing needs migrating for it.
        if (genome.FormatVersion < 4) MigrateFromVersion3(genome, json);
        genome.FormatVersion = Genome.CurrentFormatVersion;

        // A textured sphere omits its colour, which would otherwise read back as the default.
        foreach (var sphere in genome.Root.DescendantsAndSelf().OfType<SphereNode>())
        {
            if (sphere.Texture is not null) sphere.Color = null;
            else sphere.Color ??= SphereNode.DefaultColor;
        }
        return genome;
    }

    /// <summary>
    /// Version 3 mapped each texture's greyscale pattern through one palette, then tinted it with the sphere's colour.
    /// Now each pattern leaf has its own palette, so the texture's palette is copied onto every leaf; the tint is dropped.
    /// </summary>
    private static void MigrateFromVersion3(Genome genome, JsonNode? json)
    {
        if (json?["textures"] is not JsonArray textures) return;
        for (int i = 0; i < textures.Count && i < genome.Textures.Count; i++)
        {
            if (textures[i]?["palette"]?.Deserialize<List<HsvColor>>(Options) is not { Count: > 0 } palette) continue;
            foreach (var leaf in genome.Textures[i].Pattern.DescendantsAndSelf().OfType<PatternImageNode>()) leaf.Palette = [.. palette];
        }
    }

    /// <summary>Version 1 had a single roughness for the whole creation; it now belongs to each sphere.</summary>
    private static void MigrateFromVersion1(Genome genome, JsonNode? json)
    {
        if (json?["surface"]?["roughness"] is JsonValue value && value.TryGetValue(out float roughness))
        {
            foreach (var sphere in genome.Root.DescendantsAndSelf().OfType<SphereNode>()) sphere.Roughness = roughness;
        }
    }

    public static void Save(Genome genome, string path) => File.WriteAllText(path, Serialize(genome));

    public static Genome Load(string path) => Deserialize(File.ReadAllText(path));

    public static string SerializeProject(Project project) => JsonSerializer.Serialize(project, Options);

    /// <summary>Reads a project, migrating each of its genomes from older formats as <see cref="Deserialize(string)"/> does.</summary>
    public static Project DeserializeProject(string json)
    {
        if (JsonNode.Parse(json) is not JsonObject root || root["genomes"] is not JsonArray genomes)
        {
            throw new InvalidDataException("The file does not contain a project.");
        }
        int version = root["formatVersion"]?.GetValue<int>() ?? Project.CurrentFormatVersion;
        if (version > Project.CurrentFormatVersion)
        {
            throw new InvalidDataException(
                $"This project uses format version {version}, but only versions up to {Project.CurrentFormatVersion} are supported.");
        }
        return new Project
        {
            Genomes = genomes.Select(g => Deserialize(g)).ToList(),
            Current = root["current"] is { } current ? Deserialize(current) : null,
        };
    }

    public static void SaveProject(Project project, string path) => File.WriteAllText(path, SerializeProject(project));

    public static Project LoadProject(string path) => DeserializeProject(File.ReadAllText(path));

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
