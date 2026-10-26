using System.Collections.Concurrent;
using System.Numerics;
using EvolutionaryOrganic.Core.Genetics;
using EvolutionaryOrganic.Core.Genetics.Imaging;
using EvolutionaryOrganic.Core.Serialization;

namespace EvolutionaryOrganic.Core.Development;

/// <summary>
/// An image baked from an <see cref="ImageGene"/>: 8-bit RGBA pixels, row by row from the top. It is laid out
/// for the sphere meshes' equirectangular UVs: u (x) runs once around the equator, v (y) from pole to pole.
/// </summary>
public sealed record BakedImage(int Width, int Height, byte[] Pixels);

/// <summary>Evaluates image trees and bakes texture and bump genes into images.</summary>
public static class ImageBaker
{
    private const int CacheCapacity = 96;

    /// <summary>
    /// Mutants usually inherit their parent's images unchanged, so baked images are cached by gene content.
    /// </summary>
    private static readonly ConcurrentDictionary<string, BakedImage> Cache = new();

    /// <summary>Bakes a colour texture, <paramref name="width"/> × <paramref name="width"/>/2 pixels.</summary>
    public static BakedImage BakeTexture(TextureGene gene, int width) =>
        Cached(gene, width, () => Bake(width, (p, _, _) => new Vector4(EvaluateColor(gene.Pattern, p), 1f)));

    /// <summary>The texture's mean colour over the whole sphere, from a sparse but even sample.</summary>
    public static Vector3 AverageColor(TextureGene gene)
    {
        const int samples = 64;
        var sum = Vector3.Zero;
        for (int i = 0; i < samples; i++)
        {
            // A Fibonacci lattice spreads the samples evenly over the sphere.
            float y = 1f - (i + 0.5f) * 2f / samples;
            float r = MathF.Sqrt(1f - y * y);
            float phi = i * 2.39996323f;
            sum += EvaluateColor(gene.Pattern, new Vector3(r * MathF.Cos(phi), y, r * MathF.Sin(phi)));
        }
        return sum / samples;
    }

    /// <summary>
    /// Bakes a bump gene into a tangent-space normal map, <paramref name="width"/> × <paramref name="width"/>/2
    /// pixels. X follows the mesh tangent (increasing u) and Y the bitangent (increasing v), matching
    /// <c>SphereMeshes</c> in the app.
    /// </summary>
    public static BakedImage BakeBumpMap(BumpGene gene, int width)
    {
        int height = Math.Max(1, width / 2);
        // Finite-difference step: about half a texel, measured along the surface.
        float step = MathF.PI / height * 0.5f;
        return Cached(gene, width, () => Bake(width, (p, tangent, bitangent) =>
        {
            float h = Evaluate(gene.Pattern, p);
            float du = (Evaluate(gene.Pattern, Vector3.Normalize(p + tangent * step)) - h) / step;
            float dv = (Evaluate(gene.Pattern, Vector3.Normalize(p + bitangent * step)) - h) / step;
            // The shader adds x·T + y·B to the normal, so storing the scaled negative gradient tilts the
            // normal away from rising ground, exactly as for a real height field.
            float x = Math.Clamp(-du * gene.Depth, -1f, 1f);
            float y = Math.Clamp(-dv * gene.Depth, -1f, 1f);
            float z = MathF.Sqrt(MathF.Max(0f, 1f - x * x - y * y));
            return new Vector4(x * 0.5f + 0.5f, y * 0.5f + 0.5f, z * 0.5f + 0.5f, 1f);
        }));
    }

    /// <summary>Evaluates an image tree in greyscale at a point; the result is nominally in [0, 1].</summary>
    public static float Evaluate(ImageNode node, Vector3 p) => Evaluate(node, p, color: false).X;

    /// <summary>Evaluates an image tree in colour at a point; each channel is nominally in [0, 1].</summary>
    public static Vector3 EvaluateColor(ImageNode node, Vector3 p) => Evaluate(node, p, color: true);

