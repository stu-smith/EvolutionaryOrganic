using System.Text.Json.Serialization;
namespace EvolutionaryOrganic.Core.Genetics;

/// <summary>
/// A transform applied to the visual properties of everything beneath a node.
/// Repeat nodes apply their modifier once per iteration, so iteration <c>i</c> receives the
/// modifier compounded <c>i</c> times. That gives gradients of colour and size along a horn,
/// or around a ring.
/// </summary>
public sealed record PropertyModifier
{
    public static PropertyModifier None { get; } = new();

    /// <summary>Degrees added to hue.</summary>
    public float HueShift { get; init; }

    /// <summary>Multiplier applied to saturation.</summary>
    public float SaturationScale { get; init; } = 1f;

    /// <summary>Multiplier applied to brightness (HSV value).</summary>
    public float ValueScale { get; init; } = 1f;

    /// <summary>
    /// Multiplier applied to sphere radii only, independently of the geometric scale, so spheres
    /// can swell or shrink without changing the spacing between them.
    /// </summary>
    public float SizeScale { get; init; } = 1f;

    [JsonIgnore]
    public bool IsNone => HueShift == 0f && SaturationScale == 1f && ValueScale == 1f && SizeScale == 1f;

    /// <summary>The non-neutral components, each as a short phrase.</summary>
    public IEnumerable<string> DescribeParts()
    {
        if (HueShift != 0f) yield return $"hue {HueShift:+0.#;-0.#}°";
        if (SaturationScale != 1f) yield return $"saturation ×{SaturationScale:0.###}";
        if (ValueScale != 1f) yield return $"brightness ×{ValueScale:0.###}";
        if (SizeScale != 1f) yield return $"size ×{SizeScale:0.###}";
    }

    public override string ToString() => IsNone ? "none" : string.Join(", ", DescribeParts());
}
