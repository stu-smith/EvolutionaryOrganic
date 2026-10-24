using System.Windows;
using System.Windows.Controls;

namespace EvolutionaryOrganic.App.Views;

public partial class ExportImageWindow : Window
{
    /// <summary>Direct3D 11 limits textures to 16384 pixels per side.</summary>
    public const int MaxDimension = 16384;

    private static readonly (string Name, int Width, int Height)[] Presets =
    [
        ("Square 2048 × 2048", 2048, 2048),
        ("Square 4096 × 4096", 4096, 4096),
        ("Full HD 1920 × 1080", 1920, 1080),
        ("4K UHD 3840 × 2160", 3840, 2160),
        ("8K UHD 7680 × 4320", 7680, 4320),
        ("Print A4 portrait, 300 dpi (2480 × 3508)", 2480, 3508),
        ("Custom", 0, 0),
    ];

    private static int _lastPreset = 1;

    public ExportImageWindow()
    {
        InitializeComponent();
        PresetCombo.ItemsSource = Presets.Select(p => p.Name).ToList();
        PresetCombo.SelectedIndex = _lastPreset;
    }

    public int ImageWidth { get; private set; }

    public int ImageHeight { get; private set; }

    private void OnPresetChanged(object sender, SelectionChangedEventArgs e)
    {
        var preset = Presets[PresetCombo.SelectedIndex];
        bool custom = preset.Width == 0;
        WidthBox.IsEnabled = HeightBox.IsEnabled = custom;
        if (!custom)
        {
            WidthBox.Text = preset.Width.ToString();
            HeightBox.Text = preset.Height.ToString();
        }
    }

    private void OnExport(object sender, RoutedEventArgs e)
    {
        if (!TryParseDimension(WidthBox.Text, out int width) || !TryParseDimension(HeightBox.Text, out int height))
        {
            ErrorText.Text = $"Width and height must be whole numbers between 16 and {MaxDimension}.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        ImageWidth = width;
        ImageHeight = height;
        _lastPreset = PresetCombo.SelectedIndex;
        DialogResult = true;
    }

    private static bool TryParseDimension(string text, out int value) =>
        int.TryParse(text, out value) && value is >= 16 and <= MaxDimension;
}
