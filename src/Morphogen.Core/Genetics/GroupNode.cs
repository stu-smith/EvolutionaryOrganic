using System.Text.Json.Serialization;

namespace Morphogen.Core.Genetics;

/// <summary>Combines several independent sub-structures into one.</summary>
public sealed class GroupNode : GenomeNode
{
    [JsonPropertyName("children")]
    public List<GenomeNode> Items { get; set; } = [];

    [JsonIgnore]
    public override string DisplayName => "Group";

    [JsonIgnore]
    public override IReadOnlyList<GenomeNode> Children => Items;

    public override GenomeNode DeepClone()
    {
        var clone = new GroupNode { Items = Items.Select(c => c.DeepClone()).ToList() };
        return CopyBaseTo(clone);
    }

    public override bool ReplaceChild(GenomeNode existing, GenomeNode replacement)
    {
        int index = Items.FindIndex(c => ReferenceEquals(c, existing));
        if (index < 0) return false;
        Items[index] = replacement;
        return true;
    }

    public override IEnumerable<GeneProperty> DescribeProperties()
    {
        yield return new("Parts", Items.Count.ToString());
        foreach (var p in base.DescribeProperties()) yield return p;
    }
}
