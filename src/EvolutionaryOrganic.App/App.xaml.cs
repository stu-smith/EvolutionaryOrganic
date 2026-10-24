using System.IO;
using System.Windows;
using System.Windows.Threading;
using EvolutionaryOrganic.App.Rendering;
using EvolutionaryOrganic.App.Services;
using HelixToolkit.SharpDX;

namespace EvolutionaryOrganic.App;

public partial class App : Application
{
    private DefaultEffectsManager? _effectsManager;

    public static new App Current => (App)Application.Current;

    public AppSettings Settings { get; private set; } = new();

    public ThemeService Theme { get; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandledException;

        Settings = AppSettings.Load();
        Theme.EffectiveThemeChanged += (_, _) => ViewerOptions.Instance.ApplyTheme(Theme.IsDark);
        ApplySettings(Settings);

        // One Direct3D device and shader cache is shared by all nine viewports.
        _effectsManager = new DefaultEffectsManager();
        Resources["EffectsManager"] = _effectsManager;

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _effectsManager?.Dispose();
        try
        {
            // The evolution pane edits settings live, so persist them on the way out.
            Settings.Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Failing to save preferences must not block shutdown.
        }
        base.OnExit(e);
    }

    /// <summary>Adopts new settings, applying theme and render options immediately.</summary>
    public void ApplySettings(AppSettings settings)
    {
        Settings = settings;
        Theme.Apply(settings.Theme);
        var viewer = ViewerOptions.Instance;
        viewer.EnableShadows = settings.EnableShadows;
        viewer.EnableAmbientOcclusion = settings.EnableAmbientOcclusion;
        viewer.EnableAntialiasing = settings.EnableAntialiasing;
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(MainWindow, e.Exception.Message, "Unexpected error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
