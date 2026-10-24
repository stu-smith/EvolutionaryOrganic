using System.Numerics;
using EvolutionaryOrganic.Core.Development;
using EvolutionaryOrganic.Core.Evolution;
using EvolutionaryOrganic.Core.Genetics;
using EvolutionaryOrganic.Core.Genetics.Imaging;
using EvolutionaryOrganic.Core.Serialization;

namespace EvolutionaryOrganic.Core.Tests;

public class ImageTests
{
    private static readonly MorphologyLimits SmallTextures = new() { TextureSize = 32 };

    [Fact]
    public void RandomImagesStayInRange()
    {
        var rng = new Random(9);
        for (int i = 0; i < 200; i++)
        {
            var image = RandomImageFactory.CreateImage(rng, 3);
            for (int j = 0; j < 50; j++)
            {
                var p = Vector3.Normalize(new Vector3(rng.NextSingle() - 0.5f, rng.NextSingle() - 0.5f, rng.NextSingle() - 0.5f));
                Assert.InRange(ImageBaker.Evaluate(image, p), -1e-4f, 1f + 1e-4f);
            }
        }
    }

    [Fact]
    public void PaletteMapsDarkToFirstStopAndLightToLast()
    {
        HsvColor[] palette = [new(0f, 1f, 1f), new(120f, 1f, 1f), new(240f, 1f, 1f)];
        Assert.Equal(new Vector3(1, 0, 0), ImageBaker.SamplePalette(palette, 0f));
        Assert.Equal(new Vector3(0, 1, 0), ImageBaker.SamplePalette(palette, 0.5f));
        Assert.Equal(new Vector3(0, 0, 1), ImageBaker.SamplePalette(palette, 1f));
    }

    [Fact]
    public void FlatBumpMapGivesUnperturbedNormals()
    {
        // Contrast 0 squashes any pattern to a constant mid-grey.
        var gene = new BumpGene { Pattern = new ToneImageNode { Function = ToneFunction.Contrast, Amount = 0f }, Depth = 0.2f };
        var image = ImageBaker.BakeBumpMap(gene, 32);
        Assert.Equal(32, image.Width);
        Assert.Equal(16, image.Height);
        for (int i = 0; i < image.Pixels.Length; i += 4)
        {
            Assert.InRange(image.Pixels[i], 127, 128);
            Assert.InRange(image.Pixels[i + 1], 127, 128);
            Assert.Equal(255, image.Pixels[i + 2]);
        }
    }

    [Fact]
    public void BakingIsDeterministic()
    {
        var gene = RandomImageFactory.CreateTexture(new Random(4));
        var a = ImageBaker.BakeTexture(gene, 32);
        var b = ImageBaker.BakeTexture(gene.DeepClone(), 32);
        Assert.Equal(a.Pixels, b.Pixels);
    }

    [Fact]
    public void MorphologyBakesOnlyTheImagesSpheresShow()
    {
        var genome = new Genome
        {
            Textures = [new TextureGene(), new TextureGene { Pattern = new CellsImageNode() }],
            BumpMaps = [new BumpGene()],
            Root = new GroupNode
            {
                Items =
                [
                    new SphereNode { Texture = 1, BumpMap = 0, Roughness = 0.8f },
                    new SphereNode { Texture = 7 },
                ],
            },
        };

        var morphology = MorphologyBuilder.Build(genome, SmallTextures);

        Assert.Equal((1, 0, 0.8f), (morphology.Spheres[0].Texture, morphology.Spheres[0].BumpMap, morphology.Spheres[0].Roughness));
        Assert.Equal((-1, -1), (morphology.Spheres[1].Texture, morphology.Spheres[1].BumpMap));
        Assert.Null(morphology.Textures[0]);
        Assert.NotNull(morphology.Textures[1]);
        Assert.NotNull(morphology.BumpMaps[0]);
    }

