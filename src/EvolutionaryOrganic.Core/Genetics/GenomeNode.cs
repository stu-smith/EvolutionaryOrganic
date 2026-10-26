using System.Text.Json.Serialization;

namespace EvolutionaryOrganic.Core.Genetics;

/// <summary>A named, formatted value used to display a node's genes.</summary>
public readonly record struct GeneProperty(string Name, string Value);

/// <summary>
/// A node in the genome tree. Each node places its content via <see cref="Offset"/> and can apply a
/// <see cref="Modifier"/> to the visual properties of its whole subtree.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(SphereNode), "sphere")]
[JsonDerivedType(typeof(GroupNode), "group")]
[JsonDerivedType(typeof(LinearRepeatNode), "linearRepeat")]
[JsonDerivedType(typeof(RadialRepeatNode), "radialRepeat")]
public abstract class GenomeNode
{
    /// <summary>Placement of this node relative to its parent.</summary>
    public Transform3 Offset { get; set; } = Transform3.Identity;

    /// <summary>Property transform applied once to everything in this subtree.</summary>
    public PropertyModifier Modifier { get; set; } = PropertyModifier.None;

    /// <summary>
    /// How far the spheres in this subtree flow into one another, as a fraction of each sphere's radius:
    /// 0 keeps them separate, around 1 melts them into one body. Null inherits the parent's fusion. Fusion
    /// scales in <see cref="Modifier"/>s, including those of ancestors, still apply beneath a node that sets it.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? Fusion { get; set; }

    /// <summary>The highest effective fusion; beyond it shapes dissolve into featureless blobs.</summary>
    public const float MaxFusion = 1.5f;

    [JsonIgnore]
    public abstract string DisplayName { get; }

    [JsonIgnore]
    public abstract IReadOnlyList<GenomeNode> Children { get; }

    public abstract GenomeNode DeepClone();

    /// <summary>Replaces a direct child, returning false if <paramref name="existing"/> is not a child.</summary>
    public abstract bool ReplaceChild(GenomeNode existing, GenomeNode replacement);

    public virtual IEnumerable<GeneProperty> DescribeProperties()
    {
        if (Fusion is float fusion) yield return new("Fusion", fusion.ToString("0.00"));
        foreach (var part in Offset.DescribeParts()) yield return new("Offset", part);
        foreach (var part in Modifier.DescribeParts()) yield return new("Modifier", part);
    }

    /// <summary>Enumerates this node and all its descendants, depth first.</summary>
    public IEnumerable<GenomeNode> DescendantsAndSelf()
    {
        var stack = new Stack<GenomeNode>();
        stack.Push(this);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            yield return node;
            for (int i = node.Children.Count - 1; i >= 0; i--) stack.Push(node.Children[i]);
        }
    }

    /// <summary>Finds the parent of <paramref name="target"/> within this subtree.</summary>
    public GenomeNode? FindParentOf(GenomeNode target)
    {
        foreach (var node in DescendantsAndSelf())
        {
            foreach (var child in node.Children)
            {
                if (ReferenceEquals(child, target)) return node;
            }
        }
        return null;
    }

    /// <summary>Depth of the deepest path below this node (a leaf has depth 1).</summary>
    public int Depth() => 1 + (Children.Count == 0 ? 0 : Children.Max(c => c.Depth()));

    protected T CopyBaseTo<T>(T clone) where T : GenomeNode
    {
        clone.Offset = Offset;
        clone.Modifier = Modifier;
        clone.Fusion = Fusion;
        return clone;
    }
}
