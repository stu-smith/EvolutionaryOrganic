using System.Text.Json.Serialization;

namespace EvolutionaryOrganic.Core.Genetics;

/// <summary>A leaf node: the sphere that every creation is ultimately built from.</summary>
public sealed class SphereNode : GenomeNode
{
    public float Radius { get; set; } = 0.5f;

    public static readonly HsvColor DefaultColor = new(30f, 0.6f, 0.9f);

    /// <summary>The sphere's plain colour, or null when it shows a <see cref="Texture"/> instead.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public HsvColor? Color { get; set; } = DefaultColor;

    /// <summary>0 = mirror-shiny, 1 = completely matte.</summary>
    public float Roughness { get; set; } = 0.35f;

    /// <summary>Index into <see cref="Genome.Textures"/>, or null for a plain <see cref="Color"/>.</summary>
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

    /// <summary>Shows texture <paramref name="index"/> in place of a plain colour.</summary>
    public void ShowTexture(int index)
    {
        Texture = index;
        Color = null;
    }

    /// <summary>Shows a plain colour in place of a texture.</summary>
    public void ShowColor(HsvColor color)
    {
        Texture = null;
        Color = color;
    }

    public override bool ReplaceChild(GenomeNode existing, GenomeNode replacement) => false;

    public override IEnumerable<GeneProperty> DescribeProperties()
    {
        yield return new("Radius", Radius.ToString("0.###"));
        if (Texture is int texture) yield return new("Texture", $"#{texture + 1}");
        else if (Color is HsvColor color) yield return new("Color", color.ToString());
        yield return new("Roughness", Roughness.ToString("0.00"));
        if (BumpMap is int bump) yield return new("Bump map", $"#{bump + 1}");
        foreach (var p in base.DescribeProperties()) yield return p;
    }
}