    [Fact]
    public void SpheresInheritTheOrientationOfTheirStructure()
    {
        var genome = new Genome
        {
            Root = new SphereNode { Offset = new Transform3 { Rotation = new Vector3(0f, 90f, 0f) } },
        };

        var orientation = MorphologyBuilder.Build(genome, SmallTextures).Spheres[0].Orientation;

        var expected = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2f);
        Assert.Equal(1f, MathF.Abs(Quaternion.Dot(expected, orientation)), 3);
    }

    [Fact]
    public void ImageGenesRoundTripThroughJson()
    {
        var genome = new Genome
        {
            Textures =
            [
                new TextureGene
                {
                    Pattern = new MaskImageNode
                    {
                        A = new WarpImageNode { Input = new NoiseImageNode { Style = NoiseStyle.Ridged } },
                        B = new ToneImageNode { Input = new CellsImageNode(), Function = ToneFunction.Posterize },
                        Mask = new BlendImageNode { A = new WavesImageNode { Twist = 3 }, B = new TransformImageNode() },
                    },
                },
            ],
            BumpMaps = [new BumpGene { Depth = 0.1f }],
            Root = new SphereNode { Texture = 0, BumpMap = 0 },
        };

        string json = GenomeSerializer.Serialize(genome);
        var back = GenomeSerializer.Deserialize(json);

        Assert.Equal(json, GenomeSerializer.Serialize(back));
        Assert.Contains("\"type\": \"mask\"", json);
        Assert.IsType<MaskImageNode>(back.Textures[0].Pattern);
    }

    [Fact]
    public void PlainSpheresDoNotWriteImageReferences()
    {
        string json = GenomeSerializer.Serialize(new Genome { Root = new SphereNode() });
        Assert.DoesNotContain("\"texture\"", json);
        Assert.DoesNotContain("\"bumpMap\"", json);
    }

    [Fact]
    public void VersionOneGenomesMoveRoughnessOntoSpheres()
    {
        const string json = """
            {
              "formatVersion": 1,
              "generation": 3,
              "surface": { "roughness": 0.8, "metallic": 0.1, "clearCoat": 0.2 },
              "root": { "type": "group", "children": [ { "type": "sphere" }, { "type": "sphere" } ] }
            }
            """;

        var genome = GenomeSerializer.Deserialize(json);

        Assert.Equal(Genome.CurrentFormatVersion, genome.FormatVersion);
        Assert.All(genome.Root.DescendantsAndSelf().OfType<SphereNode>(), s => Assert.Equal(0.8f, s.Roughness));
        Assert.Empty(genome.Textures);
    }

    [Fact]
    public void DeepCloneCopiesImageGenes()
    {
        var genome = new Genome { Textures = [new TextureGene()], BumpMaps = [new BumpGene()], Root = new SphereNode { Texture = 0 } };
        var clone = genome.DeepClone();
        ((NoiseImageNode)clone.Textures[0].Pattern).Frequency = 99f;
        clone.Textures[0].Palette[0] = new HsvColor(1f, 1f, 1f);
        clone.BumpMaps[0].Depth = 99f;

        Assert.NotEqual(99f, ((NoiseImageNode)genome.Textures[0].Pattern).Frequency);
        Assert.NotEqual(new HsvColor(1f, 1f, 1f), genome.Textures[0].Palette[0]);
        Assert.NotEqual(99f, genome.BumpMaps[0].Depth);
    }

    [Fact]
    public void ZeroLimitsStripImagesFromMutants()
    {
        var parent = new Genome
        {
            Textures = [new TextureGene()],
            BumpMaps = [new BumpGene()],
            Root = new LinearRepeatNode { Child = new SphereNode { Texture = 0, BumpMap = 0 } },
        };
        var settings = new MutationSettings { MaxTextures = 0, MaxBumpMaps = 0 };

        var child = Mutator.Mutate(parent, new Random(2), settings);

        Assert.Empty(child.Textures);
        Assert.Empty(child.BumpMaps);
        Assert.All(child.Root.DescendantsAndSelf().OfType<SphereNode>(), s => Assert.Null(s.Texture ?? s.BumpMap));
    }

    [Fact]
    public void LineagesEvolveTexturesAndBumpMaps()
    {
        var settings = new MutationSettings { StructuralProbability = 0.4f, MaxTextures = 2, MaxBumpMaps = 1 };
        var rng = new Random(77);
        var genome = RandomGenomeFactory.CreateGenome(rng, maxTextures: 0, maxBumpMaps: 0);
        int textured = 0, bumpy = 0;
        for (int generation = 0; generation < 300; generation++)
        {
            genome = Mutator.Mutate(genome, rng, settings);
            if (genome.Textures.Count > 0) textured++;
            if (genome.BumpMaps.Count > 0) bumpy++;
        }
        Assert.True(textured > 0, "No texture ever evolved.");
        Assert.True(bumpy > 0, "No bump map ever evolved.");
    }
}
