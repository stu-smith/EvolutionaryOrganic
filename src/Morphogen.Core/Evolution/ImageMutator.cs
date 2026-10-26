using System.Numerics;
using Morphogen.Core.Development;
using Morphogen.Core.Genetics;
using Morphogen.Core.Genetics.Imaging;

namespace Morphogen.Core.Evolution;

/// <summary>Mutations of a genome's textures and bump maps, and of which spheres show them.</summary>
internal static class ImageMutator
{
    /// <summary>The texture and bump genes that at least one sphere shows, so that changing them is visible.</summary>
    public static List<ImageGene> VisibleGenes(Genome genome)
    {
        var spheres = genome.Root.DescendantsAndSelf().OfType<SphereNode>().ToList();
        var genes = new List<ImageGene>();
        for (int i = 0; i < genome.Textures.Count; i++)
        {
            if (spheres.Any(s => s.Texture == i)) genes.Add(genome.Textures[i]);
        }
        for (int i = 0; i < genome.BumpMaps.Count; i++)
        {
            if (spheres.Any(s => s.BumpMap == i)) genes.Add(genome.BumpMaps[i]);
        }
        return genes;
    }

    // ---------------------------------------------------------------- parameters

    public static void MutateParameters(ImageGene gene, Random rng, float s)
    {
        switch (gene)
        {
            case TextureGene texture when rng.Chance(0.35) && ColoredLeaves(texture) is { Count: > 0 } leaves:
                MutatePalette(rng.Pick(leaves).Palette!, rng, s);
                return;

            case BumpGene bump when rng.Chance(0.3):
                bump.Depth = Math.Clamp(bump.Depth * MathF.Exp(rng.NextGaussian() * 0.35f * s), 0.003f, 0.25f);
                return;
        }

        var nodes = gene.Pattern.DescendantsAndSelf().ToList();
        MutateNode(rng.Pick(nodes), rng, s);
    }

    private static void MutateNode(ImageNode node, Random rng, float s)
    {
        switch (node)
        {
            case NoiseImageNode noise:
                switch (rng.Next(4))
                {
                    case 0: noise.Frequency = JiggleFrequency(noise.Frequency, rng, s); break;
                    case 1: noise.Octaves += rng.Chance(0.5) ? 1 : -1; break;
                    case 2: noise.Style = (NoiseStyle)rng.Next(3); break;
                    default: noise.Seed = rng.Next(); break;
                }
                break;

            case CellsImageNode cells:
                switch (rng.Next(3))
                {
                    case 0: cells.Frequency = JiggleFrequency(cells.Frequency, rng, s); break;
                    case 1: cells.Style = cells.Style == CellStyle.Spots ? CellStyle.Edges : CellStyle.Spots; break;
                    default: cells.Seed = rng.Next(); break;
                }
                break;

            case WavesImageNode waves:
                switch (rng.Next(4))
                {
                    case 0: waves.Frequency = JiggleFrequency(waves.Frequency, rng, s); break;
                    case 1: waves.Axis = Vector3.Normalize(waves.Axis + RandomVector(rng) * 0.4f * s + new Vector3(1e-4f)); break;
                    case 2: waves.Twist += rng.Chance(0.5) ? 1 : -1; break;
                    default: waves.Phase = (waves.Phase + rng.NextGaussian() * 60f * s + 360f) % 360f; break;
                }
                break;

            case WarpImageNode warp:
                switch (rng.Next(3))
                {
                    case 0: warp.Amount = Math.Clamp(warp.Amount * MathF.Exp(rng.NextGaussian() * 0.4f * s), 0.01f, 1.2f); break;
                    case 1: warp.Frequency = JiggleFrequency(warp.Frequency, rng, s); break;
                    default: warp.Seed = rng.Next(); break;
                }
                break;

            case TransformImageNode transform:
                if (rng.Chance(0.5)) transform.Scale = Math.Clamp(transform.Scale * MathF.Exp(rng.NextGaussian() * 0.3f * s), 0.2f, 5f);
                else transform.Rotation += RandomVector(rng) * 30f * s;
                break;

            case ToneImageNode tone:
                if (rng.Chance(0.2)) tone.Function = (ToneFunction)rng.Next(5);
                else tone.Amount = Math.Clamp(tone.Amount + rng.NextGaussian() * 1.2f * s, 1f, 12f);
                break;

            case BlendImageNode blend:
                if (rng.Chance(0.3)) blend.Mode = (BlendMode)rng.Next(6);
                else blend.Weight = Math.Clamp(blend.Weight + rng.NextGaussian() * 0.2f * s, 0f, 1f);
                break;

            case MaskImageNode mask:
                mask.Sharpness = Math.Clamp(mask.Sharpness * MathF.Exp(rng.NextGaussian() * 0.4f * s), 0.5f, 20f);
                break;
        }
    }

