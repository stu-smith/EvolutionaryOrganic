using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Morphogen.App.Services;
using Morphogen.App.ViewModels;
using Morphogen.App.Views;
using Morphogen.Core.Serialization;
using Microsoft.Win32;

namespace Morphogen.App;

public partial class MainWindow : Window
{
    private const string GenomeFilter = "Genome files (*.genome.json)|*.genome.json|JSON files (*.json)|*.json|All files (*.*)|*.*";

    private const string ProjectFilter = "Projects (*.morphogen.json)|*.morphogen.json;*.organic.json|JSON files (*.json)|*.json|All files (*.*)|*.*";

    public static readonly RoutedCommand NewProjectCommand = new();

    public static readonly RoutedCommand ImportGenomeCommand = new();

    public static readonly RoutedCommand ExportGenomeCommand = new();

    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel(() => App.Current.Settings);
        DataContext = _viewModel;
        RestorePlacement(App.Current.Settings.MainWindowPlacement);
        Loaded += (_, _) => Start();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!ConfirmDiscardChanges()) e.Cancel = true;
        base.OnClosing(e);
        if (e.Cancel) return;

        // App.OnExit persists the settings, so record where we were for next time.
        var settings = App.Current.Settings;
        var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        settings.MainWindowPlacement = new WindowPlacement(bounds.Left, bounds.Top, bounds.Width, bounds.Height,
            WindowState == WindowState.Maximized);
        settings.LastProjectPath = _viewModel.ProjectPath;
    }

    /// <summary>Puts the window back where it was last time, provided that is still on screen.</summary>
    private void RestorePlacement(WindowPlacement? placement)
    {
        if (placement is null || placement.Width <= 0 || placement.Height <= 0) return;

        var bounds = new Rect(placement.Left, placement.Top, Math.Max(placement.Width, MinWidth), Math.Max(placement.Height, MinHeight));
        var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        var visible = Rect.Intersect(bounds, screen);
        // A monitor may have been unplugged since; keep the default placement rather than open off screen.
        if (visible.IsEmpty || visible.Width < 100 || visible.Height < 50) return;

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = bounds.Left;
        Top = bounds.Top;
        Width = bounds.Width;
        Height = bounds.Height;
        if (placement.IsMaximized) WindowState = WindowState.Maximized;
    }

    /// <summary>Reopens the project that was open last time, or starts afresh if there wasn't one.</summary>
    private void Start()
    {
        var path = App.Current.Settings.LastProjectPath;
        if (path is not null && File.Exists(path) && TryOpenProject(path)) return;
        _viewModel.Start();
    }

    private void OnNewProject(object sender, ExecutedRoutedEventArgs e)
    {
        if (ConfirmDiscardChanges()) _viewModel.NewProject();
    }

    private void OnOpenProject(object sender, ExecutedRoutedEventArgs e)
    {
        if (!ConfirmDiscardChanges()) return;

        var dialog = new OpenFileDialog { Title = "Open project", Filter = ProjectFilter };
        if (dialog.ShowDialog(this) != true) return;
        TryOpenProject(dialog.FileName);
    }

    /// <summary>Opens the project at <paramref name="path"/>, explaining why if it can't. Returns false if it was not opened.</summary>
    private bool TryOpenProject(string path)
    {
        try
        {
            _viewModel.OpenProject(GenomeSerializer.LoadProject(path), path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException or InvalidOperationException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"Could not open the project.\n\n{ex.Message}", "Open project", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    private void OnSaveProject(object sender, ExecutedRoutedEventArgs e) => SaveProject(_viewModel.ProjectPath);

    private void OnSaveProjectAs(object sender, ExecutedRoutedEventArgs e) => SaveProject(null);

    /// <summary>Saves the project to <paramref name="path"/>, or asks where to save it. Returns false if it was not saved.</summary>
    private bool SaveProject(string? path)
    {
        if (path is null)
        {
            var dialog = new SaveFileDialog
            {
                Title = "Save project",
                Filter = ProjectFilter,
                FileName = $"{_viewModel.ProjectName}{GenomeSerializer.ProjectFileExtension}",
            };
            if (dialog.ShowDialog(this) != true) return false;
            path = dialog.FileName;
        }

        try
        {
            GenomeSerializer.SaveProject(_viewModel.ToProject(), path);
            _viewModel.MarkSaved(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"Could not save the project.\n\n{ex.Message}", "Save project", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    /// <summary>Offers to save unsaved changes to the project. Returns false if the user cancelled.</summary>
    private bool ConfirmDiscardChanges()
    {
        if (!_viewModel.IsDirty) return true;

        var answer = MessageBox.Show(this, $"Save changes to {_viewModel.ProjectName}?", "Morphogen",
            MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        return answer switch
        {
            MessageBoxResult.Yes => SaveProject(_viewModel.ProjectPath),
            MessageBoxResult.No => true,
            _ => false,
        };
    }

    private void OnImportGenome(object sender, ExecutedRoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Import genome into project", Filter = GenomeFilter, Multiselect = true };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            var genomes = dialog.FileNames.Select(GenomeSerializer.Load).ToList();
            foreach (var genome in genomes) _viewModel.AddToProject(genome);
            _viewModel.SetCurrent(genomes[^1]);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"Could not import the genome.\n\n{ex.Message}", "Import genome", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnExportGenome(object sender, ExecutedRoutedEventArgs e)
    {
        var genome = _viewModel.CurrentGenome;
        if (genome is null) return;

        var dialog = new SaveFileDialog
        {
            Title = "Export current genome",
            Filter = GenomeFilter,
            FileName = $"creation-gen{genome.Generation}{GenomeSerializer.FileExtension}",
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            GenomeSerializer.Save(genome, dialog.FileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"Could not export the genome.\n\n{ex.Message}", "Export genome", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnExportImage(object sender, ExecutedRoutedEventArgs e)
    {
        var view = FindCurrentCreationView();
        var genome = _viewModel.CurrentGenome;
        if (view is null || genome is null) return;

        var options = new ExportImageWindow { Owner = this };
        if (options.ShowDialog() != true) return;

        var dialog = new SaveFileDialog
        {
            Title = "Export image",
            Filter = "PNG image (*.png)|*.png|JPEG image (*.jpg)|*.jpg|TIFF image (*.tif)|*.tif",
            FileName = $"creation-gen{genome.Generation}.png",
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            var bitmap = view.RenderImage(options.ImageWidth, options.ImageHeight);
            BitmapEncoder encoder = Path.GetExtension(dialog.FileName).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => new JpegBitmapEncoder { QualityLevel = 95 },
                ".tif" or ".tiff" => new TiffBitmapEncoder { Compression = TiffCompressOption.Lzw },
                _ => new PngBitmapEncoder(),
            };
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(dialog.FileName);
            encoder.Save(stream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or SharpDX.SharpDXException)
        {
            MessageBox.Show(this, $"Could not export the image.\n\n{ex.Message}", "Export image", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private void OnSettings(object sender, RoutedEventArgs e)
    {
        var window = new SettingsWindow(App.Current.Settings.Clone()) { Owner = this };
        if (window.ShowDialog() != true) return;

        App.Current.ApplySettings(window.Settings);
        try
        {
            window.Settings.Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"Settings were applied but could not be saved.\n\n{ex.Message}", "Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnFullScreen(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: CreationSlotViewModel slot } || slot.Morphology is null) return;
        new FullScreenWindow(slot) { Owner = this }.ShowDialog();
    }

    private void OnExit(object sender, RoutedEventArgs e) => Close();

    private CreationView? FindCurrentCreationView() =>
        FindDescendants<CreationView>(this).FirstOrDefault(v => ReferenceEquals(v.DataContext, _viewModel.CurrentSlot));

    private static IEnumerable<T> FindDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var descendant in FindDescendants<T>(child)) yield return descendant;
        }
    }
}
