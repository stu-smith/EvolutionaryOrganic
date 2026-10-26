using System.Numerics;
using System.Text.Json.Serialization;

namespace Morphogen.Core.Genetics;

/// <summary>
/// Repeats its child around the local Y axis. Each copy is pushed out by <see cref="Radius"/>,
/// leaned by <see cref="Tilt"/> (rotation about the tangent), then rotated into place around the ring.
/// </summary>
public sealed class RadialRepeatNode : RepeatNode
{
    /// <summary>Distance of each copy from the axis.</summary>
    public float Radius { get; set; } = 1f;

    /// <summary>Angle covered by the ring, in degrees. 360 gives a closed ring.</summary>
    public float Sweep { get; set; } = 360f;

    /// <summary>Lean of each copy away from (positive) or towards the axis, in degrees.</summary>
    public float Tilt { get; set; }

    [JsonIgnore]
    public override string DisplayName => $"Radial repeat ×{Count}";

    [JsonIgnore]
    public float AngleStep
    {
        get
        {
            bool closed = MathF.Abs(Sweep) >= 359.999f;
            return closed || Count <= 1 ? Sweep / Count : Sweep / (Count - 1);
        }
    }

    public override IEnumerable<(Matrix4x4 Matrix, float Scale)> Iterations()
    {
        const float toRadians = MathF.PI / 180f;
        var placement = Matrix4x4.CreateRotationZ(-Tilt * toRadians) * Matrix4x4.CreateTranslation(Radius, 0f, 0f);
        for (int i = 0; i < Count; i++)
        {
            yield return (placement * Matrix4x4.CreateRotationY(i * AngleStep * toRadians), 1f);
        }
    }

    public override GenomeNode DeepClone() =>
        CopyRepeatTo(new RadialRepeatNode { Radius = Radius, Sweep = Sweep, Tilt = Tilt });

    public override IEnumerable<GeneProperty> DescribeProperties()
    {
        yield return new("Radius", Radius.ToString("0.###"));
        yield return new("Sweep", $"{Sweep:0.#}°");
        if (Tilt != 0f) yield return new("Tilt", $"{Tilt:0.#}°");
        foreach (var p in base.DescribeProperties()) yield return p;
    }
}
