# Evolutionary Organic

Breed 3D organic forms through aesthetic selection, in the spirit of William Latham's *Mutator* and *FormSynth*.

The window shows a 3×3 grid. The **current** creation sits in the centre and is surrounded by eight **mutants** of it.
Click **Select** under a mutant to make it current and breed a new generation around it, or click **Re-roll** to discard
the mutants and breed eight fresh ones. The sidebar shows the current creation's genome tree.

## Building

Requires the .NET 10 SDK on Windows with a Direct3D 11 capable GPU.

```
dotnet build EvolutionaryOrganic.sln
dotnet run --project src/EvolutionaryOrganic.App
dotnet test
```

The solution can also be opened in Visual Studio 2022 17.14+ / Visual Studio 2026.

## Using it

| Action | How |
| --- | --- |
| Rotate a creation | Left-drag |
| Zoom | Mouse wheel, or right-drag |
| Pan | Middle-drag, or Shift + left-drag |
| Reset a view | Double-click it |
| New random creation | File → New (Ctrl+N) |
| Undo a selection | Evolution → Back (Ctrl+Z) |
| Re-roll mutants | Re-roll button, or F5 |
| Import / export genome | File → Import (Ctrl+O) / Export (Ctrl+S): human-readable `.genome.json` |
| Export a high-res image | File → Export image (Ctrl+P): PNG / JPEG / TIFF, up to 16384 px per side, from the current angle |
| Mutation strength, sphere / texture / bump map limits | Evolution pane, bottom right |
| Theme, quality | Tools → Settings |

## Genome model

A genome (`src/EvolutionaryOrganic.Core/Genetics`) is a tree rather than a linear string:

- **Sphere**: the leaf and the visual building block. It has a radius, a roughness (shiny to matte), and either a
  plain HSV colour or a reference to one of the genome's textures plus a tint. It can also refer to a bump map.
- **Group**: combines several sub-structures.
- **Linear repeat**: repeats its child *n* times, applying a step transform (move, rotate, uniform scale)
  cumulatively. This gives rows, arcs, spirals and tapering horns.
- **Radial repeat**: repeats its child *n* times around an axis, with a radius, sweep angle and tilt.
  This gives rings, flowers and crowns.

Every node has an **offset** (placement relative to its parent) and a **modifier**, which is a transform on the
*properties* of everything below it (hue shift, saturation, brightness, sphere size). Repeat nodes also have a
**per-step modifier** that compounds with each iteration, which produces colour and size gradients along a horn
or around a ring. A genome also carries whole-creation **surface genes**: metalness and clear coat.

### Textures and bump maps

A genome defines zero or more **textures** and **bump maps** (`Genetics/Imaging`), each a tree of image operations
that spheres refer to by index. Every image node is a greyscale field over 3D space, sampled on the surface of the
unit sphere, so patterns have no seams and no pinching at the poles:

- **Patterns** (leaves): fractal noise (smooth, turbulent or ridged), cells (spots or edges), and bands or spirals.
- **Filters**: warp (domain distortion), transform (zoom and rotate), and tone curves (contrast, invert, posterize,
  ridge, contours).
- **Combiners**: blend (mix, add, multiply, difference, lighten, darken) and mask (choose between two images by a third).

A texture maps its pattern onto a gradient of two to four colours; a bump map treats its pattern as a height field
with an evolvable depth. `Development/ImageBaker` bakes them into images (cached by content, since mutants usually
inherit their parent's images), and each sphere's texture and bumps turn with the structure it belongs to.
Mutations tweak image parameters, restructure image trees, add textures and bump maps, and switch spheres between a
plain colour and a texture. Textures and bump maps that no sphere shows are dropped, and the totals are capped by the
limits in the Evolution pane.

`Development/MorphologyBuilder` grows a genome into a list of world-space spheres, within a sphere budget.
`Evolution/Mutator` makes offspring with parametric mutations (Gaussian nudges scaled by mutation strength) and
structural mutations (wrap a node in a repeat, unwrap, add or remove group parts, swap repeat kinds, duplicate
subtrees, add textures and bump maps, restructure image trees). It then prunes each mutant back within the sphere,
depth, texture and bump map limits.

Genome files from before textures (format version 1) still load: their whole-creation roughness is copied onto every
sphere.

## Rendering

Rendering uses [HelixToolkit.Wpf.SharpDX](https://github.com/helix-toolkit/helix-toolkit) (Direct3D 11) inside WPF:

- **Physically based materials** (`PBRMaterial`) with clear coat, and reflections from a procedurally
  generated studio environment cube map (`Rendering/StudioEnvironment.cs`).
- **GPU instancing**: spheres are batched by material (roughness level, texture, bump map), with one draw call per
  batch and per-instance transforms and colours (the colour tints the texture).
- Baked textures and normal maps are uploaded with full mip chains (`Rendering/ImageTextures.cs`).
- Shadow mapping, screen-space ambient occlusion and 4× MSAA, each switchable in Settings.
- A three-light studio rig that follows the camera, so creations stay well lit from any angle.
- All nine viewports share a single `EffectsManager` (device, shaders and geometry buffers).

## Project layout

```
src/EvolutionaryOrganic.Core     Genome model, development (genome → spheres), mutation, JSON serialization
src/EvolutionaryOrganic.App      WPF app: views, view models, rendering, settings and theming
tests/EvolutionaryOrganic.Core.Tests
```
