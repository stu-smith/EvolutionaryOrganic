using System.Text.Json.Serialization;

namespace EvolutionaryOrganic.Core.Genetics.Imaging;

/// <summary>A gene that defines a procedural image as a tree of image operations.</summary>
public abstract class ImageGene
{
    /// <summary>Image trees larger than this are pruned after mutation, which bounds the cost of baking them.</summary>
    public const int MaxNodes = 14;

    public const int MaxDepth = 6;

    public ImageNode Pattern { get; set; } = new NoiseImageNode();

    [JsonIgnore]
    public abstract string Kind { get; }

    /// <summary>
    /// Replaces <paramref name="existing"/> (anywhere in the tree, including the root)
    /// with <paramref name="replacement"/>.
    /// </summary>
    public bool Replace(ImageNode existing, ImageNode replacement)
    {
        if (ReferenceEquals(Pattern, existing))
        {
            Pattern = replacement;
            return true;
        }
        return Pattern.FindParentOf(existing)?.ReplaceInput(existing, replacement) ?? false;
    }

    public abstract IEnumerable<GeneProperty> DescribeProperties();
}

/// <summary>A colour texture: the pattern's greyscale values are mapped onto a gradient of colours.</summary>
public sealed class TextureGene : ImageGene
{
    public const int MaxStops = 4;

    /// <summary>Colours spaced evenly along the gradient, from the pattern's darkest value to its lightest.</summary>
    public List<HsvColor> Palette { get; set; } = [new(30f, 0.5f, 0.3f), new(40f, 0.3f, 1f)];

    [JsonIgnore]
    public override string Kind => "Texture";

    public TextureGene DeepClone() => new() { Pattern = Pattern.DeepClone(), Palette = [.. Palette] };

    public override IEnumerable<GeneProperty> DescribeProperties()
    {
        foreach (var color in Palette) yield return new("Palette", color.ToString());
    }
}

/// <summary>A bump map: the pattern is a height field that perturbs the surface normals.</summary>
public sealed class BumpGene : ImageGene
{
    /// <summary>Height of the pattern's brightest areas above its darkest, in sphere radii.</summary>
    public float Depth { get; set; } = 0.05f;

    [JsonIgnore]
    public override string Kind => "Bump map";

    public BumpGene DeepClone() => new() { Pattern = Pattern.DeepClone(), Depth = Depth };

    public override IEnumerable<GeneProperty> DescribeProperties()
    {
        yield return new("Depth", Depth.ToString("0.###"));
    }
}
