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
| Theme, mutation strength, quality | Tools → Settings |

## Genome model

A genome (`src/EvolutionaryOrganic.Core/Genetics`) is a tree rather than a linear string:

- **Sphere**: the leaf and the visual building block. It has a radius and an HSV colour.
- **Group**: combines several sub-structures.
- **Linear repeat**: repeats its child *n* times, applying a step transform (move, rotate, uniform scale)
  cumulatively. This gives rows, arcs, spirals and tapering horns.
- **Radial repeat**: repeats its child *n* times around an axis, with a radius, sweep angle and tilt.
  This gives rings, flowers and crowns.

Every node has an **offset** (placement relative to its parent) and a **modifier**, which is a transform on the
*properties* of everything below it (hue shift, saturation, brightness, sphere size). Repeat nodes also have a
**per-step modifier** that compounds with each iteration, which produces colour and size gradients along a horn
or around a ring. A genome also carries whole-creation **surface genes**: roughness, metalness and clear coat.

`Development/MorphologyBuilder` grows a genome into a list of world-space spheres, within a sphere budget.
`Evolution/Mutator` makes offspring with parametric mutations (Gaussian nudges scaled by mutation strength) and
structural mutations (wrap a node in a repeat, unwrap, add or remove group parts, swap repeat kinds, duplicate
subtrees). It then prunes each mutant back within the sphere and depth limits.

## Rendering

Rendering uses [HelixToolkit.Wpf.SharpDX](https://github.com/helix-toolkit/helix-toolkit) (Direct3D 11) inside WPF:

- **Physically based materials** (`PBRMaterial`) with clear coat, and reflections from a procedurally
  generated studio environment cube map (`Rendering/StudioEnvironment.cs`).
- **GPU instancing**: each creation is one draw call with per-instance transforms and colours.
- Shadow mapping, screen-space ambient occlusion and 4× MSAA, each switchable in Settings.
- A three-light studio rig that follows the camera, so creations stay well lit from any angle.
- All nine viewports share a single `EffectsManager` (device, shaders and geometry buffers).

### Ready for textures and bump mapping

The sphere meshes (`Rendering/SphereMeshes.cs`) already carry UVs, tangents and bitangents. `PBRMaterial` supports
albedo, normal, roughness/metallic, ambient-occlusion, emissive and displacement maps, including hardware
tessellation for true displacement. Adding textured or bumpy surfaces means adding genes for them, then setting the
corresponding maps in `CreationView.OnMorphologyChanged`. Per-sphere material variation beyond colour would mean
grouping spheres by material into several instanced models.

## Project layout

```
src/EvolutionaryOrganic.Core     Genome model, development (genome → spheres), mutation, JSON serialization
src/EvolutionaryOrganic.App      WPF app: views, view models, rendering, settings and theming
tests/EvolutionaryOrganic.Core.Tests
```
