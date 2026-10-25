using System.Numerics;
using EvolutionaryOrganic.Core.Development;
using EvolutionaryOrganic.Core.Evolution;
using EvolutionaryOrganic.Core.Genetics;
using EvolutionaryOrganic.Core.Serialization;

namespace EvolutionaryOrganic.Core.Tests;

public class GenomeTests
{
    [Theory]
    [InlineData(0f, 1f, 1f, 1f, 0f, 0f)]
    [InlineData(120f, 1f, 1f, 0f, 1f, 0f)]
    [InlineData(240f, 1f, 0.5f, 0f, 0f, 0.5f)]
    [InlineData(0f, 0f, 0.25f, 0.25f, 0.25f, 0.25f)]
    public void HsvConvertsToRgb(float h, float s, float v, float r, float g, float b)
    {
        var rgb = new HsvColor(h, s, v).ToRgb();
        Assert.Equal(r, rgb.X, 3);
        Assert.Equal(g, rgb.Y, 3);
        Assert.Equal(b, rgb.Z, 3);
    }

    [Fact]
    public void HsvRoundTripsThroughRgb()
    {
        var original = new HsvColor(200f, 0.6f, 0.8f);
        var back = HsvColor.FromRgb(original.ToRgb());
        Assert.Equal(original.Hue, back.Hue, 2);
        Assert.Equal(original.Saturation, back.Saturation, 3);
        Assert.Equal(original.Value, back.Value, 3);
    }

    [Fact]
    public void HueWrapsAroundTheWheel()
    {
        Assert.Equal(10f, new HsvColor(370f, 1f, 1f).Normalized().Hue, 3);
        Assert.Equal(350f, new HsvColor(-10f, 1f, 1f).Normalized().Hue, 3);
    }

    [Fact]
    public void LinearRepeatPlacesChildrenAlongStep()
    {
        var genome = new Genome
        {
            Root = new LinearRepeatNode
            {
                Count = 4,
                Step = new Transform3 { Translation = new Vector3(0, 1, 0) },
                Child = new SphereNode { Radius = 0.5f },
            },
        };

        var spheres = MorphologyBuilder.Build(genome).Spheres;

        Assert.Equal(4, spheres.Count);
        for (int i = 0; i < 4; i++) AssertClose(new Vector3(0, i, 0), spheres[i].Position);
    }

    [Fact]
    public void LinearRepeatScaleCompoundsIntoRadiusAndSpacing()
    {
        var genome = new Genome
        {
            Root = new LinearRepeatNode
            {
                Count = 3,
                Step = new Transform3 { Translation = new Vector3(1, 0, 0), Scale = 0.5f },
                Child = new SphereNode { Radius = 1f },
            },
        };

        var spheres = MorphologyBuilder.Build(genome).Spheres;

        Assert.Equal([1f, 0.5f, 0.25f], spheres.Select(s => s.Radius));
        AssertClose(new Vector3(0, 0, 0), spheres[0].Position);
        AssertClose(new Vector3(1, 0, 0), spheres[1].Position);
        AssertClose(new Vector3(1.5f, 0, 0), spheres[2].Position);
    }

    [Fact]
    public void RadialRepeatPlacesChildrenOnRing()
    {
        var genome = new Genome
        {
            Root = new RadialRepeatNode { Count = 4, Radius = 2f, Child = new SphereNode() },
        };

        var spheres = MorphologyBuilder.Build(genome).Spheres;

        Assert.Equal(4, spheres.Count);
        Assert.All(spheres, s => Assert.Equal(2f, new Vector2(s.Position.X, s.Position.Z).Length(), 3));
        Assert.All(spheres, s => Assert.Equal(0f, s.Position.Y, 3));
        AssertClose(new Vector3(-2, 0, 0), spheres[2].Position);
    }

    [Fact]
    public void StepModifierCompoundsPerIteration()
    {
        var genome = new Genome
        {
            Root = new LinearRepeatNode
            {
                Count = 3,
                StepModifier = new PropertyModifier { HueShift = 120f, SizeScale = 2f },
                Child = new SphereNode { Radius = 1f, Color = new HsvColor(0f, 1f, 1f) },
            },
        };

        var spheres = MorphologyBuilder.Build(genome).Spheres;

        Assert.Equal([1f, 2f, 4f], spheres.Select(s => s.Radius));
        AssertClose(new Vector3(1, 0, 0), spheres[0].Color);
        AssertClose(new Vector3(0, 1, 0), spheres[1].Color);
        AssertClose(new Vector3(0, 0, 1), spheres[2].Color);
    }