    private static void MutatePalette(List<HsvColor> palette, Random rng, float s)
    {
        if (rng.Chance(0.1))
        {
            // Add or drop a stop, keeping at least two.
            if (palette.Count < PatternImageNode.MaxStops && (palette.Count <= 2 || rng.Chance(0.5)))
            {
                int at = rng.Next(1, palette.Count);
                var a = palette[at - 1];
                var b = palette[at];
                palette.Insert(at, new HsvColor(
                    a.Hue + rng.NextGaussian() * 60f, (a.Saturation + b.Saturation) * 0.5f, (a.Value + b.Value) * 0.5f).Normalized());
            }
            else if (palette.Count > 2)
            {
                palette.RemoveAt(rng.Next(palette.Count));
            }
            return;
        }

        int stop = rng.Next(palette.Count);
        palette[stop] = new HsvColor(
            palette[stop].Hue + rng.NextGaussian() * 35f * s,
            palette[stop].Saturation + rng.NextGaussian() * 0.12f * s,
            palette[stop].Value + rng.NextGaussian() * 0.12f * s).Normalized();
    }

    private static List<PatternImageNode> ColoredLeaves(ImageGene gene) =>
        gene.Pattern.DescendantsAndSelf().OfType<PatternImageNode>().Where(p => p.Palette is { Count: > 0 }).ToList();

    /// <summary>
    /// The hue to colour a texture's new leaves around: near one of its existing leaves, so they fit in.
    /// Null for bump maps, whose leaves are greyscale.
    /// </summary>
    private static float? ThemeHue(ImageGene gene, Random rng)
    {
        if (gene is not TextureGene) return null;
        var leaves = ColoredLeaves(gene);
        return leaves.Count > 0 ? rng.Pick(rng.Pick(leaves).Palette!).Hue : rng.NextFloat(0f, 360f);
    }

    /// <summary>Turns the hue of every colour in a texture.</summary>
    public static void ShiftHue(TextureGene gene, float degrees)
    {
        foreach (var leaf in ColoredLeaves(gene))
        {
            for (int i = 0; i < leaf.Palette!.Count; i++) leaf.Palette[i] = leaf.Palette[i].Shift(degrees, 1f, 1f);
        }
    }

    private static float JiggleFrequency(float frequency, Random rng, float s) =>
        Math.Clamp(frequency * MathF.Exp(rng.NextGaussian() * 0.25f * s), 0.5f, 24f);

    private static Vector3 RandomVector(Random rng) => new(rng.NextGaussian(), rng.NextGaussian(), rng.NextGaussian());

    // ---------------------------------------------------------------- structure

    /// <summary>Restructures an image tree: wraps, combines, unwraps or replaces one of its nodes.</summary>
    public static void MutateStructure(ImageGene gene, Random rng)
    {
        var nodes = gene.Pattern.DescendantsAndSelf().ToList();
        var node = rng.Pick(nodes);
        float? hue = ThemeHue(gene, rng);
        bool canGrow = nodes.Count < ImageGene.MaxNodes - 2 && gene.Pattern.Depth() < ImageGene.MaxDepth;

        switch (rng.PickWeighted(canGrow ? 1.0 : 0, canGrow ? 0.8 : 0, 0.7, 0.6))
        {
            case 0: // Wrap the node in a single-input operation.
                ImageNode wrapper = rng.Next(3) switch
                {
                    0 => RandomImageFactory.CreateWarp(rng, node),
                    1 => RandomImageFactory.CreateTransform(rng, node),
                    _ => RandomImageFactory.CreateTone(rng, node),
                };
                gene.Replace(node, wrapper);
                break;

            case 1: // Combine the node with a new pattern.
                var other = RandomImageFactory.CreatePattern(rng, hue);
                ImageNode combined = rng.Chance(0.65)
                    ? RandomImageFactory.CreateBlend(rng, node, other)
                    : RandomImageFactory.CreateMask(rng, node, other, RandomImageFactory.CreatePattern(rng, hue));
                gene.Replace(node, combined);
                break;

            case 2: // Remove an operation, keeping one of its inputs.
                if (nodes.Where(n => n.Inputs.Count > 0).ToList() is { Count: > 0 } operations)
                {
                    var operation = rng.Pick(operations);
                    gene.Replace(operation, rng.Pick(operation.Inputs));
                }
                break;

            default: // Replace a small subtree with something new.
                if (node.Depth() <= 2) gene.Replace(node, RandomImageFactory.CreateImage(rng, rng.Next(0, 3), hue));
                break;
        }
    }

