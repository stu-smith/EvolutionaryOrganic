using EvolutionaryOrganic.Core.Development;
using EvolutionaryOrganic.Core.Evolution;
using EvolutionaryOrganic.Core.Genetics;
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
        var settings = new MutationSettings { Strength = strength, StructuralProbability = 0.5f, MaxSpheres = 1500, MaxTreeDepth = 7 };
        var rng = new Random(1234);
        var genome = RandomGenomeFactory.CreateGenome(rng, settings.MaxSpheres);
        for (int generation = 0; generation < 300; generation++)
        {
            genome = Mutator.Mutate(genome, rng, settings);
            Assert.InRange(MorphologyBuilder.EstimateSphereCount(genome.Root), 1, settings.MaxSpheres);
            Assert.InRange(genome.Root.Depth(), 1, settings.MaxTreeDepth);
            AssertTreeIsSound(genome.Root);
        }
        Assert.NotEmpty(MorphologyBuilder.Build(genome).Spheres);
    }

    private static void AssertTreeIsSound(GenomeNode root)
    {
        // Every node must appear exactly once: shared references would make later mutations leak between branches.
        var seen = new HashSet<GenomeNode>(ReferenceEqualityComparer.Instance);
        foreach (var node in root.DescendantsAndSelf()) Assert.True(seen.Add(node), "A node appears twice in the tree.");
    }
}
