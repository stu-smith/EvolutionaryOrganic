using System.Numerics;
using Morphogen.Core.Development;
using Morphogen.Core.Evolution;
using Morphogen.Core.Genetics;
using Morphogen.Core.Genetics.Imaging;
using Morphogen.Core.Serialization;

namespace Morphogen.Core.Tests;

public class ImageTests
{
    private static readonly MorphologyLimits SmallTextures = new() { TextureSize = 32 };

    [Fact]
    public void RandomImagesStayInRange()
    {
        var rng = new Random(9);
        for (int i = 0; i < 200; i++)
        {
            var image = RandomImageFactory.CreateImage(rng, 3, rng.NextSingle() * 360f);
            for (int j = 0; j < 50; j++)
            {
                var p = Vector3.Normalize(new Vector3(rng.NextSingle() - 0.5f, rng.NextSingle() - 0.5f, rng.NextSingle() - 0.5f));
                Assert.InRange(ImageBaker.Evaluate(image, p), -1e-4f, 1f + 1e-4f);
                var color = ImageBaker.EvaluateColor(image, p);
                Assert.InRange(MathF.Min(color.X, MathF.Min(color.Y, color.Z)), -1e-4f, 1f);
                Assert.InRange(MathF.Max(color.X, MathF.Max(color.Y, color.Z)), 0f, 1f + 1e-4f);
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
    public void LeavesColourTheirOwnPatterns()
    {
        HsvColor red = new(0f, 1f, 1f), blue = new(240f, 1f, 1f);
        var image = new MaskImageNode
        {
            // Bands across the equator: the mask is dark at one pole and light at the other.
            Mask = new WavesImageNode { Axis = Vector3.UnitY, Frequency = 0.5f, Palette = [new(0f, 0f, 0f), new(0f, 0f, 1f)] },
            A = new CellsImageNode { Palette = [red, red] },
            B = new CellsImageNode { Palette = [blue, blue] },
            Sharpness = 20f,
        };

        AssertClose(new Vector3(1, 0, 0), ImageBaker.EvaluateColor(image, -Vector3.UnitY));
        AssertClose(new Vector3(0, 0, 1), ImageBaker.EvaluateColor(image, Vector3.UnitY));
        // In greyscale, as for a bump map, the palettes are ignored.
        float grey = ImageBaker.Evaluate(image, Vector3.UnitY);
        Assert.Equal(new Vector3(grey), ImageBaker.EvaluateColor(new MaskImageNode
        {
            Mask = new WavesImageNode { Axis = Vector3.UnitY, Frequency = 0.5f },
            A = new CellsImageNode(),
            B = new CellsImageNode(),
            Sharpness = 20f,
        }, Vector3.UnitY));
    }

    [Fact]
    public void RandomTexturesAreColouredAndHaveOperations()
    {
        var rng = new Random(12);
        for (int i = 0; i < 100; i++)
        {
            var texture = RandomImageFactory.CreateTexture(rng);
            Assert.NotEmpty(texture.Pattern.Inputs);
            Assert.InRange(texture.Pattern.DescendantsAndSelf().Count(), 2, ImageGene.MaxNodes);
            Assert.All(texture.Pattern.DescendantsAndSelf().OfType<PatternImageNode>(), leaf => Assert.InRange(leaf.Palette!.Count, 2, PatternImageNode.MaxStops));

            var bumps = RandomImageFactory.CreateBumpMap(rng);
            Assert.NotEmpty(bumps.Pattern.Inputs);
            Assert.All(bumps.Pattern.DescendantsAndSelf().OfType<PatternImageNode>(), leaf => Assert.Null(leaf.Palette));
        }
    }

    [Fact]
    public void TexturedSpheresAreNotTinted()
    {
        var genome = new Genome
        {
            Textures = [RandomImageFactory.CreateTexture(new Random(3))],
            Root = new GroupNode
            {
                Modifier = new PropertyModifier { HueShift = 90f, ValueScale = 0.5f },
                Items = [new SphereNode { Texture = 0, Color = null }, new SphereNode { Color = new HsvColor(0f, 1f, 1f) }],
            },
        };

        var spheres = MorphologyBuilder.Build(genome, SmallTextures).Spheres;

        Assert.Equal(Vector3.One, spheres[0].Color);
        AssertClose(new Vector3(0.25f, 0.5f, 0f), spheres[1].Color);
    }

    [Fact]
    public void TexturedSpheresHaveNoColour()
    {
        var genome = new Genome { Textures = [new TextureGene()], Root = new SphereNode { Texture = 0 } };

        var back = GenomeSerializer.Deserialize(GenomeSerializer.Serialize(genome));

        Assert.Null(((SphereNode)back.Root).Color);
        Assert.DoesNotContain("\"color\"", GenomeSerializer.Serialize(back));
    }

    [Fact]
    public void VersionThreeTexturesMoveTheirPaletteOntoLeavesAndLoseTheirTint()
    {
        const string json = """
            {
              "formatVersion": 3,
              "textures": [
                {
                  "pattern": { "type": "blend", "a": { "type": "noise" }, "b": { "type": "warp", "input": { "type": "cells" } } },
                  "palette": [ { "hue": 10, "saturation": 0.5, "value": 0.2 }, { "hue": 50, "saturation": 0.3, "value": 1 } ]
                }
              ],
              "root": {
                "type": "group",
                "children": [
                  { "type": "sphere", "texture": 0, "color": { "hue": 200, "saturation": 0.2, "value": 0.9 } },
                  { "type": "sphere", "color": { "hue": 120, "saturation": 0.5, "value": 0.5 } }
                ]
              }
            }
            """;

        var genome = GenomeSerializer.Deserialize(json);

        var leaves = genome.Textures[0].Pattern.DescendantsAndSelf().OfType<PatternImageNode>().ToList();
        Assert.Equal(2, leaves.Count);
        Assert.All(leaves, leaf => Assert.Equal([new(10f, 0.5f, 0.2f), new(50f, 0.3f, 1f)], leaf.Palette!));
        var spheres = genome.Root.DescendantsAndSelf().OfType<SphereNode>().ToList();
        Assert.Null(spheres[0].Color);
        Assert.Equal(new HsvColor(120f, 0.5f, 0.5f), spheres[1].Color);
    }

    [Fact]
    public void SpheresThatLoseTheirTextureTakeItsColour()
    {
        var texture = new TextureGene { Pattern = new NoiseImageNode { Palette = [new(240f, 1f, 1f), new(240f, 1f, 1f)] } };
        var parent = new Genome { Textures = [texture], Root = new LinearRepeatNode { Child = new SphereNode { Texture = 0, Color = null } } };

        var child = Mutator.Mutate(parent, new Random(2), new MutationSettings { MaxTextures = 0 });

        Assert.All(child.Root.DescendantsAndSelf().OfType<SphereNode>(), s => Assert.Equal(240f, s.Color!.Value.Hue, 1f));
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
            Root = new SphereNode { Texture = 0, Color = null, BumpMap = 0 },
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
        var genome = new Genome
        {
            Textures = [new TextureGene { Pattern = new NoiseImageNode { Palette = [new(30f, 0.5f, 0.5f), new(60f, 0.5f, 1f)] } }],
            BumpMaps = [new BumpGene()],
            Root = new SphereNode { Texture = 0 },
        };
        var clone = genome.DeepClone();
        ((NoiseImageNode)clone.Textures[0].Pattern).Frequency = 99f;
        ((PatternImageNode)clone.Textures[0].Pattern).Palette![0] = new HsvColor(1f, 1f, 1f);
        clone.BumpMaps[0].Depth = 99f;

        Assert.NotEqual(99f, ((NoiseImageNode)genome.Textures[0].Pattern).Frequency);
        Assert.NotEqual(new HsvColor(1f, 1f, 1f), ((PatternImageNode)genome.Textures[0].Pattern).Palette![0]);
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

    [Fact]
    public void TextureTreesGrowUnderMutation()
    {
        var rng = new Random(5);
        var genome = new Genome
        {
            Textures = [new TextureGene { Pattern = RandomImageFactory.CreatePattern(rng, 0f) }],
            Root = new LinearRepeatNode { Child = new SphereNode { Texture = 0, Color = null } },
        };
        int largest = 1;
        for (int generation = 0; generation < 200; generation++)
        {
            genome = Mutator.Mutate(genome, rng);
            if (genome.Textures.Count > 0) largest = Math.Max(largest, genome.Textures[0].Pattern.DescendantsAndSelf().Count());
        }
        Assert.True(largest >= 4, $"The texture never grew beyond {largest} nodes.");
    }

    private static void AssertClose(Vector3 expected, Vector3 actual) =>
        Assert.True(Vector3.Distance(expected, actual) < 1e-3f, $"Expected {expected}, got {actual}.");
}
