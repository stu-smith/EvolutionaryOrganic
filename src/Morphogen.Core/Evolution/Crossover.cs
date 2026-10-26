using Morphogen.Core.Genetics;
using Morphogen.Core.Serialization;

namespace Morphogen.Core.Evolution;

/// <summary>
/// Produces offspring of two genomes by sexual crossover: a copy of one parent has one of its subtrees
/// replaced by a subtree of the other, and the whole-creation genes are inherited from either parent.
/// </summary>
public static class Crossover
{
    public static Genome Cross(Genome first, Genome second, Random rng, MutationSettings? settings = null)
    {
        settings ??= MutationSettings.Default;
        string firstJson = GenomeSerializer.Serialize(first);
        string secondJson = GenomeSerializer.Serialize(second);

        for (int attempt = 0; attempt < 20; attempt++)
        {
            // Either parent can provide the body plan, so a brood shows both directions of the cross.
            var (host, donor) = rng.Chance(0.5) ? (first, second) : (second, first);
            var child = host.DeepClone();
            child.Generation = Math.Max(first.Generation, second.Generation) + 1;

            child.Surface = new SurfaceGenes
            {
                Metallic = rng.Chance(0.5) ? host.Surface.Metallic : donor.Surface.Metallic,
                ClearCoat = rng.Chance(0.5) ? host.Surface.ClearCoat : donor.Surface.ClearCoat,
            };
            Transplant(child, donor, rng);
            if (rng.Chance(0.25)) SwapImageGene(child, donor, rng);

            Mutator.EnforceLimits(child, rng, settings);
            string json = GenomeSerializer.Serialize(child);
            if (json != firstJson && json != secondJson) return child;
        }

        // The parents are identical, or every cross reproduced one of them: mutate instead, so the brood still varies.
        return Mutator.Mutate(first, rng, settings);
    }

    /// <summary>
    /// Replaces a random subtree of <paramref name="child"/> with a copy of a random subtree of <paramref name="donor"/>,
    /// bringing along the textures and bump maps the copied spheres show.
    /// </summary>
    private static void Transplant(Genome child, Genome donor, Random rng)
    {
        var targets = child.Root.DescendantsAndSelf().ToList();
        // Replacing the root would discard the host altogether, so only do that when it is a lone sphere.
        if (targets.Count > 1) targets.RemoveAt(0);
        var target = rng.Pick(targets);

        var graft = rng.Pick(donor.Root.DescendantsAndSelf().ToList()).DeepClone();
        if (rng.Chance(0.5)) graft.Offset = target.Offset;

        var textures = new Dictionary<int, int>();
        var bumpMaps = new Dictionary<int, int>();
        foreach (var sphere in graft.DescendantsAndSelf().OfType<SphereNode>())
        {
            sphere.Texture = Import(sphere.Texture, donor.Textures, child.Textures, textures, t => t.DeepClone());
            sphere.BumpMap = Import(sphere.BumpMap, donor.BumpMaps, child.BumpMaps, bumpMaps, b => b.DeepClone());
        }

        child.Replace(target, graft);
    }

    /// <summary>Copies the donor's image gene at <paramref name="index"/> into the child (once), returning its new index.</summary>
    private static int? Import<T>(int? index, List<T> from, List<T> to, Dictionary<int, int> imported, Func<T, T> clone)
    {
        if (index is not int i || i < 0 || i >= from.Count) return null;
        if (!imported.TryGetValue(i, out int newIndex))
        {
            newIndex = to.Count;
            to.Add(clone(from[i]));
            imported[i] = newIndex;
        }
        return newIndex;
    }

    /// <summary>Replaces one of the child's textures or bump maps with a copy of one of the donor's, keeping the spheres that show it.</summary>
    private static void SwapImageGene(Genome child, Genome donor, Random rng)
    {
        bool canSwapTexture = child.Textures.Count > 0 && donor.Textures.Count > 0;
        bool canSwapBumpMap = child.BumpMaps.Count > 0 && donor.BumpMaps.Count > 0;
        if (canSwapTexture && (!canSwapBumpMap || rng.Chance(0.6)))
        {
            child.Textures[rng.Next(child.Textures.Count)] = rng.Pick(donor.Textures).DeepClone();
        }
        else if (canSwapBumpMap)
        {
            child.BumpMaps[rng.Next(child.BumpMaps.Count)] = rng.Pick(donor.BumpMaps).DeepClone();
        }
    }
}
