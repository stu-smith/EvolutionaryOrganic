# Morphogen

Breed 3D organic forms through aesthetic selection, in the spirit of William Latham's *Mutator* and *FormSynth*.

The window shows a 3×3 grid. The **current** creation sits in the centre and is surrounded by eight **mutants** of it.
Click **Select** under a mutant to make it current and breed a new generation around it, or click **Re-roll** to discard
the mutants and breed eight fresh ones. The sidebar on the right shows the current creation's genome tree.

Work is organised into **projects**. Click **Keep** on any of the nine creations to add its genome to the project,
which is listed down the left of the window. Each kept genome has a **Combine** button, which crosses it with the
current creation (sexual reproduction) and fills the eight outer slots with offspring, and a **Select** button, which
makes it the current creation.

## Building

Requires the .NET 10 SDK on Windows with a Direct3D 11 capable GPU.

```
dotnet build Morphogen.sln
dotnet run --project src/Morphogen.App
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
| New / open / save project | File → New project (Ctrl+Shift+N) / Open project (Ctrl+O) / Save project (Ctrl+S), Save as (Ctrl+Shift+S): `.morphogen.json` |
| Keep a creation in the project | Keep button on any of the nine creations |
| Cross a kept genome with the current creation | Combine button in the project list |
| New random creation | File → New random creation (Ctrl+N) |
| Undo a selection | Evolution → Back (Ctrl+Z) |
| Re-roll mutants | Re-roll button, or F5 |
| Import / export a single genome | File → Import genome into project (Ctrl+I) / Export current genome (Ctrl+E): human-readable `.genome.json` |
| Export a high-res image | File → Export image (Ctrl+P): PNG / JPEG / TIFF, up to 16384 px per side, from the current angle |
| Mutation strength, sphere / texture / bump map limits | Evolution pane, bottom right |
| Theme, quality | Tools → Settings |

## Genome model

A genome (`src/Morphogen.Core/Genetics`) is a tree rather than a linear string:

- **Sphere**: the leaf and the visual building block. It has a radius, a roughness (shiny to matte), and either a
  plain HSV colour or a reference to one of the genome's colour textures, never both. It can also refer to a bump map.
- **Group**: combines several sub-structures.
- **Linear repeat**: repeats its child *n* times, applying a step transform (move, rotate, uniform scale)
  cumulatively. This gives rows, arcs, spirals and tapering horns.
- **Radial repeat**: repeats its child *n* times around an axis, with a radius, sweep angle and tilt.
  This gives rings, flowers and crowns.

Every node has an **offset** (placement relative to its parent) and a **modifier**, which is a transform on the
*properties* of everything below it (hue shift, saturation, brightness, sphere size, fusion). Repeat nodes also have a
**per-step modifier** that compounds with each iteration, which produces colour and size gradients along a horn
or around a ring. A genome also carries whole-creation **surface genes**: metalness and clear coat.

### Fusion

Any node can carry a **fusion** gene, which makes the spheres beneath it flow into one another instead of meeting at
hard creases. It is a fraction of each sphere's radius: around 0.2 grows a slight web where spheres meet, around 1
melts them into one body. A node without one inherits its parent's fusion, so different branches of a creation can
fuse to different degrees, and a fusion of 0 keeps a branch crisp inside a fused body. Modifiers also have a
**fusion scale**, so a repeat's per-step modifier can make a horn solid at its base and beaded at its tip.

Each sphere is treated as a signed distance field, and touching spheres are joined with a smooth minimum whose blend
radius is about the smaller of the two spheres' fusion × radius. A sphere on its own keeps exactly its radius. Clusters of
fused spheres are meshed with surface nets (`Development/FusedSurfaceBuilder`), and colour, roughness, textures and
bumps are blended across each join. On fused surfaces, textures are sampled per vertex and bump maps displace the
mesh, so detail finer than the mesh is lost; spheres that do not fuse are drawn exactly as before.

### Textures and bump maps

A genome defines zero or more **textures** and **bump maps** (`Genetics/Imaging`), each a tree of image operations
that spheres refer to by index. Every image node is a colour field over 3D space, sampled on the surface of the
unit sphere, so patterns have no seams and no pinching at the poles:

- **Patterns** (leaves): fractal noise (smooth, turbulent or ridged), cells (spots or edges), and bands or spirals.
  In a texture, each pattern maps its values onto its own gradient of two to four colours, around a hue shared by
  the texture's other patterns (occasionally a contrasting one).
- **Filters**: warp (domain distortion), transform (zoom and rotate), and tone curves (contrast, invert, posterize,
  ridge, contours), which act on each colour channel.
- **Combiners**: blend (mix, add, multiply, difference, lighten, darken), per channel, and mask (choose between two
  images by the brightness of a third).

New textures and bump maps start with two or three levels of operations, up to 20 nodes. A textured sphere shows its
texture exactly as baked: colour modifiers do not tint it. A bump map's patterns have no colours; it treats its image
as a greyscale height field with an evolvable depth. `Development/ImageBaker` bakes them into images (cached by content, since mutants usually
inherit their parent's images), and each sphere's texture and bumps turn with the structure it belongs to.
Mutations tweak image parameters and pattern colours, restructure image trees, add textures and bump maps, and
switch spheres between a plain colour and a texture (a sphere that drops its texture takes on the texture's overall
colour). Textures and bump maps that no sphere shows are dropped, and the totals are capped by the
limits in the Evolution pane.

`Development/MorphologyBuilder` grows a genome into a list of world-space spheres, within a sphere budget.
`Evolution/Mutator` makes offspring with parametric mutations (Gaussian nudges scaled by mutation strength) and
structural mutations (wrap a node in a repeat, unwrap, add or remove group parts, swap repeat kinds, duplicate
subtrees, add textures and bump maps, restructure image trees). It then prunes each mutant back within the sphere,
depth, texture and bump map limits.

`Evolution/Crossover` breeds offspring of two genomes. A copy of one parent (either, at random) has a random subtree
replaced by a copy of a random subtree of the other, bringing along the textures and bump maps the transplanted spheres
show; metalness and clear coat are each inherited from either parent, and sometimes a texture or bump map is swapped
for one of the other parent's. Offspring are pruned to the same limits as mutants, and are always different from both
parents.

A project file (`.morphogen.json`) holds the kept genomes and the current creation, each in the same format as a
`.genome.json` file.

Genome files from before textures (format version 1) still load: their whole-creation roughness is copied onto every
sphere. In files from before colour textures (format version 3 and earlier), each texture's palette is copied onto
every pattern in it and the tint is dropped, so such textures look somewhat different.

## Rendering

Rendering uses [HelixToolkit.Wpf.SharpDX](https://github.com/helix-toolkit/helix-toolkit) (Direct3D 11) inside WPF:

- **Physically based materials** (`PBRMaterial`) with clear coat, and reflections from a procedurally
  generated studio environment cube map (`Rendering/StudioEnvironment.cs`).
- **GPU instancing**: spheres are batched by material (roughness level, texture, bump map), with one draw call per
  batch and per-instance transforms and colours (white for textured spheres, so textures are not tinted).
- Baked textures and normal maps are uploaded with full mip chains (`Rendering/ImageTextures.cs`).
- Shadow mapping, screen-space ambient occlusion and 4× MSAA, each switchable in Settings.
- A three-light studio rig that follows the camera, so creations stay well lit from any angle.
- All nine viewports share a single `EffectsManager` (device, shaders and geometry buffers).

## Project layout

```
src/Morphogen.Core     Genome model, development (genome → spheres), mutation, JSON serialization
src/Morphogen.App      WPF app: views, view models, rendering, settings and theming
tests/Morphogen.Core.Tests
```
