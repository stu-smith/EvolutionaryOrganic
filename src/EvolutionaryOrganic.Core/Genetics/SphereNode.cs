using System.Text.Json.Serialization;

namespace EvolutionaryOrganic.Core.Genetics;

/// <summary>A leaf node: the sphere that every creation is ultimately built from.</summary>
public sealed class SphereNode : GenomeNode
{
    public float Radius { get; set; } = 0.5f;

    public HsvColor Color { get; set; } = new(30f, 0.6f, 0.9f);

    [JsonIgnore]
    public override string DisplayName => "Sphere";

    [JsonIgnore]
    public override IReadOnlyList<GenomeNode> Children => [];

    public override GenomeNode DeepClone() => CopyBaseTo(new SphereNode { Radius = Radius, Color = Color });

    public override bool ReplaceChild(GenomeNode existing, GenomeNode replacement) => false;

    public override IEnumerable<GeneProperty> DescribeProperties()
    {
        yield return new("Radius", Radius.ToString("0.###"));
        yield return new("Color", Color.ToString());
        foreach (var p in base.DescribeProperties()) yield return p;
    }
}
