using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EvolutionaryOrganic.App.ViewModels;
using EvolutionaryOrganic.App.Views;
using EvolutionaryOrganic.Core.Serialization;
using Microsoft.Win32;

namespace EvolutionaryOrganic.App;

public partial class MainWindow : Window
{
    private const string GenomeFilter = "Genome files (*.genome.json)|*.genome.json|JSON files (*.json)|*.json|All files (*.*)|*.*";

    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel(() => App.Current.Settings);
        DataContext = _viewModel;
        Loaded += (_, _) => _viewModel.Start();
    }

    private void OnImportGenome(object sender, ExecutedRoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Import genome", Filter = GenomeFilter };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            _viewModel.SetCurrent(GenomeSerializer.Load(dialog.FileName));
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
            Title = "Export genome",
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
