using System.Windows;
using EvolutionaryOrganic.App.Services;

namespace EvolutionaryOrganic.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        Settings = settings;

        SystemTheme.IsChecked = settings.Theme == AppTheme.System;
        LightTheme.IsChecked = settings.Theme == AppTheme.Light;
        DarkTheme.IsChecked = settings.Theme == AppTheme.Dark;
        StrengthSlider.Value = settings.MutationStrength;
        StructuralSlider.Value = settings.StructuralMutationRate;
        MaxSpheresSlider.Value = settings.MaxSpheres;
        ShadowsCheck.IsChecked = settings.EnableShadows;
        AmbientOcclusionCheck.IsChecked = settings.EnableAmbientOcclusion;
        AntialiasingCheck.IsChecked = settings.EnableAntialiasing;
    }

    /// <summary>The edited settings; only meaningful when the dialog returns true.</summary>
    public AppSettings Settings { get; }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        Settings.Theme = LightTheme.IsChecked == true ? AppTheme.Light
                       : DarkTheme.IsChecked == true ? AppTheme.Dark
                       : AppTheme.System;
        Settings.MutationStrength = StrengthSlider.Value;
        Settings.StructuralMutationRate = StructuralSlider.Value;
        Settings.MaxSpheres = (int)MaxSpheresSlider.Value;
        Settings.EnableShadows = ShadowsCheck.IsChecked == true;
        Settings.EnableAmbientOcclusion = AmbientOcclusionCheck.IsChecked == true;
        Settings.EnableAntialiasing = AntialiasingCheck.IsChecked == true;
        DialogResult = true;
    }
}
