using System.Numerics;
using EvolutionaryOrganic.Core.Development;
using EvolutionaryOrganic.Core.Genetics;
using EvolutionaryOrganic.Core.Serialization;

namespace EvolutionaryOrganic.Core.Evolution;

/// <summary>Controls how far mutants stray from their parent.</summary>
public sealed record MutationSettings
{
    public static MutationSettings Default { get; } = new();

    /// <summary>0 = barely perceptible tweaks, 1 = wild leaps.</summary>
    public float Strength { get; init; } = 0.5f;

    /// <summary>Chance that any single mutation alters the tree's structure rather than its parameters.</summary>
    public float StructuralProbability { get; init; } = 0.2f;

    /// <summary>Mutants that would grow more spheres than this are pruned back.</summary>
    public int MaxSpheres { get; init; } = 3000;

    /// <summary>Maximum nesting depth of the genome tree.</summary>
    public int MaxTreeDepth { get; init; } = 8;
}

/// <summary>Produces mutated offspring of a genome.</summary>
public static class Mutator
{
    public static Genome Mutate(Genome parent, Random rng, MutationSettings? settings = null)
    {
        settings ??= MutationSettings.Default;
        string parentJson = GenomeSerializer.Serialize(parent);

        for (int attempt = 0; attempt < 20; attempt++)
        {
            var child = parent.DeepClone();
            child.Generation = parent.Generation + 1;

            int mutations = 1 + (int)MathF.Floor(rng.NextFloat(0f, 1f + settings.Strength * 4f));
            for (int i = 0; i < mutations; i++)
            {
                if (rng.Chance(settings.StructuralProbability)) MutateStructure(child, rng, settings);
                else MutateParameters(child, rng, settings.Strength);
            }

            EnforceLimits(child, rng, settings);
            if (GenomeSerializer.Serialize(child) != parentJson) return child;
        }

        // Extremely unlikely: every attempt was a no-op. Fall back to a guaranteed visible change.
        var fallback = parent.DeepClone();
        fallback.Generation = parent.Generation + 1;
        foreach (var sphere in fallback.Root.DescendantsAndSelf().OfType<SphereNode>())
        {
            sphere.Color = sphere.Color.Shift(30f, 1f, 1f);
        }
        return fallback;
    }

    // ---------------------------------------------------------------- parameters

    private static void MutateParameters(Genome genome, Random rng, float strength)
    {
        float s = 0.25f + strength * 1.5f;
        if (rng.Chance(0.1))
        {
            genome.Surface = MutateSurface(genome.Surface, rng, s);
            return;
        }

        var nodes = genome.Root.DescendantsAndSelf().ToList();
        var node = rng.Pick(nodes);

        // Mutations of the node's own genes are more common than of its placement.
        if (rng.Chance(0.25))
        {
            if (rng.Chance(0.7)) node.Offset = Jiggle(node.Offset, rng, s);
            else node.Modifier = Jiggle(node.Modifier, rng, s, isStep: false);
            return;
        }

        switch (node)
        {
            case SphereNode sphere:
                if (rng.Chance(0.5)) sphere.Radius = Math.Clamp(sphere.Radius * MathF.Exp(rng.NextGaussian() * 0.25f * s), 0.02f, 3f);
                else sphere.Color = JiggleColor(sphere.Color, rng, s);
                break;

            case LinearRepeatNode linear:
                switch (rng.Next(4))
                {
                    case 0: linear.Count = JiggleCount(linear.Count, rng, s); break;
                    case 1: linear.Step = Jiggle(linear.Step, rng, s, isStep: true); break;
                    case 2: linear.StepModifier = Jiggle(linear.StepModifier, rng, s, isStep: true); break;
                    default: linear.Step = linear.Step with { Rotation = linear.Step.Rotation + RandomVector(rng) * 8f * s }; break;
                }
                break;

            case RadialRepeatNode radial:
                switch (rng.Next(5))
                {
                    case 0: radial.Count = JiggleCount(radial.Count, rng, s); break;
                    case 1: radial.Radius = Math.Clamp(radial.Radius * MathF.Exp(rng.NextGaussian() * 0.25f * s), 0f, 6f); break;
                    case 2: radial.Tilt = Math.Clamp(radial.Tilt + rng.NextGaussian() * 25f * s, -180f, 180f); break;
                    case 3: radial.Sweep = MutateSweep(radial.Sweep, rng, s); break;
                    default: radial.StepModifier = Jiggle(radial.StepModifier, rng, s, isStep: true); break;
                }
                break;

            case GroupNode group when group.Items.Count > 0:
                var part = rng.Pick(group.Items);
                part.Offset = Jiggle(part.Offset, rng, s);
                break;
        }
    }

