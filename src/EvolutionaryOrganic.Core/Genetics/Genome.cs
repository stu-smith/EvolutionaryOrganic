namespace EvolutionaryOrganic.Core.Genetics;

/// <summary>Genes that control the surface finish of the whole creation.</summary>
public sealed record SurfaceGenes
{
    /// <summary>0 = mirror-smooth, 1 = completely matte.</summary>
    public float Roughness { get; init; } = 0.35f;

    /// <summary>0 = dielectric (plastic, flesh), 1 = metal.</summary>
    public float Metallic { get; init; } = 0.05f;

    /// <summary>Strength of a glossy lacquer layer over the base surface.</summary>
    public float ClearCoat { get; init; } = 0.4f;

    public override string ToString() => $"rough {Roughness:0.00}, metal {Metallic:0.00}, coat {ClearCoat:0.00}";
}

/// <summary>A complete genome: the morphology tree plus whole-creation genes.</summary>
public sealed class Genome
{
    public const int CurrentFormatVersion = 1;

    public int FormatVersion { get; set; } = CurrentFormatVersion;

    /// <summary>How many selections led to this genome.</summary>
    public int Generation { get; set; }

    public SurfaceGenes Surface { get; set; } = new();

    public GenomeNode Root { get; set; } = new SphereNode();

    public Genome DeepClone() => new()
    {
        FormatVersion = FormatVersion,
        Generation = Generation,
        Surface = Surface,
        Root = Root.DeepClone(),
    };

    /// <summary>
    /// Replaces <paramref name="existing"/> (anywhere in the tree, including the root)
    /// with <paramref name="replacement"/>.
    /// </summary>
    public bool Replace(GenomeNode existing, GenomeNode replacement)
    {
        if (ReferenceEquals(Root, existing))
        {
            Root = replacement;
            return true;
        }
        return Root.FindParentOf(existing)?.ReplaceChild(existing, replacement) ?? false;
    }
}