    /// <summary>Shrinks an image tree until it fits within <see cref="ImageGene.MaxNodes"/> and <see cref="ImageGene.MaxDepth"/>.</summary>
    public static void Prune(ImageGene gene, Random rng)
    {
        for (int guard = 0; guard < 100; guard++)
        {
            if (gene.Pattern.DescendantsAndSelf().Count() <= ImageGene.MaxNodes && gene.Pattern.Depth() <= ImageGene.MaxDepth) return;

            // Collapse an operation on the deepest path into its deepest input.
            var path = new List<ImageNode>();
            for (var node = gene.Pattern; node.Inputs.Count > 0; node = node.Inputs.MaxBy(c => c.Depth())!) path.Add(node);
            var victim = rng.Pick(path);
            gene.Replace(victim, victim.Inputs.MaxBy(c => c.Depth())!);
        }
    }

    // ---------------------------------------------------------------- genome-level

    /// <summary>Adds a new random texture and shows it on one of the spheres.</summary>
    public static void AddTexture(Genome genome, Random rng)
    {
        if (PickSphere(genome, rng, s => s.Texture is null) is not { } sphere) return;
        genome.Textures.Add(RandomImageFactory.CreateTexture(rng));
        sphere.ShowTexture(genome.Textures.Count - 1);
    }

    /// <summary>Adds a new random bump map and shows it on one of the spheres.</summary>
    public static void AddBumpMap(Genome genome, Random rng)
    {
        if (PickSphere(genome, rng, s => s.BumpMap is null) is not { } sphere) return;
        genome.BumpMaps.Add(RandomImageFactory.CreateBumpMap(rng));
        sphere.BumpMap = genome.BumpMaps.Count - 1;
    }

    /// <summary>Switches a sphere between a plain colour and one of the genome's textures (and likewise for bumps).</summary>
    public static void MutateAppearance(SphereNode sphere, Genome genome, Random rng)
    {
        if (rng.Chance(0.5) && genome.Textures.Count > 0)
        {
            if (sphere.Texture is null || (rng.Chance(0.4) && genome.Textures.Count > 1)) sphere.ShowTexture(rng.Next(genome.Textures.Count));
            // The texture's overall colour, so the sphere does not change colour completely.
            else sphere.ShowColor(FallbackColor(sphere.Texture, genome.Textures));
        }
        else if (genome.BumpMaps.Count > 0)
        {
            sphere.BumpMap = sphere.BumpMap is null || (rng.Chance(0.4) && genome.BumpMaps.Count > 1)
                ? rng.Next(genome.BumpMaps.Count)
                : null;
        }
    }

    /// <summary>
    /// Drops textures and bump maps that no sphere shows, then any beyond the limits, renumbering the
    /// spheres' references to match. Spheres that lose their image fall back to a plain colour (the texture's
    /// overall colour) or smooth surface.
    /// </summary>
    public static void Compact(Genome genome, Random rng, int maxTextures, int maxBumpMaps)
    {
        var spheres = genome.Root.DescendantsAndSelf().OfType<SphereNode>().ToList();
        var oldTextures = genome.Textures.ToList();
        var oldIndices = spheres.Select(s => s.Texture).ToList();
        CompactList(genome.Textures, spheres, s => s.Texture, (s, i) => s.Texture = i, Math.Max(0, maxTextures));
        for (int i = 0; i < spheres.Count; i++)
        {
            if (spheres[i].Texture is null && spheres[i].Color is null) spheres[i].Color = FallbackColor(oldIndices[i], oldTextures);
        }
        CompactList(genome.BumpMaps, spheres, s => s.BumpMap, (s, i) => s.BumpMap = i, Math.Max(0, maxBumpMaps));
        foreach (var gene in genome.Textures.Concat<ImageGene>(genome.BumpMaps)) Prune(gene, rng);
    }

    private static void CompactList<T>(List<T> genes, List<SphereNode> spheres, Func<SphereNode, int?> get, Action<SphereNode, int?> set, int max)
    {
        var remap = new Dictionary<int, int>();
        var kept = new List<T>();
        for (int i = 0; i < genes.Count && kept.Count < max; i++)
        {
            if (!spheres.Any(s => get(s) == i)) continue;
            remap[i] = kept.Count;
            kept.Add(genes[i]);
        }
        foreach (var sphere in spheres)
        {
            if (get(sphere) is int index) set(sphere, remap.TryGetValue(index, out int newIndex) ? newIndex : null);
        }
        genes.Clear();
        genes.AddRange(kept);
    }

    private static HsvColor FallbackColor(int? texture, List<TextureGene> textures) =>
        texture is int t && t >= 0 && t < textures.Count
            ? HsvColor.FromRgb(ImageBaker.AverageColor(textures[t]))
            : SphereNode.DefaultColor;

    private static SphereNode? PickSphere(Genome genome, Random rng, Func<SphereNode, bool> preferred)
    {
        var spheres = genome.Root.DescendantsAndSelf().OfType<SphereNode>().ToList();
        if (spheres.Count == 0) return null;
        var candidates = spheres.Where(preferred).ToList();
        return rng.Pick(candidates.Count > 0 ? candidates : spheres);
    }
}