    [Fact]
    public void NestedRepeatsMultiplyAndRespectBudget()
    {
        var genome = new Genome
        {
            Root = new RadialRepeatNode
            {
                Count = 10,
                Child = new LinearRepeatNode { Count = 10, Child = new SphereNode() },
            },
        };

        Assert.Equal(100, MorphologyBuilder.EstimateSphereCount(genome.Root));
        Assert.Equal(100, MorphologyBuilder.Build(genome).Spheres.Count);

        var limited = MorphologyBuilder.Build(genome, new MorphologyLimits { MaxSpheres = 30 });
        Assert.Equal(30, limited.Spheres.Count);
        Assert.True(limited.Truncated);
    }

    [Fact]
    public void BoundsEncloseAllSpheres()
    {
        var genome = RandomGenomeFactory.CreateGenome(new Random(7));
        var morphology = MorphologyBuilder.Build(genome);
        Assert.All(morphology.Spheres, s =>
            Assert.True(Vector3.Distance(s.Position, morphology.Center) + s.Radius <= morphology.BoundingRadius + 1e-3f));
    }

    [Fact]
    public void SerializationRoundTrips()
    {
        for (int seed = 0; seed < 25; seed++)
        {
            var genome = RandomGenomeFactory.CreateGenome(new Random(seed));
            string json = GenomeSerializer.Serialize(genome);
            var back = GenomeSerializer.Deserialize(json);
            Assert.Equal(json, GenomeSerializer.Serialize(back));
        }
    }

    [Fact]
    public void SerializedJsonIsReadable()
    {
        var genome = new Genome { Root = new RadialRepeatNode { Child = new SphereNode() } };
        string json = GenomeSerializer.Serialize(genome);
        Assert.Contains("\"type\": \"radialRepeat\"", json);
        Assert.Contains("\"type\": \"sphere\"", json);
    }

    [Fact]
    public void RejectsNewerFormatVersions()
    {
        string json = GenomeSerializer.Serialize(new Genome { FormatVersion = 99 });
        Assert.Throws<InvalidDataException>(() => GenomeSerializer.Deserialize(json));
    }

    [Fact]
    public void ProjectSerializationRoundTrips()
    {
        var rng = new Random(8);
        var project = new Project
        {
            Genomes = [RandomGenomeFactory.CreateGenome(rng), RandomGenomeFactory.CreateGenome(rng)],
            Current = RandomGenomeFactory.CreateGenome(rng),
        };
        string json = GenomeSerializer.SerializeProject(project);
        var back = GenomeSerializer.DeserializeProject(json);
        Assert.Equal(2, back.Genomes.Count);
        Assert.NotNull(back.Current);
        Assert.Equal(json, GenomeSerializer.SerializeProject(back));
    }

    [Fact]
    public void ProjectWithoutCurrentRoundTrips()
    {
        var back = GenomeSerializer.DeserializeProject(GenomeSerializer.SerializeProject(new Project()));
        Assert.Empty(back.Genomes);
        Assert.Null(back.Current);
    }

    [Fact]
    public void RejectsFilesThatAreNotProjects()
    {
        string genomeJson = GenomeSerializer.Serialize(new Genome());
        Assert.Throws<InvalidDataException>(() => GenomeSerializer.DeserializeProject(genomeJson));
        string newer = GenomeSerializer.SerializeProject(new Project { FormatVersion = 99 });
        Assert.Throws<InvalidDataException>(() => GenomeSerializer.DeserializeProject(newer));
    }

    [Fact]
    public void ReplaceWorksForRootAndNestedNodes()
    {
        var leaf = new SphereNode();
        var genome = new Genome { Root = new GroupNode { Items = [new LinearRepeatNode { Child = leaf }] } };
        var replacement = new SphereNode { Radius = 2f };

        Assert.True(genome.Replace(leaf, replacement));
        Assert.Same(replacement, ((LinearRepeatNode)((GroupNode)genome.Root).Items[0]).Child);

        var newRoot = new SphereNode();
        Assert.True(genome.Replace(genome.Root, newRoot));
        Assert.Same(newRoot, genome.Root);
    }

    [Fact]
    public void DeepCloneIsIndependent()
    {
        var genome = RandomGenomeFactory.CreateGenome(new Random(3));
        var clone = genome.DeepClone();
        foreach (var sphere in clone.Root.DescendantsAndSelf().OfType<SphereNode>()) sphere.Radius = 99f;
        Assert.DoesNotContain(genome.Root.DescendantsAndSelf().OfType<SphereNode>(), s => s.Radius == 99f);
    }

    private static void AssertClose(Vector3 expected, Vector3 actual, float tolerance = 1e-3f) =>
        Assert.True(Vector3.Distance(expected, actual) < tolerance, $"Expected {expected} but was {actual}");
}
