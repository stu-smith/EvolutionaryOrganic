using System.Numerics;
using Morphogen.Core.Development;
using Morphogen.Core.Evolution;
using Morphogen.Core.Genetics;
using Morphogen.Core.Serialization;

namespace Morphogen.Core.Tests;

public class FusionTests
{
    [Fact]
    public void UnfusedSpheresAreNotMeshed()
    {
        var morphology = MorphologyBuilder.Build(Pair(distance: 0.8f, fusion: null));

        Assert.Empty(morphology.Surfaces);
        Assert.Empty(morphology.FusedSpheres);
    }

    [Fact]
    public void FusedSpheresBridgeAGapIntoOneClosedSurface()
    {
        // A gap of 0.1 between the spheres, well within the blend radius of 0.6 × 0.5.
        var morphology = MorphologyBuilder.Build(Pair(distance: 1.1f, fusion: 0.6f));

        var surface = Assert.Single(morphology.Surfaces);
        Assert.Equal([0, 1], surface.Spheres.Order());
        Assert.Equal(2, EulerCharacteristic(surface)); // One closed piece, no holes.
    }

    [Fact]
    public void SpheresTooFarApartToBlendAreNotMeshed()
    {
        var morphology = MorphologyBuilder.Build(Pair(distance: 2f, fusion: 0.6f));

        Assert.Empty(morphology.Surfaces);
    }

    [Fact]
    public void FusedSurfaceKeepsTheSpheresShapeAwayFromTheJoin()
    {
        var morphology = MorphologyBuilder.Build(Pair(distance: 0.8f, fusion: 0.5f));
        var surface = Assert.Single(morphology.Surfaces);
        var spheres = morphology.Spheres;

        // On the far sides the blend has no effect, so the mesh should sit on the spheres themselves.
        var outer = surface.Positions.Where(p => MathF.Abs(p.X) > 0.75f).ToList();
        Assert.NotEmpty(outer);
        foreach (var p in outer)
        {
            float distance = spheres.Min(s => Vector3.Distance(p, s.Position) - s.Radius);
            Assert.InRange(distance, -0.01f, 0.01f);
        }
    }

    [Fact]
    public void TrianglesAndNormalsFaceOutwards()
    {
        var surface = Assert.Single(MorphologyBuilder.Build(Pair(distance: 0.8f, fusion: 0.8f)).Surfaces);
        // The shape is symmetric about the origin and star-shaped, so outward is away from it.
        for (int i = 0; i < surface.Indices.Length; i += 3)
        {
            var a = surface.Positions[surface.Indices[i]];
            var b = surface.Positions[surface.Indices[i + 1]];
            var c = surface.Positions[surface.Indices[i + 2]];
            var centroid = (a + b + c) / 3f;
            Assert.True(Vector3.Dot(Vector3.Cross(b - a, c - a), centroid) > 0f);
        }
        for (int i = 0; i < surface.Positions.Length; i++)
        {
            Assert.True(Vector3.Dot(surface.Normals[i], surface.Positions[i]) > 0f);
        }
    }

    [Fact]
    public void ColoursBlendAcrossTheJoin()
    {
        var genome = Pair(distance: 0.8f, fusion: 0.8f);
        var group = (GroupNode)genome.Root;
        ((SphereNode)group.Items[0]).Color = new HsvColor(0f, 1f, 1f);
        ((SphereNode)group.Items[1]).Color = new HsvColor(240f, 1f, 1f);

        var surface = Assert.Single(MorphologyBuilder.Build(genome).Surfaces);
        var colors = surface.Positions.Zip(surface.Colors).ToList();

        var left = colors.MinBy(pc => pc.First.X).Second;
        var right = colors.MaxBy(pc => pc.First.X).Second;
        var middle = colors.MinBy(pc => MathF.Abs(pc.First.X)).Second;
        AssertClose(new Vector3(1, 0, 0), left, 1e-3f);
        AssertClose(new Vector3(0, 0, 1), right, 1e-3f);
        Assert.InRange(middle.X, 0.3f, 0.7f);
        Assert.InRange(middle.Z, 0.3f, 0.7f);
    }

    [Fact]
    public void FusionIsInheritedAndCanBeSetPerSubtree()
    {
        // Two separate pairs of overlapping spheres: one branch fuses, the other does not.
        var genome = new Genome
        {
            Root = new GroupNode
            {
                Fusion = 0.7f,
                Items =
                [
                    Pair(distance: 0.8f, fusion: null).Root,
                    Offset(Pair(distance: 0.8f, fusion: 0f).Root, new Vector3(0, 4, 0)),
                ],
            },
        };

        var morphology = MorphologyBuilder.Build(genome);

        Assert.Equal([0.7f, 0.7f, 0f, 0f], morphology.Spheres.Select(s => s.Fusion));
        var surface = Assert.Single(morphology.Surfaces);
        Assert.Equal([0, 1], surface.Spheres.Order());
    }

