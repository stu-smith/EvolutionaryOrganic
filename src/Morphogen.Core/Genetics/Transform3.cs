using System.Numerics;
using System.Text.Json.Serialization;

namespace Morphogen.Core.Genetics;

/// <summary>
/// A similarity transform: uniform scale, then rotation (Euler angles in degrees, applied X→Y→Z),
/// then translation. Uniform scale keeps spheres spherical, and keeps the world-space radius
/// computable as a simple product of scale factors.
/// </summary>
public sealed record Transform3
{
    public static Transform3 Identity { get; } = new();

    public Vector3 Translation { get; init; } = Vector3.Zero;

    /// <summary>Rotation as Euler angles in degrees about the X, Y and Z axes.</summary>
    public Vector3 Rotation { get; init; } = Vector3.Zero;

    public float Scale { get; init; } = 1f;

    [JsonIgnore]
    public bool IsIdentity => Translation == Vector3.Zero && Rotation == Vector3.Zero && Scale == 1f;

    /// <summary>Builds the matrix for this transform (System.Numerics row-vector convention: v * M).</summary>
    public Matrix4x4 ToMatrix()
    {
        const float toRadians = MathF.PI / 180f;
        return Matrix4x4.CreateScale(Scale)
             * Matrix4x4.CreateRotationX(Rotation.X * toRadians)
             * Matrix4x4.CreateRotationY(Rotation.Y * toRadians)
             * Matrix4x4.CreateRotationZ(Rotation.Z * toRadians)
             * Matrix4x4.CreateTranslation(Translation);
    }

    /// <summary>The non-identity components, each as a short phrase.</summary>
    public IEnumerable<string> DescribeParts()
    {
        if (Translation != Vector3.Zero) yield return $"move ({Translation.X:0.##}, {Translation.Y:0.##}, {Translation.Z:0.##})";
        if (Rotation != Vector3.Zero) yield return $"rotate ({Rotation.X:0.#}°, {Rotation.Y:0.#}°, {Rotation.Z:0.#}°)";
        if (Scale != 1f) yield return $"scale ×{Scale:0.###}";
    }

    public override string ToString() => IsIdentity ? "identity" : string.Join(", ", DescribeParts());
}
