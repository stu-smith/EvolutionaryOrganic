using System.Numerics;
using Morphogen.Core.Genetics;

namespace Morphogen.Core.Development;

/// <summary>
/// One sphere of an expanded creation, in world space. Colour is sRGB in [0, 1]; for a textured sphere it is white,
/// so that multiplying it over the texture leaves the texture unchanged. <see cref="Orientation"/> turns the sphere's texture and bumps with the
/// structure it belongs to. <see cref="Texture"/> and <see cref="BumpMap"/> index the morphology's baked images,
/// or are -1 for none. <see cref="Fusion"/> is how far the sphere flows into its neighbours, as a fraction of its radius.
/// </summary>
public readonly record struct SphereInstance(
    Vector3 Position,
    float Radius,
    Vector3 Color,
    Quaternion Orientation,
    float Roughness,
    int Texture = -1,
    int BumpMap = -1,
    float Fusion = 0f);

/// <summary>Bounds on how far a genome is expanded, which keeps rendering interactive.</summary>
public sealed record MorphologyLimits
{
    public static MorphologyLimits Default { get; } = new();

    public int MaxSpheres { get; init; } = 4000;

    public int MaxDepth { get; init; } = 32;

    /// <summary>Spheres (and whole subtrees) smaller than this are not produced.</summary>
    public float MinRadius { get; init; } = 1e-3f;

    /// <summary>Width in pixels of baked textures and bump maps; their height is half this.</summary>
    public int TextureSize { get; init; } = 512;

    /// <summary>Grid cells across the whole creation when meshing fused spheres; small clusters get finer cells.</summary>
    public int FusionResolution { get; init; } = 160;

    /// <summary>Budget of field samples for meshing fused spheres; past it the grid is coarsened.</summary>
    public long MaxFusionSamples { get; init; } = 3_000_000;
}

/// <summary>The phenotype: the set of spheres a genome grows into.</summary>
public sealed class Morphology
{
    public Morphology(
        IReadOnlyList<SphereInstance> spheres,
        bool truncated,
        SurfaceGenes surface,
        IReadOnlyList<BakedImage?>? textures = null,
        IReadOnlyList<BakedImage?>? bumpMaps = null,
        IReadOnlyList<FusedSurface>? surfaces = null)
    {
        Spheres = spheres;
        Surfaces = surfaces ?? [];
        FusedSpheres = Surfaces.SelectMany(f => f.Spheres).ToHashSet();
        Truncated = truncated;
        Surface = surface;
        Textures = textures ?? [];
        BumpMaps = bumpMaps ?? [];

        if (spheres.Count == 0)
        {
            Center = Vector3.Zero;
            BoundingRadius = 1f;
            return;
        }

        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var s in spheres)
        {
            min = Vector3.Min(min, s.Position - new Vector3(s.Radius));
            max = Vector3.Max(max, s.Position + new Vector3(s.Radius));
        }
        // Fillets stay within the spheres' bounds, but bumps can raise a fused surface beyond them.
        foreach (var p in Surfaces.SelectMany(f => f.Positions))
        {
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
        Center = (min + max) * 0.5f;
        float radius = 0f;
        foreach (var s in spheres) radius = MathF.Max(radius, Vector3.Distance(s.Position, Center) + s.Radius);
        foreach (var p in Surfaces.SelectMany(f => f.Positions)) radius = MathF.Max(radius, Vector3.Distance(p, Center));
        BoundingRadius = MathF.Max(radius, 1e-4f);
    }

    public IReadOnlyList<SphereInstance> Spheres { get; }

    /// <summary>True if the sphere budget ran out before the whole genome was expanded.</summary>
    public bool Truncated { get; }

    /// <summary>Meshes of the spheres that fuse together; those spheres are drawn as part of these, not on their own.</summary>
    public IReadOnlyList<FusedSurface> Surfaces { get; }

    /// <summary>Indices into <see cref="Spheres"/> of the spheres drawn as part of a <see cref="Surfaces">surface</see>.</summary>
    public IReadOnlySet<int> FusedSpheres { get; }

    public SurfaceGenes Surface { get; }

    /// <summary>Baked colour textures, parallel to <see cref="Genome.Textures"/>; null where no sphere uses one.</summary>
    public IReadOnlyList<BakedImage?> Textures { get; }

    /// <summary>Baked normal maps, parallel to <see cref="Genome.BumpMaps"/>; null where no sphere uses one.</summary>
    public IReadOnlyList<BakedImage?> BumpMaps { get; }

    public Vector3 Center { get; }

    /// <summary>Radius of a sphere about <see cref="Center"/> that encloses every sphere.</summary>
    public float BoundingRadius { get; }
}
