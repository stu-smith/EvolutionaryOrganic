using System.Numerics;
using System.Text.Json.Serialization;

namespace EvolutionaryOrganic.Core.Genetics;

/// <summary>Base for nodes that instantiate a single child several times.</summary>
public abstract class RepeatNode : GenomeNode
{
    public const int MaxCount = 64;

    private int _count = 6;

    public int Count
    {
        get => _count;
        set => _count = Math.Clamp(value, 1, MaxCount);
    }

    public GenomeNode Child { get; set; } = new SphereNode();

    /// <summary>Property transform compounded once per iteration (iteration 0 is unmodified).</summary>
    public PropertyModifier StepModifier { get; set; } = PropertyModifier.None;

    [JsonIgnore]
    public override IReadOnlyList<GenomeNode> Children => [Child];

    public override bool ReplaceChild(GenomeNode existing, GenomeNode replacement)
    {
        if (!ReferenceEquals(Child, existing)) return false;
        Child = replacement;
        return true;
    }

    /// <summary>
    /// The local transform and geometric scale factor of each iteration, in order.
    /// </summary>
    public abstract IEnumerable<(Matrix4x4 Matrix, float Scale)> Iterations();

    public override IEnumerable<GeneProperty> DescribeProperties()
    {
        foreach (var part in StepModifier.DescribeParts()) yield return new("Per step", part);
        foreach (var p in base.DescribeProperties()) yield return p;
    }

    protected T CopyRepeatTo<T>(T clone) where T : RepeatNode
    {
        clone.Count = Count;
        clone.Child = Child.DeepClone();
        clone.StepModifier = StepModifier;
        return CopyBaseTo(clone);
    }
}
