using System.IO;
using System.Numerics;
using Morphogen.Core.Development;
using HelixToolkit.SharpDX;

namespace Morphogen.App.Rendering;

/// <summary>Uploads images baked from texture and bump genes as GPU textures.</summary>
public static class ImageTextures
{
    /// <summary>
    /// Creates a texture with a full, box-filtered mip chain, so that small, distant spheres sample a
    /// smooth average of their pattern rather than shimmering.
    /// </summary>
    public static TextureModel Create(BakedImage image)
    {
        int mipCount = (int)Math.Log2(Math.Max(image.Width, image.Height)) + 1;
        using var stream = new MemoryStream();
        using var w = new BinaryWriter(stream);
        DdsWriter.WriteHeader(w, image.Width, image.Height, mipCount, cubeMap: false);

        var level = image.Pixels;
        int width = image.Width, height = image.Height;
        for (int mip = 0; mip < mipCount; mip++)
        {
            w.Write(level);
            if (mip == mipCount - 1) break;
            (level, width, height) = Downsample(level, width, height);
        }

        w.Flush();
        return new TextureModel(new MemoryStream(stream.ToArray(), writable: false), autoCloseStream: true);
    }

    private static (byte[] Pixels, int Width, int Height) Downsample(byte[] source, int width, int height)
    {
        int halfWidth = Math.Max(1, width / 2), halfHeight = Math.Max(1, height / 2);
        var result = new byte[halfWidth * halfHeight * 4];
        for (int y = 0; y < halfHeight; y++)
        for (int x = 0; x < halfWidth; x++)
        {
            int x0 = Math.Min(2 * x, width - 1), x1 = Math.Min(2 * x + 1, width - 1);
            int y0 = Math.Min(2 * y, height - 1), y1 = Math.Min(2 * y + 1, height - 1);
            var sum = Pixel(source, width, x0, y0) + Pixel(source, width, x1, y0) + Pixel(source, width, x0, y1) + Pixel(source, width, x1, y1);
            var average = sum * 0.25f;
            int i = (y * halfWidth + x) * 4;
            result[i] = (byte)MathF.Round(average.X);
            result[i + 1] = (byte)MathF.Round(average.Y);
            result[i + 2] = (byte)MathF.Round(average.Z);
            result[i + 3] = (byte)MathF.Round(average.W);
        }
        return (result, halfWidth, halfHeight);
    }

    private static Vector4 Pixel(byte[] pixels, int width, int x, int y)
    {
        int i = (y * width + x) * 4;
        return new Vector4(pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]);
    }
}
