using System.Numerics;
using EvolutionaryOrganic.Core.Genetics;
using EvolutionaryOrganic.Core.Genetics.Imaging;

namespace EvolutionaryOrganic.Core.Evolution;

/// <summary>Creates random textures, bump maps and image trees.</summary>
public static class RandomImageFactory
{
    public static TextureGene CreateTexture(Random rng)
    {
        // A dark-to-light ramp around one hue reads clearly as a pattern; extra stops add accents.
        float hue = rng.NextFloat(0f, 360f);
        int stops = rng.Chance(0.6) ? 2 : rng.Next(3, TextureGene.MaxStops + 1);
        var palette = new List<HsvColor>(stops);
        for (int i = 0; i < stops; i++)
        {
            float t = stops == 1 ? 1f : (float)i / (stops - 1);
            palette.Add(new HsvColor(
                hue + rng.NextGaussian() * 40f,
                rng.NextFloat(0.15f, 0.85f),
                0.15f + t * 0.8f + rng.NextGaussian() * 0.08f).Normalized());
        }
        return new TextureGene { Pattern = CreateImage(rng, rng.Next(0, 3)), Palette = palette };
    }

    public static BumpGene CreateBumpMap(Random rng) => new()
    {
        Pattern = CreateImage(rng, rng.Next(0, 3)),
        Depth = rng.NextFloat(0.01f, 0.08f),
    };

    /// <summary>Creates a random image tree with at most <paramref name="depth"/> levels of operations above its leaves.</summary>
    public static ImageNode CreateImage(Random rng, int depth)
    {
        if (depth <= 0) return CreatePattern(rng);

        return rng.PickWeighted(0.25, 0.2, 0.1, 0.2, 0.15, 0.1) switch
        {
            0 => CreatePattern(rng),
            1 => CreateWarp(rng, CreateImage(rng, depth - 1)),
            2 => CreateTransform(rng, CreateImage(rng, depth - 1)),
            3 => CreateTone(rng, CreateImage(rng, depth - 1)),
            4 => CreateBlend(rng, CreateImage(rng, depth - 1), CreateImage(rng, depth - 1)),
            _ => CreateMask(rng, CreateImage(rng, depth - 1), CreateImage(rng, depth - 1), CreateImage(rng, depth - 1)),
        };
    }

    public static PatternImageNode CreatePattern(Random rng) => rng.PickWeighted(0.45, 0.3, 0.25) switch
    {
        0 => new NoiseImageNode
        {
            Style = (NoiseStyle)rng.Next(3),
            Frequency = rng.NextFloat(1.5f, 8f),
            Octaves = rng.Next(1, 5),
            Seed = rng.Next(),
        },
        1 => new CellsImageNode
        {
            Style = (CellStyle)rng.Next(2),
            Frequency = rng.NextFloat(2f, 10f),
            Seed = rng.Next(),
        },
        _ => new WavesImageNode
        {
            Axis = RandomDirection(rng),
            Frequency = rng.NextFloat(2f, 14f),
            Twist = rng.Chance(0.35) ? rng.Next(1, 5) * (rng.Chance(0.5) ? 1 : -1) : 0,
            Phase = rng.NextFloat(0f, 360f),
            Seed = rng.Next(),
        },
    };

    public static WarpImageNode CreateWarp(Random rng, ImageNode input) => new()
    {
        Input = input,
        Amount = rng.NextFloat(0.05f, 0.5f),
        Frequency = rng.NextFloat(1f, 5f),
        Seed = rng.Next(),
    };

    public static TransformImageNode CreateTransform(Random rng, ImageNode input) => new()
    {
        Input = input,
        Scale = MathF.Exp(rng.NextGaussian() * 0.5f),
        Rotation = new Vector3(rng.NextFloat(-180f, 180f), rng.NextFloat(-180f, 180f), rng.NextFloat(-180f, 180f)),
    };

    public static ToneImageNode CreateTone(Random rng, ImageNode input)
    {
        var function = (ToneFunction)rng.Next(5);
        return new ToneImageNode
        {
            Input = input,
            Function = function,
            Amount = function switch
            {
                ToneFunction.Contrast => rng.NextFloat(1.5f, 6f),
                ToneFunction.Posterize => rng.Next(2, 6),
                _ => rng.NextFloat(2f, 6f),
            },
        };
    }

    public static BlendImageNode CreateBlend(Random rng, ImageNode a, ImageNode b) => new()
    {
        A = a,
        B = b,
        Mode = (BlendMode)rng.Next(6),
        Weight = rng.NextFloat(0.3f, 1f),
    };

    public static MaskImageNode CreateMask(Random rng, ImageNode a, ImageNode b, ImageNode mask) => new()
    {
        A = a,
        B = b,
        Mask = mask,
        Sharpness = rng.NextFloat(1f, 8f),
    };

    internal static Vector3 RandomDirection(Random rng)
    {
        var v = new Vector3(rng.NextGaussian(), rng.NextGaussian(), rng.NextGaussian());
        return v.LengthSquared() > 1e-6f ? Vector3.Normalize(v) : Vector3.UnitY;
    }
}
