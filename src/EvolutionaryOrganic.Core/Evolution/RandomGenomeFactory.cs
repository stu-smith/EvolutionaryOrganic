using System.Numerics;
using EvolutionaryOrganic.Core.Development;
using EvolutionaryOrganic.Core.Genetics;

namespace EvolutionaryOrganic.Core.Evolution;

/// <summary>Creates random genomes and random sub-structures.</summary>
public static class RandomGenomeFactory
{
    /// <summary>
    /// Creates a random genome that grows into a reasonable number of spheres, and that may define
    /// up to <paramref name="maxTextures"/> textures and <paramref name="maxBumpMaps"/> bump maps.
    /// </summary>
    public static Genome CreateGenome(Random rng, int maxSpheres = 3000, int maxTextures = 3, int maxBumpMaps = 3)
    {
        for (int attempt = 0; ; attempt++)
        {
            var root = CreateNode(rng, rng.Next(2, 4));
            long count = MorphologyBuilder.EstimateSphereCount(root);
            if ((count >= 12 && count <= maxSpheres) || attempt > 50)
            {
                var genome = new Genome { Root = root, Surface = CreateSurface(rng) };
                if (maxTextures > 0 && rng.Chance(0.5))
                {
                    int textures = rng.Next(1, Math.Min(maxTextures, 2) + 1);
                    for (int i = 0; i < textures; i++) ImageMutator.AddTexture(genome, rng);
                }
                if (maxBumpMaps > 0 && rng.Chance(0.4)) ImageMutator.AddBumpMap(genome, rng);
                return genome;
            }
        }
    }

    public static SurfaceGenes CreateSurface(Random rng) => new()
    {
        Metallic = rng.Chance(0.2) ? rng.NextFloat(0.4f, 1f) : rng.NextFloat(0f, 0.1f),
        ClearCoat = rng.Chance(0.5) ? rng.NextFloat(0.2f, 1f) : 0f,
    };

    /// <summary>Creates a random subtree with at most <paramref name="depth"/> levels of nesting.</summary>
    public static GenomeNode CreateNode(Random rng, int depth)
    {
        if (depth <= 0) return CreateSphere(rng);

        return rng.PickWeighted(0.45, 0.3, 0.12, 0.13) switch
        {
            0 => CreateLinearRepeat(rng, CreateNode(rng, depth - 1)),
            1 => CreateRadialRepeat(rng, CreateNode(rng, depth - 1)),
            2 => CreateGroup(rng, depth - 1),
            _ => CreateSphere(rng),
        };
    }

    public static SphereNode CreateSphere(Random rng) => new()
    {
        Radius = rng.NextFloat(0.25f, 0.6f),
        Color = new HsvColor(rng.NextFloat(0f, 360f), rng.NextFloat(0.35f, 0.9f), rng.NextFloat(0.55f, 1f)),
        Roughness = rng.Chance(0.25) ? rng.NextFloat(0.04f, 0.2f) : rng.NextFloat(0.15f, 0.85f),
    };

    public static LinearRepeatNode CreateLinearRepeat(Random rng, GenomeNode child) => new()
    {
        Child = child,
        Count = rng.Next(4, 17),
        Step = new Transform3
        {
            Translation = new Vector3(rng.NextGaussian() * 0.08f, rng.NextFloat(0.25f, 0.7f), rng.NextGaussian() * 0.08f),
            Rotation = new Vector3(rng.NextGaussian() * 10f, rng.NextGaussian() * 12f, rng.NextGaussian() * 10f),
            Scale = rng.NextFloat(0.86f, 1.01f),
        },
        StepModifier = CreateStepModifier(rng),
    };

    public static RadialRepeatNode CreateRadialRepeat(Random rng, GenomeNode child) => new()
    {
        Child = child,
        Count = rng.Next(3, 11),
        Radius = rng.NextFloat(0.4f, 1.6f),
        Sweep = rng.Chance(0.75) ? 360f : rng.NextFloat(60f, 300f),
        Tilt = rng.Chance(0.6) ? rng.NextFloat(-70f, 70f) : 0f,
        StepModifier = rng.Chance(0.5) ? CreateStepModifier(rng) : PropertyModifier.None,
    };

    public static GroupNode CreateGroup(Random rng, int depth)
    {
        var group = new GroupNode();
        int parts = rng.Next(2, 4);
        for (int i = 0; i < parts; i++)
        {
            var part = CreateNode(rng, depth);
            if (i > 0) part.Offset = CreateOffset(rng);
            group.Items.Add(part);
        }
        return group;
    }

    public static Transform3 CreateOffset(Random rng) => new()
    {
        Translation = new Vector3(rng.NextGaussian(), rng.NextGaussian(), rng.NextGaussian()) * 0.6f,
        Rotation = new Vector3(rng.NextFloat(-180f, 180f), rng.NextFloat(-180f, 180f), rng.NextFloat(-180f, 180f)),
        Scale = rng.NextFloat(0.5f, 1.1f),
    };

    public static PropertyModifier CreateStepModifier(Random rng) => new()
    {
        HueShift = rng.NextGaussian() * 8f,
        SaturationScale = 1f + rng.NextGaussian() * 0.03f,
        ValueScale = 1f + rng.NextGaussian() * 0.03f,
        SizeScale = rng.Chance(0.5) ? 1f + rng.NextGaussian() * 0.05f : 1f,
    };
}
