using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Morphogen.App.Services;

public enum AppTheme
{
    System,
    Light,
    Dark,
}

/// <summary>User preferences, persisted as JSON in the roaming application data folder.</summary>
public sealed class AppSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public AppTheme Theme { get; set; } = AppTheme.System;

    /// <summary>0 = subtle, 1 = wild.</summary>
    public double MutationStrength { get; set; } = 0.5;

    /// <summary>Chance that a mutation alters the genome's structure rather than its parameters.</summary>
    public double StructuralMutationRate { get; set; } = 0.2;

    /// <summary>Upper limit on spheres per creation, which keeps the nine views interactive.</summary>
    public int MaxSpheres { get; set; } = 3000;

    /// <summary>Upper limit on procedural textures per genome.</summary>
    public int MaxTextures { get; set; } = 3;

    /// <summary>Upper limit on procedural bump maps per genome.</summary>
    public int MaxBumpMaps { get; set; } = 3;

    public bool EnableShadows { get; set; } = true;

    public bool EnableAmbientOcclusion { get; set; } = true;

    public bool EnableAntialiasing { get; set; } = true;

    /// <summary>Where the main window was when the app last closed, or null to use the default placement.</summary>
    public WindowPlacement? MainWindowPlacement { get; set; }

    /// <summary>The saved project that was open when the app last closed, reopened on startup.</summary>
    public string? LastProjectPath { get; set; }

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Morphogen", "settings.json");

    /// <summary>Where settings lived before the app was renamed to Morphogen; read only if there are no new settings.</summary>
    private static string LegacyFilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EvolutionaryOrganic", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            var path = File.Exists(FilePath) ? FilePath : LegacyFilePath;
            if (File.Exists(path))
            {
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonOptions) ?? new AppSettings();
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Unreadable settings must never stop the app starting; fall back to defaults.
        }
        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
    }

    public AppSettings Clone() => (AppSettings)MemberwiseClone();
}

/// <summary>A window's restored (non-maximised) bounds, and whether it was maximised.</summary>
public sealed record WindowPlacement(double Left, double Top, double Width, double Height, bool IsMaximized);
