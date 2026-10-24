using System.Numerics;
using System.Text.Json.Serialization;

namespace EvolutionaryOrganic.Core.Genetics.Imaging;

public enum NoiseStyle
{
    /// <summary>Soft, cloudy fractal noise.</summary>
    Smooth,

    /// <summary>Folded noise with sharp creases in the dark areas, like marble or flames.</summary>
    Turbulent,

    /// <summary>Inverted folds: thin bright ridges, like veins or mountain ranges.</summary>
    Ridged,
}

/// <summary>Fractal gradient noise, summed over several octaves.</summary>
public sealed class NoiseImageNode : PatternImageNode
{
    public const int MaxOctaves = 6;

    private int _octaves = 3;

    public NoiseStyle Style { get; set; }

    /// <summary>Number of layers of finer detail.</summary>
    public int Octaves
    {
        get => _octaves;
        set => _octaves = Math.Clamp(value, 1, MaxOctaves);
    }

    [JsonIgnore]
    public override string DisplayName => $"{Style} noise";

    public override ImageNode DeepClone() => new NoiseImageNode { Frequency = Frequency, Seed = Seed, Style = Style, Octaves = Octaves };

    public override IEnumerable<GeneProperty> DescribeProperties()
    {
        yield return new("Frequency", Frequency.ToString("0.##"));
        yield return new("Octaves", Octaves.ToString());
        yield return new("Seed", Seed.ToString());
    }
}

public enum CellStyle
{
    /// <summary>Bright centres fading towards cell borders: scales, pebbles, spots.</summary>
    Spots,

    /// <summary>Dark cells with bright borders: cracks, honeycomb, giraffe patches.</summary>
    Edges,
}

/// <summary>Cellular (Worley) noise: randomly scattered cells.</summary>
public sealed class CellsImageNode : PatternImageNode
{
    public CellStyle Style { get; set; }

    [JsonIgnore]
    public override string DisplayName => $"Cells ({Style.ToString().ToLowerInvariant()})";

    public override ImageNode DeepClone() => new CellsImageNode { Frequency = Frequency, Seed = Seed, Style = Style };

    public override IEnumerable<GeneProperty> DescribeProperties()
    {
        yield return new("Frequency", Frequency.ToString("0.##"));
        yield return new("Seed", Seed.ToString());
    }
}

/// <summary>
/// Smooth bands around an axis. With a non-zero <see cref="Twist"/> the bands wind around the axis as spiral arms.
/// </summary>
public sealed class WavesImageNode : PatternImageNode
{
    public const int MaxTwist = 8;

    private int _twist;

    /// <summary>The bands are perpendicular to this direction.</summary>
    public Vector3 Axis { get; set; } = Vector3.UnitY;

    /// <summary>Number of spiral arms; 0 gives plain bands. Whole numbers keep the pattern seamless.</summary>
    public int Twist
    {
        get => _twist;
        set => _twist = Math.Clamp(value, -MaxTwist, MaxTwist);
    }

    /// <summary>Shift of the bands, in degrees.</summary>
    public float Phase { get; set; }

    [JsonIgnore]
    public override string DisplayName => Twist == 0 ? "Bands" : "Spiral";

    public override ImageNode DeepClone() =>
        new WavesImageNode { Frequency = Frequency, Seed = Seed, Axis = Axis, Twist = Twist, Phase = Phase };

    public override IEnumerable<GeneProperty> DescribeProperties()
    {
        yield return new("Frequency", Frequency.ToString("0.##"));
        yield return new("Axis", $"({Axis.X:0.##}, {Axis.Y:0.##}, {Axis.Z:0.##})");
        if (Twist != 0) yield return new("Arms", Twist.ToString());
        if (Phase != 0f) yield return new("Phase", $"{Phase:0}°");
    }
}
