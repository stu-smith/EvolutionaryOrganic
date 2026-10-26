using System.Text.Json.Serialization;

namespace EvolutionaryOrganic.Core.Genetics.Imaging;

/// <summary>
/// A node in a procedural image tree. Every node defines a colour field over 3D space, each channel nominally in
/// [0, 1]; textures sample it on the surface of the unit sphere, so the result has no seams and no pinching at the
/// poles. Leaves generate patterns and colour them, and inner nodes reshape, distort or combine the images of their
/// inputs. Bump maps use the same trees in greyscale: their leaves have no palette.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(NoiseImageNode), "noise")]
[JsonDerivedType(typeof(CellsImageNode), "cells")]
[JsonDerivedType(typeof(WavesImageNode), "waves")]
[JsonDerivedType(typeof(WarpImageNode), "warp")]
[JsonDerivedType(typeof(TransformImageNode), "transform")]
[JsonDerivedType(typeof(ToneImageNode), "tone")]
[JsonDerivedType(typeof(BlendImageNode), "blend")]
[JsonDerivedType(typeof(MaskImageNode), "mask")]
public abstract class ImageNode
{
    [JsonIgnore]
    public abstract string DisplayName { get; }

    [JsonIgnore]
    public abstract IReadOnlyList<ImageNode> Inputs { get; }

    public abstract ImageNode DeepClone();

    /// <summary>Replaces a direct input, returning false if <paramref name="existing"/> is not an input.</summary>
    public virtual bool ReplaceInput(ImageNode existing, ImageNode replacement) => false;

    public virtual IEnumerable<GeneProperty> DescribeProperties() => [];

    /// <summary>Enumerates this node and all its descendants, depth first.</summary>
    public IEnumerable<ImageNode> DescendantsAndSelf()
    {
        var stack = new Stack<ImageNode>();
        stack.Push(this);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            yield return node;
            for (int i = node.Inputs.Count - 1; i >= 0; i--) stack.Push(node.Inputs[i]);
        }
    }

    /// <summary>Finds the node whose input is <paramref name="target"/> within this subtree.</summary>
    public ImageNode? FindParentOf(ImageNode target) =>
        DescendantsAndSelf().FirstOrDefault(n => n.Inputs.Any(i => ReferenceEquals(i, target)));

    /// <summary>Depth of the deepest path below this node (a leaf has depth 1).</summary>
    public int Depth() => 1 + (Inputs.Count == 0 ? 0 : Inputs.Max(c => c.Depth()));
}

/// <summary>Base for leaf nodes, which generate a pattern from nothing.</summary>
public abstract class PatternImageNode : ImageNode
{
    public const int MaxStops = 4;

    /// <summary>Feature density: roughly how many features span the sphere's diameter.</summary>
    public float Frequency { get; set; } = 4f;

    /// <summary>Selects a different but statistically identical pattern.</summary>
    public int Seed { get; set; }

    /// <summary>
    /// Colours spaced evenly along a gradient, from the pattern's darkest value to its lightest; null in bump maps,
    /// which are greyscale.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<HsvColor>? Palette { get; set; }

    [JsonIgnore]
    public override IReadOnlyList<ImageNode> Inputs => [];

    /// <summary>The pattern's own genes, followed by its palette.</summary>
    public sealed override IEnumerable<GeneProperty> DescribeProperties()
    {
        foreach (var p in DescribePattern()) yield return p;
        foreach (var color in Palette ?? []) yield return new("Colour", color.ToString());
    }

    protected abstract IEnumerable<GeneProperty> DescribePattern();

    protected T CopyPaletteTo<T>(T clone) where T : PatternImageNode
    {
        clone.Palette = Palette is null ? null : [.. Palette];
        return clone;
    }
}

/// <summary>Base for nodes with a single input.</summary>
public abstract class FilterImageNode : ImageNode
{
    public ImageNode Input { get; set; } = new NoiseImageNode();

    [JsonIgnore]
    public override IReadOnlyList<ImageNode> Inputs => [Input];

    public override bool ReplaceInput(ImageNode existing, ImageNode replacement)
    {
        if (!ReferenceEquals(Input, existing)) return false;
        Input = replacement;
        return true;
    }
}
