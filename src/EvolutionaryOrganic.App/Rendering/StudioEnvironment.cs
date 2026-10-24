using System.IO;
using System.Numerics;
using HelixToolkit.SharpDX;

namespace EvolutionaryOrganic.App.Rendering;

/// <summary>
/// Generates a photographic-studio style environment cube map procedurally. A dim gradient room with
/// a few large soft boxes gives the PBR material convincing reflections and highlights without any
/// image assets. The cube map is encoded as an in-memory DDS file with a full mip chain, so rough
/// surfaces can sample blurrier reflections.
/// </summary>
public static class StudioEnvironment
{
    private const int FaceSize = 128;

    private static readonly Lazy<byte[]> DdsData = new(() => BuildDds(FaceSize));

    /// <summary>Creates a new texture model over the shared DDS data.</summary>
    public static TextureModel CreateTexture() => new(new MemoryStream(DdsData.Value, writable: false), autoCloseStream: true);

    private readonly record struct SoftBox(Vector3 Direction, float InnerCos, float OuterCos, Vector3 Color);

    private static readonly SoftBox[] Lights =
    [
        // Key: large, above and to the front-left.
        new(Vector3.Normalize(new Vector3(-0.6f, 0.75f, 0.5f)), MathF.Cos(0.30f), MathF.Cos(0.55f), new Vector3(1.0f, 0.97f, 0.92f)),
        // Fill: a tall strip on the right.
        new(Vector3.Normalize(new Vector3(0.9f, 0.15f, 0.35f)), MathF.Cos(0.18f), MathF.Cos(0.42f), new Vector3(0.55f, 0.6f, 0.68f)),
        // Rim: behind and above.
        new(Vector3.Normalize(new Vector3(0.2f, 0.5f, -0.9f)), MathF.Cos(0.22f), MathF.Cos(0.5f), new Vector3(0.85f, 0.8f, 0.75f)),
    ];

    private static Vector3 Radiance(Vector3 dir)
    {
        // Floor-to-ceiling gradient.
        float y = dir.Y;
        var floor = new Vector3(0.05f, 0.05f, 0.055f);
        var horizon = new Vector3(0.22f, 0.22f, 0.24f);
        var ceiling = new Vector3(0.36f, 0.37f, 0.40f);
        var color = y < 0f
            ? Vector3.Lerp(horizon, floor, MathF.Pow(-y, 0.6f))
            : Vector3.Lerp(horizon, ceiling, MathF.Pow(y, 0.8f));

        foreach (var light in Lights)
        {
            float c = Vector3.Dot(dir, light.Direction);
            if (c <= light.OuterCos) continue;
            float t = Math.Clamp((c - light.OuterCos) / (light.InnerCos - light.OuterCos), 0f, 1f);
            color += light.Color * (t * t * (3f - 2f * t));
        }
        return color;
    }

    /// <summary>Direction through texel (u, v) in [-1, 1] of a cube face, in D3D face order.</summary>
    private static Vector3 FaceDirection(int face, float u, float v) => Vector3.Normalize(face switch
    {
        0 => new Vector3(1f, -v, -u),
        1 => new Vector3(-1f, -v, u),
        2 => new Vector3(u, 1f, v),
        3 => new Vector3(u, -1f, -v),
        4 => new Vector3(u, -v, 1f),
        _ => new Vector3(-u, -v, -1f),
    });

    private static byte[] BuildDds(int size)
    {
        int mipCount = (int)Math.Log2(size) + 1;
        using var stream = new MemoryStream();
        using var w = new BinaryWriter(stream);

        WriteHeader(w, size, mipCount);

        for (int face = 0; face < 6; face++)
        {
            // Supersample the top level, then box-filter down the chain.
            var level = new Vector3[size * size];
            const int ss = 3;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var sum = Vector3.Zero;
                    for (int sy = 0; sy < ss; sy++)
                    for (int sx = 0; sx < ss; sx++)
                    {
                        float u = 2f * (x + (sx + 0.5f) / ss) / size - 1f;
                        float v = 2f * (y + (sy + 0.5f) / ss) / size - 1f;
                        sum += Radiance(FaceDirection(face, u, v));
                    }
                    level[y * size + x] = sum / (ss * ss);
                }
            }

            int levelSize = size;
            for (int mip = 0; mip < mipCount; mip++)
            {
                foreach (var c in level)
                {
                    w.Write(ToByte(c.X));
                    w.Write(ToByte(c.Y));
                    w.Write(ToByte(c.Z));
                    w.Write((byte)255);
                }
                if (levelSize == 1) break;
                level = Downsample(level, levelSize);
                levelSize /= 2;
            }
        }

        w.Flush();
        return stream.ToArray();
    }

    private static Vector3[] Downsample(Vector3[] source, int size)
    {
        int half = size / 2;
        var result = new Vector3[half * half];
        for (int y = 0; y < half; y++)
        for (int x = 0; x < half; x++)
        {
            int i = 2 * y * size + 2 * x;
            result[y * half + x] = (source[i] + source[i + 1] + source[i + size] + source[i + size + 1]) * 0.25f;
        }
        return result;
    }

    private static byte ToByte(float linear)
    {
        // Gentle tone-map so the soft boxes keep a little gradient rather than clipping flat.
        float mapped = linear / (1f + 0.15f * linear);
        return (byte)Math.Clamp((int)MathF.Round(mapped * 255f), 0, 255);
    }

    private static void WriteHeader(BinaryWriter w, int size, int mipCount)
    {
        const uint ddsdCaps = 0x1, ddsdHeight = 0x2, ddsdWidth = 0x4, ddsdPitch = 0x8, ddsdPixelFormat = 0x1000, ddsdMipMapCount = 0x20000;
        const uint ddpfAlphaPixels = 0x1, ddpfRgb = 0x40;
        const uint capsComplex = 0x8, capsTexture = 0x1000, capsMipMap = 0x400000;
        const uint caps2CubeMapAllFaces = 0x200 | 0x400 | 0x800 | 0x1000 | 0x2000 | 0x4000 | 0x8000;

        w.Write(0x20534444u); // "DDS "
        w.Write(124u);
        w.Write(ddsdCaps | ddsdHeight | ddsdWidth | ddsdPitch | ddsdPixelFormat | ddsdMipMapCount);
        w.Write((uint)size);
        w.Write((uint)size);
        w.Write((uint)(size * 4));
        w.Write(0u); // depth
        w.Write((uint)mipCount);
        for (int i = 0; i < 11; i++) w.Write(0u);

        // Pixel format: 32-bit RGBA (R8G8B8A8_UNORM).
        w.Write(32u);
        w.Write(ddpfRgb | ddpfAlphaPixels);
        w.Write(0u);
        w.Write(32u);
        w.Write(0x000000FFu);
        w.Write(0x0000FF00u);
        w.Write(0x00FF0000u);
        w.Write(0xFF000000u);

        w.Write(capsComplex | capsTexture | capsMipMap);
        w.Write(caps2CubeMapAllFaces);
        w.Write(0u);
        w.Write(0u);
        w.Write(0u);
    }
}
