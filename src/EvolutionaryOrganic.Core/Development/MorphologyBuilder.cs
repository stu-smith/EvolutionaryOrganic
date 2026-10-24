using System.Numerics;
using EvolutionaryOrganic.Core.Genetics;

namespace EvolutionaryOrganic.Core.Development;

/// <summary>Grows a genome into its <see cref="Morphology"/> by walking the node tree.</summary>
public static class MorphologyBuilder
{
    public static Morphology Build(Genome genome, MorphologyLimits? limits = null)
    {
        var walker = new Walker(limits ?? MorphologyLimits.Default);
        walker.Visit(genome.Root, Matrix4x4.Identity, 1f, PropertyState.Initial, 0);
        return new Morphology(walker.Spheres, walker.Truncated, genome.Surface);
    }

    /// <summary>
    /// Counts the spheres a genome would produce without a budget, saturating at
    /// <paramref name="cap"/>. It is cheap enough to screen candidate mutations.
    /// </summary>
    public static long EstimateSphereCount(GenomeNode node, long cap = 1_000_000)
    {
        long count = node switch
        {
            SphereNode => 1,
            GroupNode g => g.Items.Sum(c => EstimateSphereCount(c, cap)),
            RepeatNode r => r.Count * EstimateSphereCount(r.Child, cap),
            _ => 0,
        };
        return Math.Min(count, cap);
    }

    /// <summary>Accumulated property transforms inherited from ancestors.</summary>
    private readonly record struct PropertyState(float Hue, float Saturation, float Value, float Size)
    {
        public static PropertyState Initial => new(0f, 1f, 1f, 1f);

        public PropertyState Apply(PropertyModifier m, int times = 1)
        {
            if (times == 0 || m.IsNone) return this;
            return new PropertyState(
                Hue + m.HueShift * times,
                Saturation * MathF.Pow(m.SaturationScale, times),
                Value * MathF.Pow(m.ValueScale, times),
                Size * MathF.Pow(m.SizeScale, times));
        }
    }

    private sealed class Walker(MorphologyLimits limits)
    {
        public List<SphereInstance> Spheres { get; } = [];

        public bool Truncated { get; private set; }

        public void Visit(GenomeNode node, Matrix4x4 parentWorld, float parentScale, PropertyState props, int depth)
        {
            if (Spheres.Count >= limits.MaxSpheres)
            {
                Truncated = true;
                return;
            }
            if (depth > limits.MaxDepth) return;

            var world = node.Offset.ToMatrix() * parentWorld;
            float scale = parentScale * node.Offset.Scale;
            if (scale < limits.MinRadius) return;
            props = props.Apply(node.Modifier);

            switch (node)
            {
                case SphereNode sphere:
                    float radius = sphere.Radius * scale * props.Size;
                    if (radius < limits.MinRadius) return;
                    var color = sphere.Color.Shift(props.Hue, props.Saturation, props.Value).ToRgb();
                    Spheres.Add(new SphereInstance(world.Translation, radius, color));
                    break;

                case GroupNode group:
                    foreach (var child in group.Items) Visit(child, world, scale, props, depth + 1);
                    break;

                case RepeatNode repeat:
                    int index = 0;
                    foreach (var (matrix, iterationScale) in repeat.Iterations())
                    {
                        Visit(repeat.Child, matrix * world, scale * iterationScale, props.Apply(repeat.StepModifier, index), depth + 1);
                        index++;
                        if (Truncated) return;
                    }
                    break;
            }
        }
    }
}