    /// <summary>
    /// In colour, each leaf maps its pattern through its palette, filters act on each channel, and a mask follows the
    /// luminance of its mask image. In greyscale, leaves ignore their palettes, so every channel is the same.
    /// </summary>
    private static Vector3 Evaluate(ImageNode node, Vector3 p, bool color)
    {
        switch (node)
        {
            case PatternImageNode pattern:
            {
                float v = EvaluatePattern(pattern, p);
                return color && pattern.Palette is { } palette ? SamplePalette(palette, v) : new Vector3(v);
            }

            case WarpImageNode warp:
                return Evaluate(warp.Input, p + ProceduralNoise.Field(p * warp.Frequency, warp.Seed) * warp.Amount, color);

            case TransformImageNode transform:
            {
                const float toRadians = MathF.PI / 180f;
                var rotation = Matrix4x4.CreateFromYawPitchRoll(
                    transform.Rotation.Y * toRadians, transform.Rotation.X * toRadians, transform.Rotation.Z * toRadians);
                return Evaluate(transform.Input, Vector3.Transform(p, rotation) * transform.Scale, color);
            }

            case ToneImageNode tone:
            {
                var v = Evaluate(tone.Input, p, color);
                return new Vector3(Tone(tone, v.X), Tone(tone, v.Y), Tone(tone, v.Z));
            }

            case BlendImageNode blend:
            {
                Vector3 a = Evaluate(blend.A, p, color), b = Evaluate(blend.B, p, color);
                var blended = new Vector3(Blend(blend.Mode, a.X, b.X), Blend(blend.Mode, a.Y, b.Y), Blend(blend.Mode, a.Z, b.Z));
                return a + (blended - a) * Saturate(blend.Weight);
            }

            case MaskImageNode mask:
            {
                var maskValue = Evaluate(mask.Mask, p, color);
                float m = Saturate(0.5f + ((color ? Luminance(maskValue) : maskValue.X) - 0.5f) * MathF.Max(mask.Sharpness, 0f));
                m = m * m * (3f - 2f * m);
                return Vector3.Lerp(Evaluate(mask.A, p, color), Evaluate(mask.B, p, color), m);
            }

            default:
                return new Vector3(0.5f);
        }
    }

    private static float EvaluatePattern(PatternImageNode node, Vector3 p)
    {
        switch (node)
        {
            case NoiseImageNode noise:
            {
                var q = p * noise.Frequency;
                return noise.Style switch
                {
                    NoiseStyle.Turbulent => Saturate(ProceduralNoise.Fractal(q, noise.Seed, noise.Octaves, MathF.Abs) * 2.2f),
                    NoiseStyle.Ridged => Squared(1f - ProceduralNoise.Fractal(q, noise.Seed, noise.Octaves, MathF.Abs) * 1.6f),
                    _ => Saturate(0.5f + ProceduralNoise.Fractal(q, noise.Seed, noise.Octaves) * 1.1f),
                };
            }

            case CellsImageNode cells:
            {
                var (f1, f2) = ProceduralNoise.Cellular(p * cells.Frequency, cells.Seed);
                return cells.Style == CellStyle.Edges
                    ? 1f - Saturate((f2 - f1) * 3f)
                    : Saturate(1f - f1 * 1.15f);
            }

            case WavesImageNode waves:
            {
                var axis = waves.Axis.LengthSquared() > 1e-8f ? Vector3.Normalize(waves.Axis) : Vector3.UnitY;
                float angle = waves.Frequency * MathF.PI * Vector3.Dot(p, axis) + waves.Phase * (MathF.PI / 180f);
                if (waves.Twist != 0)
                {
                    // Longitude about the axis, from a basis perpendicular to it.
                    var side = Vector3.Normalize(Vector3.Cross(axis, MathF.Abs(axis.X) < 0.9f ? Vector3.UnitX : Vector3.UnitZ));
                    var up = Vector3.Cross(axis, side);
                    angle += waves.Twist * MathF.Atan2(Vector3.Dot(p, up), Vector3.Dot(p, side));
                }
                return 0.5f + 0.5f * MathF.Sin(angle);
            }

            default:
                return 0.5f;
        }
    }

