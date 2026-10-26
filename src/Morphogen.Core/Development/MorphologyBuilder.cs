using System.Numerics;
using Morphogen.Core.Genetics;

namespace Morphogen.Core.Development;

/// <summary>Grows a genome into its <see cref="Morphology"/> by walking the node tree.</summary>
public static class MorphologyBuilder
{
    public static Morphology Build(Genome genome, MorphologyLimits? limits = null)
    {
        limits ??= MorphologyLimits.Default;
        var walker = new Walker(limits, genome.Textures.Count, genome.BumpMaps.Count);
        walker.Visit(genome.Root, Matrix4x4.Identity, 1f, PropertyState.Initial, 0);

        // Only images that some sphere shows are worth the time to bake.
        var textures = genome.Textures
            .Select((gene, i) => walker.Spheres.Any(s => s.Texture == i) ? ImageBaker.BakeTexture(gene, limits.TextureSize) : null)
            .ToList();
        var bumpMaps = genome.BumpMaps
            .Select((gene, i) => walker.Spheres.Any(s => s.BumpMap == i) ? ImageBaker.BakeBumpMap(gene, limits.TextureSize) : null)
            .ToList();
        var surfaces = FusedSurfaceBuilder.Build(walker.Spheres, textures, genome.BumpMaps, limits);
        return new Morphology(walker.Spheres, walker.Truncated, genome.Surface, textures, bumpMaps, surfaces);
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

    /// <summary>
    /// Accumulated property transforms inherited from ancestors. Fusion is kept as the nearest ancestor's
    /// <see cref="GenomeNode.Fusion"/> and, separately, the product of every fusion scale above, so a subtree
    /// that sets its own fusion still follows the gradients of the repeats it sits in.
    /// </summary>
    private readonly record struct PropertyState(float Hue, float Saturation, float Value, float Size, float Fusion, float FusionScale)
    {
        public static PropertyState Initial => new(0f, 1f, 1f, 1f, 0f, 1f);

        public float EffectiveFusion => Math.Clamp(Fusion * FusionScale, 0f, GenomeNode.MaxFusion);

        public PropertyState Apply(PropertyModifier m, int times = 1)
        {
            if (times == 0 || m.IsNone) return this;
            return new PropertyState(
                Hue + m.HueShift * times,
                Saturation * MathF.Pow(m.SaturationScale, times),
                Value * MathF.Pow(m.ValueScale, times),
                Size * MathF.Pow(m.SizeScale, times),
                Fusion,
                FusionScale * MathF.Pow(m.FusionScale, times));
        }
    }

    private sealed class Walker(MorphologyLimits limits, int textureCount, int bumpMapCount)
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
            if (node.Fusion is float fusion) props = props with { Fusion = fusion };
            props = props.Apply(node.Modifier);

            switch (node)
            {
                case SphereNode sphere:
                    float radius = sphere.Radius * scale * props.Size;
                    if (radius < limits.MinRadius) return;
                    int texture = ValidIndex(sphere.Texture, textureCount);
                    // A texture shows its own colours, untouched by colour modifiers; white leaves it untinted.
                    var color = texture >= 0
                        ? Vector3.One
                        : (sphere.Color ?? SphereNode.DefaultColor).Shift(props.Hue, props.Saturation, props.Value).ToRgb();
                    var orientation = Matrix4x4.Decompose(world, out _, out var rotation, out _) ? rotation : Quaternion.Identity;
                    Spheres.Add(new SphereInstance(
                        world.Translation,
                        radius,
                        color,
                        orientation,
                        Math.Clamp(sphere.Roughness, 0f, 1f),
                        texture,
                        ValidIndex(sphere.BumpMap, bumpMapCount),
                        props.EffectiveFusion));
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

        /// <summary>A hand-edited genome may refer to images it lacks; such spheres are drawn plain (in the default colour if they have none).</summary>
        private static int ValidIndex(int? index, int count) => index is int i && i >= 0 && i < count ? i : -1;
    }
}
