using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using HelixToolkit.SharpDX;

namespace EvolutionaryOrganic.App.Rendering;

/// <summary>Render options shared by every creation view, updated live from settings and theme.</summary>
public sealed partial class ViewerOptions : ObservableObject
{
    public static ViewerOptions Instance { get; } = new();

    [ObservableProperty]
    private Color _backgroundColor = Color.FromRgb(0x14, 0x15, 0x19);

    [ObservableProperty]
    private bool _enableShadows = true;

    [ObservableProperty]
    private bool _enableAmbientOcclusion = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MsaaLevel))]
    private bool _enableAntialiasing = true;

    public MSAALevel MsaaLevel => EnableAntialiasing ? MSAALevel.Four : MSAALevel.Disable;

    public void ApplyTheme(bool isDark)
    {
        BackgroundColor = isDark ? Color.FromRgb(0x14, 0x15, 0x19) : Color.FromRgb(0xE6, 0xE7, 0xEB);
    }
}