    private static float Tone(ToneImageNode tone, float v) => tone.Function switch
    {
        ToneFunction.Contrast => Saturate(0.5f + (v - 0.5f) * MathF.Max(tone.Amount, 0f)),
        ToneFunction.Invert => 1f - v,
        ToneFunction.Posterize => MathF.Min(MathF.Floor(Saturate(v) * tone.ToneLevels), tone.ToneLevels - 1) / (tone.ToneLevels - 1),
        ToneFunction.Ridge => 1f - MathF.Abs(2f * v - 1f),
        _ => Fraction(v * MathF.Max(tone.Amount, 1f)),
    };

    private static float Blend(BlendMode mode, float a, float b) => mode switch
    {
        BlendMode.Add => Saturate(a + b),
        BlendMode.Multiply => a * b,
        BlendMode.Difference => MathF.Abs(a - b),
        BlendMode.Lighten => MathF.Max(a, b),
        BlendMode.Darken => MathF.Min(a, b),
        _ => b,
    };

    /// <summary>Rec. 709 luma weights, so a mask's light and dark match what the eye sees.</summary>
    private static float Luminance(Vector3 rgb) => Vector3.Dot(rgb, new Vector3(0.2126f, 0.7152f, 0.0722f));

    /// <summary>Maps a value in [0, 1] onto colours spaced evenly from the first stop to the last.</summary>
    public static Vector3 SamplePalette(IReadOnlyList<HsvColor> palette, float t)
    {
        if (palette.Count == 0) return new Vector3(Saturate(t));
        if (palette.Count == 1) return palette[0].ToRgb();
        float position = Saturate(t) * (palette.Count - 1);
        int i = Math.Min((int)position, palette.Count - 2);
        // Interpolate in RGB: hues on opposite sides of the wheel then meet in a neutral tone, not a rainbow.
        return Vector3.Lerp(palette[i].ToRgb(), palette[i + 1].ToRgb(), position - i);
    }

    private delegate Vector4 Shader(Vector3 position, Vector3 tangent, Vector3 bitangent);

    private static BakedImage Bake(int width, Shader shader)
    {
        int height = Math.Max(1, width / 2);
        var pixels = new byte[width * height * 4];
        Parallel.For(0, height, y =>
        {
            float theta = (y + 0.5f) / height * MathF.PI;
            float sinTheta = MathF.Sin(theta), cosTheta = MathF.Cos(theta);
            for (int x = 0; x < width; x++)
            {
                // The same parameterisation as the sphere meshes, so texels land where the UVs say.
                float phi = (x + 0.5f) / width * MathF.PI * 2f;
                float sinPhi = MathF.Sin(phi), cosPhi = MathF.Cos(phi);
                var normal = new Vector3(sinTheta * cosPhi, cosTheta, sinTheta * sinPhi);
                var tangent = new Vector3(-sinPhi, 0f, cosPhi);
                var bitangent = Vector3.Cross(normal, tangent);

                var c = shader(normal, tangent, bitangent);
                int i = (y * width + x) * 4;
                pixels[i] = ToByte(c.X);
                pixels[i + 1] = ToByte(c.Y);
                pixels[i + 2] = ToByte(c.Z);
                pixels[i + 3] = ToByte(c.W);
            }
        });
        return new BakedImage(width, height, pixels);
    }

    private static BakedImage Cached(ImageGene gene, int width, Func<BakedImage> bake)
    {
        string key = $"{gene.Kind}|{width}|{GenomeSerializer.SerializeCompact(gene, gene.GetType())}";
        if (Cache.TryGetValue(key, out var image)) return image;
        image = bake();
        // A crude bound on memory: the working set is the current creation and its mutants, so start afresh when full.
        if (Cache.Count >= CacheCapacity) Cache.Clear();
        Cache[key] = image;
        return image;
    }

    private static float Saturate(float v) => Math.Clamp(float.IsNaN(v) ? 0f : v, 0f, 1f);

    private static float Squared(float v) => Saturate(v) * Saturate(v);

    private static float Fraction(float v) => v - MathF.Floor(v);

    private static byte ToByte(float v) => (byte)Math.Clamp((int)MathF.Round(v * 255f), 0, 255);
}
