using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EvolutionaryOrganic.App.Services;

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

    public bool EnableShadows { get; set; } = true;

    public bool EnableAmbientOcclusion { get; set; } = true;

    public bool EnableAntialiasing { get; set; } = true;

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EvolutionaryOrganic", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions) ?? new AppSettings();
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
