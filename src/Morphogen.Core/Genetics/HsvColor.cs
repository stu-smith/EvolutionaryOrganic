using System.Numerics;

namespace Morphogen.Core.Genetics;

/// <summary>
/// A colour expressed in HSV space, which mutates far more naturally than RGB:
/// hue drifts around the wheel, while saturation and value stay in [0, 1].
/// </summary>
public readonly record struct HsvColor(float Hue, float Saturation, float Value)
{
    /// <summary>Returns the colour with hue wrapped into [0, 360) and S/V clamped into [0, 1].</summary>
    public HsvColor Normalized() => new(WrapHue(Hue), Math.Clamp(Saturation, 0f, 1f), Math.Clamp(Value, 0f, 1f));

    public HsvColor Shift(float hueDelta, float saturationScale, float valueScale) =>
        new HsvColor(Hue + hueDelta, Saturation * saturationScale, Value * valueScale).Normalized();

    /// <summary>Converts to sRGB components in [0, 1].</summary>
    public Vector3 ToRgb()
    {
        var c = Normalized();
        float h = c.Hue / 60f;
        float chroma = c.Value * c.Saturation;
        float x = chroma * (1f - MathF.Abs(h % 2f - 1f));
        float m = c.Value - chroma;
        var (r, g, b) = (int)h switch
        {
            0 => (chroma, x, 0f),
            1 => (x, chroma, 0f),
            2 => (0f, chroma, x),
            3 => (0f, x, chroma),
            4 => (x, 0f, chroma),
            _ => (chroma, 0f, x),
        };
        return new Vector3(r + m, g + m, b + m);
    }

    public static HsvColor FromRgb(Vector3 rgb)
    {
        float max = MathF.Max(rgb.X, MathF.Max(rgb.Y, rgb.Z));
        float min = MathF.Min(rgb.X, MathF.Min(rgb.Y, rgb.Z));
        float delta = max - min;
        float hue = 0f;
        if (delta > 1e-6f)
        {
            if (max == rgb.X) hue = 60f * ((rgb.Y - rgb.Z) / delta % 6f);
            else if (max == rgb.Y) hue = 60f * ((rgb.Z - rgb.X) / delta + 2f);
            else hue = 60f * ((rgb.X - rgb.Y) / delta + 4f);
        }
        float saturation = max <= 1e-6f ? 0f : delta / max;
        return new HsvColor(hue, saturation, max).Normalized();
    }

    public override string ToString() => $"H {Hue:0}° S {Saturation:0.00} V {Value:0.00}";

    private static float WrapHue(float hue)
    {
        hue %= 360f;
        return hue < 0f ? hue + 360f : hue;
    }
}
