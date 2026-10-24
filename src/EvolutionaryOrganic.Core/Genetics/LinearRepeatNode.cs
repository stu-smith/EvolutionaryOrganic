using System.Numerics;
using System.Text.Json.Serialization;

namespace EvolutionaryOrganic.Core.Genetics;

/// <summary>
/// Repeats its child along a path: every iteration applies <see cref="Step"/> to the previous one.
/// A translation alone makes a straight row; adding rotation curls it into an arc or spiral,
/// and a scale below 1 tapers it into a horn.
/// </summary>
public sealed class LinearRepeatNode : RepeatNode
{
    public Transform3 Step { get; set; } = new() { Translation = new Vector3(0f, 0.6f, 0f) };

    [JsonIgnore]
    public override string DisplayName => $"Linear repeat ×{Count}";

    public override IEnumerable<(Matrix4x4 Matrix, float Scale)> Iterations()
    {
        var step = Step.ToMatrix();
        var matrix = Matrix4x4.Identity;
        float scale = 1f;
        for (int i = 0; i < Count; i++)
        {
            yield return (matrix, scale);
            matrix *= step;
            scale *= Step.Scale;
        }
    }

    public override GenomeNode DeepClone() => CopyRepeatTo(new LinearRepeatNode { Step = Step });

    public override IEnumerable<GeneProperty> DescribeProperties()
    {
        foreach (var part in Step.DescribeParts()) yield return new("Step", part);
        foreach (var p in base.DescribeProperties()) yield return p;
    }
}
