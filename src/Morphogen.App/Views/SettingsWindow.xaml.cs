using System.Windows;
using Morphogen.App.Services;

namespace Morphogen.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        Settings = settings;

        SystemTheme.IsChecked = settings.Theme == AppTheme.System;
        LightTheme.IsChecked = settings.Theme == AppTheme.Light;
        DarkTheme.IsChecked = settings.Theme == AppTheme.Dark;
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
        Settings.EnableShadows = ShadowsCheck.IsChecked == true;
        Settings.EnableAmbientOcclusion = AmbientOcclusionCheck.IsChecked == true;
        Settings.EnableAntialiasing = AntialiasingCheck.IsChecked == true;
        DialogResult = true;
    }
}
