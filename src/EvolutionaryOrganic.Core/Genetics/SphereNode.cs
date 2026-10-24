using System.Text.Json.Serialization;

namespace EvolutionaryOrganic.Core.Genetics;

/// <summary>A leaf node: the sphere that every creation is ultimately built from.</summary>
public sealed class SphereNode : GenomeNode
{
    public float Radius { get; set; } = 0.5f;

    /// <summary>The sphere's plain colour or, when it has a <see cref="Texture"/>, the tint multiplied over it.</summary>
    public HsvColor Color { get; set; } = new(30f, 0.6f, 0.9f);

    /// <summary>0 = mirror-shiny, 1 = completely matte.</summary>
    public float Roughness { get; set; } = 0.35f;

    /// <summary>Index into <see cref="Genome.Textures"/>, or null for a plain colour.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Texture { get; set; }

    /// <summary>Index into <see cref="Genome.BumpMaps"/>, or null for a smooth surface.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? BumpMap { get; set; }

    [JsonIgnore]
    public override string DisplayName => "Sphere";

    [JsonIgnore]
    public override IReadOnlyList<GenomeNode> Children => [];

    public override GenomeNode DeepClone() => CopyBaseTo(new SphereNode
    {
        Radius = Radius,
        Color = Color,
        Roughness = Roughness,
        Texture = Texture,
        BumpMap = BumpMap,
    });

    public override bool ReplaceChild(GenomeNode existing, GenomeNode replacement) => false;

    public override IEnumerable<GeneProperty> DescribeProperties()
    {
        yield return new("Radius", Radius.ToString("0.###"));
        if (Texture is int texture)
        {
            yield return new("Texture", $"#{texture + 1}");
            yield return new("Tint", Color.ToString());
        }
        else
        {
            yield return new("Color", Color.ToString());
        }
        yield return new("Roughness", Roughness.ToString("0.00"));
        if (BumpMap is int bump) yield return new("Bump map", $"#{bump + 1}");
        foreach (var p in base.DescribeProperties()) yield return p;
    }
}
