using System.Numerics;

namespace Morphogen.Core.Development;

/// <summary>
/// Deterministic 3D noise functions. Lattice values come from an integer hash rather than a permutation
/// table, so any seed gives an independent pattern without allocating anything.
/// </summary>
public static class ProceduralNoise
{
    private static readonly Vector3[] Gradients =
    [
        new(1, 1, 0), new(-1, 1, 0), new(1, -1, 0), new(-1, -1, 0),
        new(1, 0, 1), new(-1, 0, 1), new(1, 0, -1), new(-1, 0, -1),
        new(0, 1, 1), new(0, -1, 1), new(0, 1, -1), new(0, -1, -1),
        new(1, 1, 0), new(-1, 1, 0), new(0, -1, 1), new(0, -1, -1),
    ];

    /// <summary>Gradient (Perlin) noise, roughly in [-1, 1].</summary>
    public static float Gradient(Vector3 p, int seed)
    {
        int x0 = (int)MathF.Floor(p.X), y0 = (int)MathF.Floor(p.Y), z0 = (int)MathF.Floor(p.Z);
        float fx = p.X - x0, fy = p.Y - y0, fz = p.Z - z0;
        float u = Fade(fx), v = Fade(fy), w = Fade(fz);

        float Corner(int dx, int dy, int dz) =>
            Vector3.Dot(Gradients[Hash(x0 + dx, y0 + dy, z0 + dz, seed) & 15], new Vector3(fx - dx, fy - dy, fz - dz));

        float x00 = Lerp(Corner(0, 0, 0), Corner(1, 0, 0), u);
        float x10 = Lerp(Corner(0, 1, 0), Corner(1, 1, 0), u);
        float x01 = Lerp(Corner(0, 0, 1), Corner(1, 0, 1), u);
        float x11 = Lerp(Corner(0, 1, 1), Corner(1, 1, 1), u);
        return Lerp(Lerp(x00, x10, v), Lerp(x01, x11, v), w);
    }

    /// <summary>
    /// Fractal sum of <paramref name="octaves"/> layers of gradient noise, each twice as fine and half as strong
    /// as the last, normalised to roughly [-1, 1].
    /// </summary>
    public static float Fractal(Vector3 p, int seed, int octaves, Func<float, float>? shape = null)
    {
        float sum = 0f, amplitude = 1f, total = 0f;
        for (int i = 0; i < octaves; i++)
        {
            float n = Gradient(p, seed + i * 7919);
            sum += (shape is null ? n : shape(n)) * amplitude;
            total += amplitude;
            amplitude *= 0.5f;
            p = p * 2.03f + new Vector3(17.1f, 3.7f, 9.3f);
        }
        return sum / total;
    }

    /// <summary>A smooth random vector field, each component roughly in [-1, 1].</summary>
    public static Vector3 Field(Vector3 p, int seed) => new(
        Gradient(p, seed),
        Gradient(p + new Vector3(31.4f, 0f, 0f), seed + 1),
        Gradient(p + new Vector3(0f, 47.2f, 0f), seed + 2));

    /// <summary>
    /// Cellular (Worley) noise: distances from <paramref name="p"/> to the nearest and second-nearest of a set
    /// of random feature points, one per unit cell.
    /// </summary>
    public static (float F1, float F2) Cellular(Vector3 p, int seed)
    {
        int cx = (int)MathF.Floor(p.X), cy = (int)MathF.Floor(p.Y), cz = (int)MathF.Floor(p.Z);
        float f1 = float.MaxValue, f2 = float.MaxValue;
        for (int dz = -1; dz <= 1; dz++)
        for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
        {
            int x = cx + dx, y = cy + dy, z = cz + dz;
            int h = Hash(x, y, z, seed);
            var feature = new Vector3(
                x + (h & 0x3FF) / 1023f,
                y + ((h >> 10) & 0x3FF) / 1023f,
                z + ((h >> 20) & 0x3FF) / 1023f);
            float d = Vector3.DistanceSquared(p, feature);
            if (d < f1)
            {
                f2 = f1;
                f1 = d;
            }
            else if (d < f2)
            {
                f2 = d;
            }
        }
        return (MathF.Sqrt(f1), MathF.Sqrt(f2));
    }

    private static int Hash(int x, int y, int z, int seed)
    {
        unchecked
        {
            uint h = (uint)seed * 0x9E3779B1u;
            h ^= (uint)x * 0x85EBCA77u;
            h = (h << 13 | h >> 19) * 0xC2B2AE3Du;
            h ^= (uint)y * 0x27D4EB2Fu;
            h = (h << 13 | h >> 19) * 0x165667B1u;
            h ^= (uint)z * 0x9E3779B9u;
            // MurmurHash3 finaliser.
            h ^= h >> 16;
            h *= 0x85EBCA6Bu;
            h ^= h >> 13;
            h *= 0xC2B2AE35u;
            h ^= h >> 16;
            return (int)(h & 0x7FFFFFFF);
        }
    }

    private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
