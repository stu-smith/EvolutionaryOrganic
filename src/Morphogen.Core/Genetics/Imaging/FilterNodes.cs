using System.Numerics;
using System.Text.Json.Serialization;

namespace Morphogen.Core.Genetics.Imaging;

/// <summary>Distorts its input by pushing each sample point along a smooth random field.</summary>
public sealed class WarpImageNode : FilterImageNode
{
    /// <summary>How far sample points are pushed, in sphere radii.</summary>
    public float Amount { get; set; } = 0.3f;

    /// <summary>Density of the distortion field.</summary>
    public float Frequency { get; set; } = 2f;

    public int Seed { get; set; }

    [JsonIgnore]
    public override string DisplayName => "Warp";

    public override ImageNode DeepClone() =>
        new WarpImageNode { Input = Input.DeepClone(), Amount = Amount, Frequency = Frequency, Seed = Seed };

    public override IEnumerable<GeneProperty> DescribeProperties()
    {
        yield return new("Amount", Amount.ToString("0.##"));
        yield return new("Frequency", Frequency.ToString("0.##"));
        yield return new("Seed", Seed.ToString());
    }
}

/// <summary>Scales and rotates its input, which zooms and turns the whole pattern.</summary>
public sealed class TransformImageNode : FilterImageNode
{
    /// <summary>Greater than 1 makes the pattern finer.</summary>
    public float Scale { get; set; } = 1f;

    /// <summary>Euler angles in degrees about the X, Y and Z axes.</summary>
    public Vector3 Rotation { get; set; }

    [JsonIgnore]
    public override string DisplayName => "Transform";

    public override ImageNode DeepClone() => new TransformImageNode { Input = Input.DeepClone(), Scale = Scale, Rotation = Rotation };

    public override IEnumerable<GeneProperty> DescribeProperties()
    {
        if (Scale != 1f) yield return new("Scale", $"×{Scale:0.##}");
        if (Rotation != Vector3.Zero) yield return new("Rotate", $"({Rotation.X:0}°, {Rotation.Y:0}°, {Rotation.Z:0}°)");
    }
}

public enum ToneFunction
{
    /// <summary>Steepens the mid-tones by <see cref="ToneImageNode.Amount"/>, towards hard-edged black and white.</summary>
    Contrast,

    /// <summary>Swaps light and dark.</summary>
    Invert,

    /// <summary>Reduces the image to <see cref="ToneImageNode.Amount"/> flat levels.</summary>
    Posterize,

    /// <summary>Folds the image about its mid-tone, so mid-grey becomes bright lines.</summary>
    Ridge,

    /// <summary>Repeats the tonal range <see cref="ToneImageNode.Amount"/> times, giving contour lines.</summary>
    Contours,
}

/// <summary>Remaps the brightness of its input through a tone curve.</summary>
public sealed class ToneImageNode : FilterImageNode
{
    public ToneFunction Function { get; set; }

    /// <summary>The curve's parameter; its meaning depends on <see cref="Function"/>.</summary>
    public float Amount { get; set; } = 3f;

    [JsonIgnore]
    public override string DisplayName => Function.ToString();

    public override ImageNode DeepClone() => new ToneImageNode { Input = Input.DeepClone(), Function = Function, Amount = Amount };

    public override IEnumerable<GeneProperty> DescribeProperties()
    {
        switch (Function)
        {
            case ToneFunction.Contrast: yield return new("Gain", Amount.ToString("0.##")); break;
            case ToneFunction.Posterize: yield return new("Levels", ToneLevels.ToString()); break;
            case ToneFunction.Contours: yield return new("Repeats", Amount.ToString("0.##")); break;
        }
    }

    /// <summary>The number of levels <see cref="ToneFunction.Posterize"/> uses.</summary>
    [JsonIgnore]
    public int ToneLevels => Math.Clamp((int)MathF.Round(Amount), 2, 12);
}

public enum BlendMode
{
    Mix,
    Add,
    Multiply,
    Difference,
    Lighten,
    Darken,
}

/// <summary>Combines two images, applying <see cref="Mode"/> at strength <see cref="Weight"/>.</summary>
public sealed class BlendImageNode : ImageNode
{
    public ImageNode A { get; set; } = new NoiseImageNode();

    public ImageNode B { get; set; } = new CellsImageNode();

    public BlendMode Mode { get; set; }

    /// <summary>0 = only <see cref="A"/>, 1 = the full effect of the blend.</summary>
    public float Weight { get; set; } = 0.5f;

    [JsonIgnore]
    public override string DisplayName => $"Blend ({Mode.ToString().ToLowerInvariant()})";

    [JsonIgnore]
    public override IReadOnlyList<ImageNode> Inputs => [A, B];

    public override ImageNode DeepClone() => new BlendImageNode { A = A.DeepClone(), B = B.DeepClone(), Mode = Mode, Weight = Weight };

    public override bool ReplaceInput(ImageNode existing, ImageNode replacement)
    {
        if (ReferenceEquals(A, existing)) A = replacement;
        else if (ReferenceEquals(B, existing)) B = replacement;
        else return false;
        return true;
    }

    public override IEnumerable<GeneProperty> DescribeProperties()
    {
        yield return new("Weight", Weight.ToString("0.##"));
    }
}

/// <summary>Shows <see cref="A"/> where <see cref="Mask"/> is dark and <see cref="B"/> where it is light.</summary>
public sealed class MaskImageNode : ImageNode
{
    public ImageNode A { get; set; } = new NoiseImageNode();

    public ImageNode B { get; set; } = new CellsImageNode();

    public ImageNode Mask { get; set; } = new WavesImageNode();

    /// <summary>1 = soft cross-fade; higher values give crisper boundaries.</summary>
    public float Sharpness { get; set; } = 2f;

    [JsonIgnore]
    public override string DisplayName => "Mask";

    [JsonIgnore]
    public override IReadOnlyList<ImageNode> Inputs => [Mask, A, B];

    public override ImageNode DeepClone() =>
        new MaskImageNode { A = A.DeepClone(), B = B.DeepClone(), Mask = Mask.DeepClone(), Sharpness = Sharpness };

    public override bool ReplaceInput(ImageNode existing, ImageNode replacement)
    {
        if (ReferenceEquals(A, existing)) A = replacement;
        else if (ReferenceEquals(B, existing)) B = replacement;
        else if (ReferenceEquals(Mask, existing)) Mask = replacement;
        else return false;
        return true;
    }

    public override IEnumerable<GeneProperty> DescribeProperties()
    {
        yield return new("Sharpness", Sharpness.ToString("0.##"));
    }
}