    private static SurfaceGenes MutateSurface(SurfaceGenes surface, Random rng, float s) => surface with
    {
        Roughness = Math.Clamp(surface.Roughness + rng.NextGaussian() * 0.12f * s, 0.04f, 1f),
        Metallic = Math.Clamp(surface.Metallic + rng.NextGaussian() * 0.15f * s, 0f, 1f),
        ClearCoat = Math.Clamp(surface.ClearCoat + rng.NextGaussian() * 0.2f * s, 0f, 1f),
    };

    private static int JiggleCount(int count, Random rng, float s)
    {
        int delta = Math.Max(1, (int)MathF.Round(MathF.Abs(rng.NextGaussian()) * 2.5f * s));
        return Math.Clamp(count + (rng.Chance(0.5) ? delta : -delta), 1, RepeatNode.MaxCount);
    }

    private static float MutateSweep(float sweep, Random rng, float s)
    {
        if (sweep >= 360f && rng.Chance(0.6)) return 360f - MathF.Abs(rng.NextGaussian()) * 90f * s;
        if (rng.Chance(0.15)) return 360f;
        return Math.Clamp(sweep + rng.NextGaussian() * 50f * s, 20f, 360f);
    }

    private static HsvColor JiggleColor(HsvColor color, Random rng, float s) => new HsvColor(
        color.Hue + rng.NextGaussian() * 35f * s,
        color.Saturation + rng.NextGaussian() * 0.12f * s,
        color.Value + rng.NextGaussian() * 0.12f * s).Normalized();

    private static Vector3 RandomVector(Random rng) => new(rng.NextGaussian(), rng.NextGaussian(), rng.NextGaussian());

    private static Transform3 Jiggle(Transform3 t, Random rng, float s, bool isStep = false)
    {
        // Step transforms compound over every iteration, so they must be nudged much more gently.
        float spread = isStep ? 0.35f : 1f;
        return rng.Next(3) switch
        {
            0 => t with { Translation = t.Translation + RandomVector(rng) * 0.15f * s * spread },
            1 => t with { Rotation = WrapAngles(t.Rotation + RandomVector(rng) * 25f * s * spread) },
            _ => t with
            {
                Scale = isStep
                    ? Math.Clamp(t.Scale * MathF.Exp(rng.NextGaussian() * 0.03f * s), 0.6f, 1.12f)
                    : Math.Clamp(t.Scale * MathF.Exp(rng.NextGaussian() * 0.2f * s), 0.05f, 4f),
            },
        };
    }

    private static PropertyModifier Jiggle(PropertyModifier m, Random rng, float s, bool isStep)
    {
        float spread = isStep ? 0.25f : 1f;
        return rng.Next(4) switch
        {
            0 => m with { HueShift = m.HueShift + rng.NextGaussian() * 30f * s * spread },
            1 => m with { SaturationScale = Math.Clamp(m.SaturationScale * MathF.Exp(rng.NextGaussian() * 0.2f * s * spread), 0.2f, 3f) },
            2 => m with { ValueScale = Math.Clamp(m.ValueScale * MathF.Exp(rng.NextGaussian() * 0.2f * s * spread), 0.2f, 3f) },
            _ => m with { SizeScale = Math.Clamp(m.SizeScale * MathF.Exp(rng.NextGaussian() * 0.25f * s * spread), 0.3f, 3f) },
        };
    }

    private static Vector3 WrapAngles(Vector3 v) => new(WrapAngle(v.X), WrapAngle(v.Y), WrapAngle(v.Z));

    private static float WrapAngle(float a)
    {
        a %= 360f;
        if (a > 180f) a -= 360f;
        if (a < -180f) a += 360f;
        return a;
    }

    // ---------------------------------------------------------------- structure

