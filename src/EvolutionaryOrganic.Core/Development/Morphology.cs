using System.Numerics;
using EvolutionaryOrganic.Core.Genetics;

namespace EvolutionaryOrganic.Core.Development;

/// <summary>
/// One sphere of an expanded creation, in world space. Colour is sRGB in [0, 1]; for a textured sphere it is the
/// tint multiplied over the texture. <see cref="Orientation"/> turns the sphere's texture and bumps with the
/// structure it belongs to. <see cref="Texture"/> and <see cref="BumpMap"/> index the morphology's baked images,
/// or are -1 for none.
/// </summary>
public readonly record struct SphereInstance(
    Vector3 Position,
    float Radius,
    Vector3 Color,
    Quaternion Orientation,
    float Roughness,
    int Texture = -1,
    int BumpMap = -1);

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
}

/// <summary>The phenotype: the set of spheres a genome grows into.</summary>
public sealed class Morphology
{
    public Morphology(
        IReadOnlyList<SphereInstance> spheres,
        bool truncated,
        SurfaceGenes surface,
        IReadOnlyList<BakedImage?>? textures = null,
        IReadOnlyList<BakedImage?>? bumpMaps = null)
    {
        Spheres = spheres;
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
        Center = (min + max) * 0.5f;
        float radius = 0f;
        foreach (var s in spheres) radius = MathF.Max(radius, Vector3.Distance(s.Position, Center) + s.Radius);
        BoundingRadius = MathF.Max(radius, 1e-4f);
    }

    public IReadOnlyList<SphereInstance> Spheres { get; }

    /// <summary>True if the sphere budget ran out before the whole genome was expanded.</summary>
    public bool Truncated { get; }

    public SurfaceGenes Surface { get; }

    /// <summary>Baked colour textures, parallel to <see cref="Genome.Textures"/>; null where no sphere uses one.</summary>
    public IReadOnlyList<BakedImage?> Textures { get; }

    /// <summary>Baked normal maps, parallel to <see cref="Genome.BumpMaps"/>; null where no sphere uses one.</summary>
    public IReadOnlyList<BakedImage?> BumpMaps { get; }

    public Vector3 Center { get; }

    /// <summary>Radius of a sphere about <see cref="Center"/> that encloses every sphere.</summary>
    public float BoundingRadius { get; }
}
