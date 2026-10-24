using System.Windows;
using Microsoft.Win32;

namespace EvolutionaryOrganic.App.Services;

/// <summary>Applies the WPF Fluent theme and tracks whether the effective theme is dark.</summary>
public sealed class ThemeService
{
    private AppTheme _theme = AppTheme.System;

    public ThemeService()
    {
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category == UserPreferenceCategory.General && _theme == AppTheme.System)
            {
                Application.Current?.Dispatcher.BeginInvoke(RaiseChanged);
            }
        };
    }

    /// <summary>Raised when the effective light/dark appearance may have changed.</summary>
    public event EventHandler? EffectiveThemeChanged;

    public bool IsDark => _theme switch
    {
        AppTheme.Dark => true,
        AppTheme.Light => false,
        _ => !SystemUsesLightTheme(),
    };

    public void Apply(AppTheme theme)
    {
        _theme = theme;
        Application.Current.ThemeMode = theme switch
        {
            AppTheme.Light => ThemeMode.Light,
            AppTheme.Dark => ThemeMode.Dark,
            _ => ThemeMode.System,
        };
        RaiseChanged();
    }

    private void RaiseChanged() => EffectiveThemeChanged?.Invoke(this, EventArgs.Empty);

    private static bool SystemUsesLightTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is not int value || value != 0;
    }
}
