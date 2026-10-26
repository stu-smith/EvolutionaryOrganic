using System.Numerics;
using Morphogen.Core.Genetics;
using Morphogen.Core.Genetics.Imaging;

namespace Morphogen.Core.Evolution;

/// <summary>Creates random textures, bump maps and image trees.</summary>
public static class RandomImageFactory
{
    public static TextureGene CreateTexture(Random rng)
    {
        var gene = new TextureGene { Pattern = CreateImage(rng, rng.Next(2, 4), rng.NextFloat(0f, 360f), operationAtRoot: true) };
        ImageMutator.Prune(gene, rng);
        return gene;
    }

    public static BumpGene CreateBumpMap(Random rng)
    {
        var gene = new BumpGene { Pattern = CreateImage(rng, rng.Next(2, 4), hue: null, operationAtRoot: true), Depth = rng.NextFloat(0.01f, 0.08f) };
        ImageMutator.Prune(gene, rng);
        return gene;
    }

    /// <summary>
    /// Creates a random image tree with at most <paramref name="depth"/> levels of operations above its leaves.
    /// Leaves are coloured around <paramref name="hue"/>, or left greyscale if it is null (for bump maps).
    /// </summary>
    public static ImageNode CreateImage(Random rng, int depth, float? hue, bool operationAtRoot = false)
    {
        if (depth <= 0) return CreatePattern(rng, hue);

        return rng.PickWeighted(operationAtRoot ? 0 : 0.2, 0.18, 0.12, 0.2, 0.18, 0.12) switch
        {
            0 => CreatePattern(rng, hue),
            1 => CreateWarp(rng, CreateImage(rng, depth - 1, hue)),
            2 => CreateTransform(rng, CreateImage(rng, depth - 1, hue)),
            3 => CreateTone(rng, CreateImage(rng, depth - 1, hue)),
            4 => CreateBlend(rng, CreateImage(rng, depth - 1, hue), CreateImage(rng, depth - 1, hue)),
            _ => CreateMask(rng, CreateImage(rng, depth - 1, hue), CreateImage(rng, depth - 1, hue), CreateImage(rng, depth - 1, hue)),
        };
    }

    /// <summary>Creates a random pattern, with a palette around <paramref name="hue"/> unless it is null.</summary>
    public static PatternImageNode CreatePattern(Random rng, float? hue)
    {
        PatternImageNode pattern = rng.PickWeighted(0.45, 0.3, 0.25) switch
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
        if (hue is float h) pattern.Palette = CreatePalette(rng, h);
        return pattern;
    }

    /// <summary>
    /// A dark-to-light ramp near <paramref name="hue"/>, which reads clearly as a pattern; the leaves of one texture
    /// share a theme hue so their colours belong together, but sometimes one strikes out for contrast.
    /// </summary>
    public static List<HsvColor> CreatePalette(Random rng, float hue)
    {
        float baseHue = rng.Chance(0.2) ? hue + rng.NextFloat(90f, 270f) : hue + rng.NextGaussian() * 30f;
        int stops = rng.Chance(0.6) ? 2 : rng.Next(3, PatternImageNode.MaxStops + 1);
        var palette = new List<HsvColor>(stops);
        for (int i = 0; i < stops; i++)
        {
            float t = (float)i / (stops - 1);
            palette.Add(new HsvColor(
                baseHue + rng.NextGaussian() * 40f,
                rng.NextFloat(0.15f, 0.9f),
                0.15f + t * 0.8f + rng.NextGaussian() * 0.08f).Normalized());
        }
        return palette;
    }

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