    [Fact]
    public void FusionScalesCompoundAlongARepeat()
    {
        var genome = new Genome
        {
            Root = new LinearRepeatNode
            {
                Count = 4,
                Step = new Transform3 { Translation = new Vector3(0, 0.6f, 0) },
                StepModifier = new PropertyModifier { FusionScale = 0.5f },
                // The child sets its own fusion, and the repeat's gradient still applies beneath it.
                Child = new SphereNode { Radius = 0.4f, Fusion = 0.8f },
            },
        };

        var spheres = MorphologyBuilder.Build(genome).Spheres;

        Assert.Equal([0.8f, 0.4f, 0.2f, 0.1f], spheres.Select(s => s.Fusion));
    }

    [Fact]
    public void FusionIsClampedToItsMaximum()
    {
        var genome = new Genome
        {
            Root = new SphereNode { Fusion = 1.2f, Modifier = new PropertyModifier { FusionScale = 3f } },
        };

        Assert.Equal(GenomeNode.MaxFusion, MorphologyBuilder.Build(genome).Spheres[0].Fusion);
    }

    [Fact]
    public void SmoothMinIsExactMinimumOutsideTheBlend()
    {
        Assert.Equal(0.2f, FusedSurfaceBuilder.SmoothMin(0.2f, 0.9f, 0.5f));
        Assert.True(FusedSurfaceBuilder.SmoothMin(0.2f, 0.3f, 0.5f) < 0.2f);
    }

    [Fact]
    public void FusionGenesRoundTripThroughJson()
    {
        var genome = Pair(distance: 1f, fusion: 0.45f);
        genome.Root.Modifier = new PropertyModifier { FusionScale = 1.25f };

        string json = GenomeSerializer.Serialize(genome);
        var back = GenomeSerializer.Deserialize(json);

        Assert.Equal(json, GenomeSerializer.Serialize(back));
        Assert.Equal(0.45f, back.Root.Fusion);
        Assert.Equal(1.25f, back.Root.Modifier.FusionScale);
        Assert.All(back.Root.Children, c => Assert.Null(c.Fusion));
        // Unset fusion is left out, so unfused genomes read just as before.
        Assert.DoesNotContain("\"fusion\"", GenomeSerializer.Serialize(Pair(distance: 1f, fusion: null)));
    }

    [Fact]
    public void OlderGenomesAreUpgradedToTheCurrentFormat()
    {
        var genome = Pair(distance: 1f, fusion: null);
        genome.FormatVersion = 2;

        var back = GenomeSerializer.Deserialize(GenomeSerializer.Serialize(genome));

        Assert.Equal(Genome.CurrentFormatVersion, back.FormatVersion);
    }

    [Fact]
    public void MutationEvolvesFusion()
    {
        var rng = new Random(7);
        var genome = RandomGenomeFactory.CreateGenome(rng);
        bool sawFusion = false;
        for (int i = 0; i < 300 && !sawFusion; i++)
        {
            genome = Mutator.Mutate(genome, rng);
            sawFusion = genome.Root.DescendantsAndSelf().Any(n => n.Fusion > 0f);
        }
        Assert.True(sawFusion);
    }

    [Fact]
    public void RandomFusedCreationsBuildWithinBudget()
    {
        var limits = MorphologyLimits.Default with { MaxFusionSamples = 400_000 };
        for (int seed = 0; seed < 12; seed++)
        {
            var rng = new Random(seed);
            var genome = RandomGenomeFactory.CreateGenome(rng, maxSpheres: 600);
            genome.Root.Fusion = 0.8f;

            var morphology = MorphologyBuilder.Build(genome, limits);

            foreach (var surface in morphology.Surfaces)
            {
                Assert.Equal(surface.Positions.Length, surface.Normals.Length);
                Assert.Equal(surface.Positions.Length, surface.Colors.Length);
                Assert.Equal(0, surface.Indices.Length % 3);
                Assert.All(surface.Indices, i => Assert.InRange(i, 0, surface.Positions.Length - 1));
                Assert.All(surface.Positions, p => Assert.True(float.IsFinite(p.X + p.Y + p.Z)));
            }
        }
    }

    /// <summary>Two spheres of radius 0.5 either side of the origin along x, under a group with the given fusion.</summary>
    private static Genome Pair(float distance, float? fusion) => new()
    {
        Root = new GroupNode
        {
            Fusion = fusion,
            Items =
            [
                new SphereNode { Radius = 0.5f, Offset = new Transform3 { Translation = new Vector3(-distance / 2, 0, 0) } },
                new SphereNode { Radius = 0.5f, Offset = new Transform3 { Translation = new Vector3(distance / 2, 0, 0) } },
            ],
        },
    };

    private static GenomeNode Offset(GenomeNode node, Vector3 translation)
    {
        node.Offset = new Transform3 { Translation = translation };
        return node;
    }

    private static int EulerCharacteristic(FusedSurface surface)
    {
        var edges = new HashSet<(int, int)>();
        for (int i = 0; i < surface.Indices.Length; i += 3)
        {
            for (int e = 0; e < 3; e++)
            {
                int a = surface.Indices[i + e], b = surface.Indices[i + (e + 1) % 3];
                edges.Add((Math.Min(a, b), Math.Max(a, b)));
            }
        }
        int used = surface.Indices.Distinct().Count();
        return used - edges.Count + surface.Indices.Length / 3;
    }

    private static void AssertClose(Vector3 expected, Vector3 actual, float tolerance)
    {
        Assert.True(Vector3.Distance(expected, actual) < tolerance, $"Expected {expected}, got {actual}.");
    }
}