    private static void MutateStructure(Genome genome, Random rng, MutationSettings settings)
    {
        var nodes = genome.Root.DescendantsAndSelf().ToList();
        var node = rng.Pick(nodes);
        bool canDeepen = genome.Root.Depth() < settings.MaxTreeDepth;

        switch (rng.PickWeighted(canDeepen ? 1.0 : 0, 1.0, 1.2, 0.8, 0.8, 0.8, 0.6))
        {
            case 0: // Wrap the node in a new repeat.
                var wrapper = rng.Chance(0.55)
                    ? (RepeatNode)RandomGenomeFactory.CreateLinearRepeat(rng, node)
                    : RandomGenomeFactory.CreateRadialRepeat(rng, node);
                wrapper.Count = Math.Min(wrapper.Count, rng.Next(2, 8));
                wrapper.Offset = node.Offset;
                node.Offset = Transform3.Identity;
                genome.Replace(node, wrapper);
                break;

            case 1: // Remove a repeat, keeping its child.
                if (nodes.OfType<RepeatNode>().ToList() is { Count: > 0 } repeats)
                {
                    var repeat = rng.Pick(repeats);
                    if (repeat.Child.Offset.IsIdentity) repeat.Child.Offset = repeat.Offset;
                    genome.Replace(repeat, repeat.Child);
                }
                break;

            case 2: // Add a part alongside the node.
                var addition = rng.Chance(0.5) ? rng.Pick(nodes).DeepClone() : RandomGenomeFactory.CreateNode(rng, rng.Next(0, 3));
                addition.Offset = RandomGenomeFactory.CreateOffset(rng);
                if (node is GroupNode existingGroup)
                {
                    existingGroup.Items.Add(addition);
                }
                else
                {
                    var group = new GroupNode { Offset = node.Offset };
                    node.Offset = Transform3.Identity;
                    genome.Replace(node, group);
                    group.Items.Add(node);
                    group.Items.Add(addition);
                }
                break;

            case 3: // Remove a part from a group.
                if (nodes.OfType<GroupNode>().Where(g => g.Items.Count > 1).ToList() is { Count: > 0 } groups)
                {
                    var g = rng.Pick(groups);
                    g.Items.RemoveAt(rng.Next(g.Items.Count));
                }
                break;

            case 4: // Replace a small subtree with something new.
                if (node.Depth() <= 2)
                {
                    var fresh = RandomGenomeFactory.CreateNode(rng, rng.Next(1, 3));
                    fresh.Offset = node.Offset;
                    genome.Replace(node, fresh);
                }
                break;

            case 5: // Copy one subtree over a leaf elsewhere (gene duplication).
                var leaves = nodes.OfType<SphereNode>().ToList();
                var source = rng.Pick(nodes);
                var target = rng.Pick(leaves);
                if (!ReferenceEquals(source, genome.Root) && !ReferenceEquals(source, target))
                {
                    var copy = source.DeepClone();
                    copy.Offset = target.Offset;
                    genome.Replace(target, copy);
                }
                break;

            default: // Turn a linear repeat into a radial one, or vice versa.
                if (nodes.OfType<RepeatNode>().ToList() is { Count: > 0 } swappable)
                {
                    var old = rng.Pick(swappable);
                    RepeatNode swapped = old is LinearRepeatNode
                        ? RandomGenomeFactory.CreateRadialRepeat(rng, old.Child)
                        : RandomGenomeFactory.CreateLinearRepeat(rng, old.Child);
                    swapped.Count = old.Count;
                    swapped.Offset = old.Offset;
                    swapped.Modifier = old.Modifier;
                    swapped.StepModifier = old.StepModifier;
                    genome.Replace(old, swapped);
                }
                break;
        }
    }

    /// <summary>Prunes the genome until it fits within the sphere budget and depth limit.</summary>
    private static void EnforceLimits(Genome genome, Random rng, MutationSettings settings)
    {
        for (int guard = 0; guard < 200; guard++)
        {
            bool tooBig = MorphologyBuilder.EstimateSphereCount(genome.Root) > settings.MaxSpheres;
            bool tooDeep = genome.Root.Depth() > settings.MaxTreeDepth;
            if (!tooBig && !tooDeep) return;

            if (tooDeep)
            {
                CollapseOnDeepestPath(genome, rng);
                continue;
            }

            var repeats = genome.Root.DescendantsAndSelf().OfType<RepeatNode>().ToList();
            if (repeats.Count == 0) return;

            var largest = repeats.Where(r => r.Count > 2).OrderByDescending(r => r.Count).FirstOrDefault()
                          ?? rng.Pick(repeats);
            if (largest.Count > 2) largest.Count = (int)MathF.Ceiling(largest.Count * 0.8f);
            else genome.Replace(largest, largest.Child);
        }
    }

    /// <summary>
    /// Removes one level from the deepest branch, preferring to unwrap a repeat over
    /// discarding the other parts of a group.
    /// </summary>
    private static void CollapseOnDeepestPath(Genome genome, Random rng)
    {
        var path = new List<GenomeNode>();
        for (var node = genome.Root; node.Children.Count > 0; node = node.Children.MaxBy(c => c.Depth())!)
        {
            path.Add(node);
        }
        if (path.Count == 0) return;

        var repeats = path.OfType<RepeatNode>().ToList();
        var victim = repeats.Count > 0 ? rng.Pick(repeats) : rng.Pick(path);
        var survivor = victim.Children.MaxBy(c => c.Depth())!;
        if (survivor.Offset.IsIdentity) survivor.Offset = victim.Offset;
        genome.Replace(victim, survivor);
    }
}
