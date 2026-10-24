using System.IO;

namespace EvolutionaryOrganic.App.Rendering;

/// <summary>Writes the header of an uncompressed 32-bit RGBA (R8G8B8A8_UNORM) DDS file, for textures built in memory.</summary>
public static class DdsWriter
{
    /// <summary>
    /// Writes the header. The caller then writes the pixels: for each face (six for a cube map, in D3D face
    /// order), every mip level from largest to smallest, rows top to bottom, 4 bytes per pixel.
    /// </summary>
    public static void WriteHeader(BinaryWriter w, int width, int height, int mipCount, bool cubeMap)
    {
        const uint ddsdCaps = 0x1, ddsdHeight = 0x2, ddsdWidth = 0x4, ddsdPitch = 0x8, ddsdPixelFormat = 0x1000, ddsdMipMapCount = 0x20000;
        const uint ddpfAlphaPixels = 0x1, ddpfRgb = 0x40;
        const uint capsComplex = 0x8, capsTexture = 0x1000, capsMipMap = 0x400000;
        const uint caps2CubeMapAllFaces = 0x200 | 0x400 | 0x800 | 0x1000 | 0x2000 | 0x4000 | 0x8000;

        w.Write(0x20534444u); // "DDS "
        w.Write(124u);
        w.Write(ddsdCaps | ddsdHeight | ddsdWidth | ddsdPitch | ddsdPixelFormat | ddsdMipMapCount);
        w.Write((uint)height);
        w.Write((uint)width);
        w.Write((uint)(width * 4));
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
        w.Write(cubeMap ? caps2CubeMapAllFaces : 0u);
        w.Write(0u);
        w.Write(0u);
        w.Write(0u);
    }
}
