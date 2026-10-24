using System.Windows.Media;
using EvolutionaryOrganic.Core.Genetics;

namespace EvolutionaryOrganic.App.ViewModels;

/// <summary>A read-only view of one genome node for the sidebar tree.</summary>
public sealed class GenomeNodeViewModel
{
    public GenomeNodeViewModel(GenomeNode node)
    {
        Title = node.DisplayName;
        Glyph = node switch
        {
            SphereNode => string.Empty, // Shown as a colour swatch instead.
            GroupNode => "▣",
            LinearRepeatNode => "⇢",
            RadialRepeatNode => "✺",
            _ => "•",
        };
        Properties = node.DescribeProperties().ToList();
        Children = node.Children.Select(c => new GenomeNodeViewModel(c)).ToList();

        if (node is SphereNode sphere)
        {
            var rgb = sphere.Color.ToRgb();
            var brush = new SolidColorBrush(Color.FromRgb(ToByte(rgb.X), ToByte(rgb.Y), ToByte(rgb.Z)));
            brush.Freeze();
            Swatch = brush;
        }
    }

    public string Title { get; }

    public string Glyph { get; }

    /// <summary>The sphere's base colour, or null for non-leaf nodes.</summary>
    public Brush? Swatch { get; }

    public IReadOnlyList<GeneProperty> Properties { get; }

    public IReadOnlyList<GenomeNodeViewModel> Children { get; }

    private static byte ToByte(float value) => (byte)Math.Clamp((int)MathF.Round(value * 255f), 0, 255);
}
