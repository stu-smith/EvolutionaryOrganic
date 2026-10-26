using EvolutionaryOrganic.Core.Genetics.Imaging;

namespace EvolutionaryOrganic.Core.Genetics;

/// <summary>
/// Genes that control the surface finish of the whole creation. How shiny or matte each sphere is
/// is a gene of the sphere itself (<see cref="SphereNode.Roughness"/>).
/// </summary>
public sealed record SurfaceGenes
{
    /// <summary>0 = dielectric (plastic, flesh), 1 = metal.</summary>
    public float Metallic { get; init; } = 0.05f;

    /// <summary>Strength of a glossy lacquer layer over the base surface.</summary>
    public float ClearCoat { get; init; } = 0.4f;

    public override string ToString() => $"metal {Metallic:0.00}, coat {ClearCoat:0.00}";
}

/// <summary>
/// A complete genome: the morphology tree, whole-creation genes, and the procedural textures and
/// bump maps that the tree's spheres can refer to by index.
/// </summary>
public sealed class Genome
{
    /// <summary>
    /// Version 2 moved roughness from <see cref="SurfaceGenes"/> to each sphere, and added textures and bump maps.
    /// Version 3 added fusion; older genomes have none, so they read unchanged.
    /// </summary>
    public const int CurrentFormatVersion = 3;

    public int FormatVersion { get; set; } = CurrentFormatVersion;

    /// <summary>How many selections led to this genome.</summary>
    public int Generation { get; set; }

    public SurfaceGenes Surface { get; set; } = new();

    public List<TextureGene> Textures { get; set; } = [];

    public List<BumpGene> BumpMaps { get; set; } = [];

    public GenomeNode Root { get; set; } = new SphereNode();

    public Genome DeepClone() => new()
    {
        FormatVersion = FormatVersion,
        Generation = Generation,
        Surface = Surface,
        Textures = Textures.Select(t => t.DeepClone()).ToList(),
        BumpMaps = BumpMaps.Select(b => b.DeepClone()).ToList(),
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
