using EvolutionaryOrganic.Core.Development;
using EvolutionaryOrganic.Core.Evolution;
using EvolutionaryOrganic.Core.Genetics;
using EvolutionaryOrganic.Core.Genetics.Imaging;
using EvolutionaryOrganic.Core.Serialization;

namespace EvolutionaryOrganic.Core.Tests;

public class EvolutionTests
{
    [Fact]
    public void RandomGenomesHaveReasonableSize()
    {
        for (int seed = 0; seed < 100; seed++)
        {
            var genome = RandomGenomeFactory.CreateGenome(new Random(seed), maxSpheres: 2000);
            long count = MorphologyBuilder.EstimateSphereCount(genome.Root);
            Assert.InRange(count, 1, 2000);
        }
    }

    [Fact]
    public void MutantsDifferFromParent()
    {
        var rng = new Random(11);
        var parent = RandomGenomeFactory.CreateGenome(rng);
        string parentJson = GenomeSerializer.Serialize(parent);
        for (int i = 0; i < 50; i++)
        {
            var child = Mutator.Mutate(parent, rng);
            Assert.NotEqual(parentJson, GenomeSerializer.Serialize(child));
            Assert.Equal(parent.Generation + 1, child.Generation);
        }
        Assert.Equal(parentJson, GenomeSerializer.Serialize(parent));
    }

    [Fact]
    public void MutationIsDeterministicForASeed()
    {
        var parent = RandomGenomeFactory.CreateGenome(new Random(5));
        var a = Mutator.Mutate(parent, new Random(42));
        var b = Mutator.Mutate(parent, new Random(42));
        Assert.Equal(GenomeSerializer.Serialize(a), GenomeSerializer.Serialize(b));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.5f)]
    [InlineData(1f)]
    public void LongLineagesStayWithinLimits(float strength)
    {
        var settings = new MutationSettings
        {
            Strength = strength, StructuralProbability = 0.5f, MaxSpheres = 1500, MaxTreeDepth = 7, MaxTextures = 2, MaxBumpMaps = 1,
        };
        var rng = new Random(1234);
        var genome = RandomGenomeFactory.CreateGenome(rng, settings.MaxSpheres);
        for (int generation = 0; generation < 300; generation++)
        {
            genome = Mutator.Mutate(genome, rng, settings);
            Assert.InRange(MorphologyBuilder.EstimateSphereCount(genome.Root), 1, settings.MaxSpheres);
            Assert.InRange(genome.Root.Depth(), 1, settings.MaxTreeDepth);
            AssertTreeIsSound(genome.Root);
            AssertImagesAreSound(genome, settings);
        }
        Assert.NotEmpty(MorphologyBuilder.Build(genome, new MorphologyLimits { TextureSize = 32 }).Spheres);
    }

    private static void AssertImagesAreSound(Genome genome, MutationSettings settings)
    {
        Assert.InRange(genome.Textures.Count, 0, settings.MaxTextures);
        Assert.InRange(genome.BumpMaps.Count, 0, settings.MaxBumpMaps);
        var spheres = genome.Root.DescendantsAndSelf().OfType<SphereNode>().ToList();
        Assert.All(spheres, s => Assert.True(s.Texture is null || s.Texture < genome.Textures.Count, "Dangling texture reference."));
        Assert.All(spheres, s => Assert.True(s.BumpMap is null || s.BumpMap < genome.BumpMaps.Count, "Dangling bump map reference."));
        for (int i = 0; i < genome.Textures.Count; i++) Assert.Contains(spheres, s => s.Texture == i);
        for (int i = 0; i < genome.BumpMaps.Count; i++) Assert.Contains(spheres, s => s.BumpMap == i);
        foreach (var gene in genome.Textures.Concat<ImageGene>(genome.BumpMaps))
        {
            Assert.InRange(gene.Pattern.DescendantsAndSelf().Count(), 1, ImageGene.MaxNodes);
            Assert.InRange(gene.Pattern.Depth(), 1, ImageGene.MaxDepth);
            var seen = new HashSet<ImageNode>(ReferenceEqualityComparer.Instance);
            foreach (var node in gene.Pattern.DescendantsAndSelf()) Assert.True(seen.Add(node), "An image node appears twice.");
        }
    }

    private static void AssertTreeIsSound(GenomeNode root)
    {
        // Every node must appear exactly once: shared references would make later mutations leak between branches.
        var seen = new HashSet<GenomeNode>(ReferenceEqualityComparer.Instance);
        foreach (var node in root.DescendantsAndSelf()) Assert.True(seen.Add(node), "A node appears twice in the tree.");
    }
}
