using System.Windows.Media;
using System.Windows.Media.Imaging;
using EvolutionaryOrganic.Core.Development;
using EvolutionaryOrganic.Core.Genetics;
using EvolutionaryOrganic.Core.Genetics.Imaging;

namespace EvolutionaryOrganic.App.ViewModels;

/// <summary>A read-only view of one genome node, texture, bump map or image operation for the sidebar tree.</summary>
public sealed class GenomeNodeViewModel
{
    private const int ThumbnailWidth = 64;

    private GenomeNodeViewModel(
        string title, string glyph, Brush? swatch, IEnumerable<GeneProperty> properties, IEnumerable<GenomeNodeViewModel> children)
    {
        Title = title;
        Glyph = glyph;
        Swatch = swatch;
        Properties = properties.ToList();
        Children = children.ToList();
    }

    public string Title { get; }

    public string Glyph { get; }

    /// <summary>The sphere's colour or texture, or an image's thumbnail; null for other nodes.</summary>
    public Brush? Swatch { get; }

    public IReadOnlyList<GeneProperty> Properties { get; }

    public IReadOnlyList<GenomeNodeViewModel> Children { get; }

    /// <summary>The tree's roots: the morphology tree, then each texture and bump map.</summary>
    public static IReadOnlyList<GenomeNodeViewModel> ForGenome(Genome genome, Morphology? morphology)
    {
        var textures = Thumbnails(genome.Textures.Count, morphology?.Textures);
        var bumpMaps = Thumbnails(genome.BumpMaps.Count, morphology?.BumpMaps);

        var roots = new List<GenomeNodeViewModel> { FromNode(genome.Root, textures) };
        roots.AddRange(genome.Textures.Select((gene, i) => FromGene(gene, i, textures[i])));
        roots.AddRange(genome.BumpMaps.Select((gene, i) => FromGene(gene, i, bumpMaps[i])));
        return roots;
    }

    private static GenomeNodeViewModel FromNode(GenomeNode node, IReadOnlyList<Brush?> textures)
    {
        string glyph = node switch
        {
            SphereNode => string.Empty, // Shown as a colour or texture swatch instead.
            GroupNode => "▣",
            LinearRepeatNode => "⇢",
            RadialRepeatNode => "✺",
            _ => "•",
        };

        Brush? swatch = null;
        if (node is SphereNode sphere)
        {
            swatch = sphere.Texture is int t && t >= 0 && t < textures.Count && textures[t] is { } texture
                ? texture
                : ColorBrush(sphere.Color);
        }

        return new GenomeNodeViewModel(
            node.DisplayName, glyph, swatch, node.DescribeProperties(), node.Children.Select(c => FromNode(c, textures)));
    }

    private static GenomeNodeViewModel FromGene(ImageGene gene, int index, Brush? thumbnail) => new(
        $"{gene.Kind} #{index + 1}",
        thumbnail is null ? "▦" : string.Empty,
        thumbnail,
        gene.DescribeProperties(),
        [FromImage(gene.Pattern)]);

    private static GenomeNodeViewModel FromImage(ImageNode node)
    {
        string glyph = node switch
        {
            NoiseImageNode => "≈",
            CellsImageNode => "⬡",
            WavesImageNode => "≋",
            WarpImageNode => "∿",
            TransformImageNode => "⤢",
            ToneImageNode => "◐",
            BlendImageNode => "⊕",
            MaskImageNode => "◧",
            _ => "•",
        };
        return new GenomeNodeViewModel(node.DisplayName, glyph, null, node.DescribeProperties(), node.Inputs.Select(FromImage));
    }

    private static Brush?[] Thumbnails(int count, IReadOnlyList<BakedImage?>? images) =>
        Enumerable.Range(0, count).Select(i => images is not null && i < images.Count && images[i] is { } image ? Thumbnail(image) : null).ToArray();

    /// <summary>A small, point-sampled copy of a baked image, showing the side of the sphere that faces the viewer.</summary>
    private static Brush Thumbnail(BakedImage image)
    {
        int width = Math.Min(ThumbnailWidth, image.Width), height = Math.Max(1, width / 2);
        var pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int source = ((y * image.Height / height) * image.Width + x * image.Width / width) * 4;
            int target = (y * width + x) * 4;
            // RGBA to WPF's BGRA.
            pixels[target] = image.Pixels[source + 2];
            pixels[target + 1] = image.Pixels[source + 1];
            pixels[target + 2] = image.Pixels[source];
            pixels[target + 3] = 255;
        }

        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        bitmap.Freeze();
        var brush = new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
        brush.Freeze();
        return brush;
    }

    private static SolidColorBrush ColorBrush(HsvColor color)
    {
        var rgb = color.ToRgb();
        var brush = new SolidColorBrush(Color.FromRgb(ToByte(rgb.X), ToByte(rgb.Y), ToByte(rgb.Z)));
        brush.Freeze();
        return brush;
    }

    private static byte ToByte(float value) => (byte)Math.Clamp((int)MathF.Round(value * 255f), 0, 255);
}
