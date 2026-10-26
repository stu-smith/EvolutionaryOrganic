using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using EvolutionaryOrganic.App.Rendering;
using EvolutionaryOrganic.Core.Development;
using EvolutionaryOrganic.Core.Genetics;
using HelixToolkit;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using SharpDX.Direct3D11;
using MeshGeometry3D = HelixToolkit.SharpDX.MeshGeometry3D;
using PBRMaterial = HelixToolkit.Wpf.SharpDX.PBRMaterial;

namespace EvolutionaryOrganic.App.Views;

/// <summary>
/// Renders one creation in a Direct3D 11 viewport with physically based materials,
/// shadows, ambient occlusion and environment reflections. Drag to rotate, wheel to zoom,
/// double-click to reset the view.
/// </summary>
public partial class CreationView : UserControl
{
    public static readonly DependencyProperty MorphologyProperty = DependencyProperty.Register(
        nameof(Morphology), typeof(Morphology), typeof(CreationView),
        new PropertyMetadata(null, (d, _) => ((CreationView)d).OnMorphologyChanged()));

    private const double FieldOfView = 36;
    private const double Azimuth = 35 * Math.PI / 180;
    private const double Elevation = 18 * Math.PI / 180;

    /// <summary>
    /// Roughness is a material constant in the PBR shader, not a per-instance value, so spheres are grouped
    /// into this many roughness levels, each drawn as its own instanced model. The steps are too fine to notice.
    /// </summary>
    private const int RoughnessLevels = 8;

    /// <summary>Normalised sphere centres and radii, kept to frame the camera tightly.</summary>
    private Vector4[] _framing = [];

    public CreationView()
    {
        InitializeComponent();
        Environment.Texture = StudioEnvironment.CreateTexture();
        Camera.FieldOfView = FieldOfView;
        ResetCamera(animate: false);
        SizeChanged += (_, e) =>
        {
            if (e.PreviousSize.Width == 0 || e.PreviousSize.Height == 0) ResetCamera(animate: false);
        };
    }

    public Morphology? Morphology
    {
        get => (Morphology?)GetValue(MorphologyProperty);
        set => SetValue(MorphologyProperty, value);
    }

    /// <summary>Restores the default three-quarter view, framing the creation snugly.</summary>
    public void ResetCamera(bool animate = true)
    {
        var toCamera = new Vector3D(
            Math.Cos(Elevation) * Math.Sin(Azimuth),
            Math.Sin(Elevation),
            Math.Cos(Elevation) * Math.Cos(Azimuth));
        double distance = FitDistance(toCamera);
        var position = (Point3D)(toCamera * distance);
        var look = -toCamera * distance;
        if (animate && IsLoaded)
        {
            Viewport.SetView(position, look, new Vector3D(0, 1, 0), 300);
        }
        else
        {
            Camera.Position = position;
            Camera.LookDirection = look;
            Camera.UpDirection = new Vector3D(0, 1, 0);
        }
        AimLights();
    }

    /// <summary>Renders the current view, from the current camera angle, at the given resolution.</summary>
    public BitmapSource RenderImage(int width, int height) =>
        Viewport.RenderBitmap(width, height) ?? throw new InvalidOperationException("The renderer did not produce an image.");

    private void OnMorphologyChanged()
    {
        SpheresGroup.Children.Clear();
        var morphology = Morphology;
        if (morphology is null || morphology.Spheres.Count == 0)
        {
            _framing = [];
            return;
        }

        var textures = morphology.Textures.Select(t => t is null ? null : ImageTextures.Create(t)).ToArray();
        var bumpMaps = morphology.BumpMaps.Select(b => b is null ? null : ImageTextures.Create(b)).ToArray();

        // Normalise into a unit sphere about the origin so every creation fills its view.
        float scale = 1f / morphology.BoundingRadius;
        var center = morphology.Center;
        var spheres = morphology.Spheres;
        var framing = new List<Vector4>(spheres.Count);
        var batches = new Dictionary<MaterialKey, (List<Matrix4x4> Matrices, List<InstanceParameter> Parameters)>();
        for (int i = 0; i < spheres.Count; i++)
        {
            var s = spheres[i];
            var position = (s.Position - center) * scale;
            framing.Add(new Vector4(position, s.Radius * scale));
            // Fused spheres are drawn as part of their surface below.
            if (morphology.FusedSpheres.Contains(i)) continue;

            var key = new MaterialKey((int)MathF.Round(s.Roughness * (RoughnessLevels - 1)), s.Texture, s.BumpMap);
            if (!batches.TryGetValue(key, out var batch))
            {
                batch = ([], []);
                batches.Add(key, batch);
            }
            // The rotation turns the sphere's texture and bumps along with the structure it belongs to.
            batch.Matrices.Add(Matrix4x4.CreateScale(s.Radius * scale)
                               * Matrix4x4.CreateFromQuaternion(s.Orientation)
                               * Matrix4x4.CreateTranslation(position));
            batch.Parameters.Add(new InstanceParameter
            {
                DiffuseColor = new Color4(s.Color.X, s.Color.Y, s.Color.Z, 1f),
                EmissiveColor = new Color4(0f, 0f, 0f, 0f),
            });
        }

        var geometry = SphereMeshes.ForSphereCount(spheres.Count - morphology.FusedSpheres.Count);
        foreach (var (key, batch) in batches)
        {
            SpheresGroup.Children.Add(new InstancingMeshGeometryModel3D
            {
                Geometry = geometry,
                Instances = batch.Matrices.ToArray(),
                InstanceParamArray = batch.Parameters.ToArray(),
                Material = CreateMaterial(morphology.Surface, key, textures, bumpMaps),
                IsThrowingShadow = true,
                CullMode = CullMode.Back,
            });
        }

        foreach (var surface in morphology.Surfaces)
        {
            foreach (var model in CreateSurfaceModels(surface, morphology.Surface, center, scale)) SpheresGroup.Children.Add(model);
            // Bumps can raise a surface beyond its spheres; a sample of its vertices is plenty to frame it.
            for (int i = 0; i < surface.Positions.Length; i += 16) framing.Add(new Vector4((surface.Positions[i] - center) * scale, 0f));
        }
        _framing = [.. framing];
        ResetCamera(animate: false);
    }

    /// <summary>
    /// Builds a fused surface as one model per roughness level, like the sphere batches, since roughness is a
    /// material constant. Each triangle takes the level of its vertices' mean roughness; colour comes from the vertices.
    /// </summary>
    private static IEnumerable<MeshGeometryModel3D> CreateSurfaceModels(FusedSurface surface, SurfaceGenes genes, Vector3 center, float scale)
    {
        var positions = new Vector3Collection(surface.Positions.Select(p => (p - center) * scale));
        var normals = new Vector3Collection(surface.Normals);
        var colors = new Color4Collection(surface.Colors.Select(c => new Color4(c.X, c.Y, c.Z, 1f)));

        var levels = new Dictionary<int, IntCollection>();
        var indices = surface.Indices;
        for (int i = 0; i < indices.Length; i += 3)
        {
            float roughness = (surface.Roughness[indices[i]] + surface.Roughness[indices[i + 1]] + surface.Roughness[indices[i + 2]]) / 3f;
            int level = (int)MathF.Round(Math.Clamp(roughness, 0f, 1f) * (RoughnessLevels - 1));
            if (!levels.TryGetValue(level, out var triangles)) levels.Add(level, triangles = []);
            triangles.Add(indices[i]);
            triangles.Add(indices[i + 1]);
            triangles.Add(indices[i + 2]);
        }

        foreach (var (level, triangles) in levels)
        {
            var material = CreateMaterial(genes, new MaterialKey(level, -1, -1), [], []);
            material.VertexColorBlendingFactor = 1;
            yield return new MeshGeometryModel3D
            {
                Geometry = new MeshGeometry3D { Positions = positions, Normals = normals, Colors = colors, Indices = triangles },
                Material = material,
                IsThrowingShadow = true,
                CullMode = CullMode.Back,
            };
        }
    }

    private static PBRMaterial CreateMaterial(SurfaceGenes surface, MaterialKey key, TextureModel?[] textures, TextureModel?[] bumpMaps) => new()
    {
        // The instance colour multiplies the albedo map; it is white for textured spheres, so textures show untinted.
        AlbedoColor = new Color4(1f, 1f, 1f, 1f),
        AlbedoMap = key.Texture >= 0 ? textures[key.Texture] : null,
        NormalMap = key.BumpMap >= 0 ? bumpMaps[key.BumpMap] : null,
        RoughnessFactor = Math.Max(0.04, (double)key.RoughnessLevel / (RoughnessLevels - 1)),
        MetallicFactor = surface.Metallic,
        ClearCoatStrength = surface.ClearCoat,
        ReflectanceFactor = 0.5,
        AmbientOcclusionFactor = 1,
        ClearCoatRoughness = 0.08,
        RenderEnvironmentMap = true,
        RenderShadowMap = true,
    };

    private readonly record struct MaterialKey(int RoughnessLevel, int Texture, int BumpMap);

    /// <summary>
    /// The camera distance, looking at the origin from direction <paramref name="toCamera"/>, at which
    /// every sphere fits inside the view with a small margin.
    /// </summary>
    private double FitDistance(Vector3D toCamera)
    {
        const double fallback = 3.4, margin = 1.08;
        if (_framing.Length == 0) return fallback;

        double aspect = ActualWidth > 0 && ActualHeight > 0 ? ActualWidth / ActualHeight : 4.0 / 3.0;
        // Helix's FieldOfView is horizontal.
        double tanH = Math.Tan(FieldOfView * Math.PI / 360);
        double tanV = tanH / aspect;

        var back = toCamera;
        back.Normalize();
        var right = Vector3D.CrossProduct(new Vector3D(0, 1, 0), back);
        right.Normalize();
        var up = Vector3D.CrossProduct(back, right);

        double distance = 0;
        foreach (var f in _framing)
        {
            var p = new Vector3D(f.X, f.Y, f.Z);
            double x = Math.Abs(Vector3D.DotProduct(p, right)) + f.W;
            double y = Math.Abs(Vector3D.DotProduct(p, up)) + f.W;
            double z = Vector3D.DotProduct(p, back);
            distance = Math.Max(distance, z + Math.Max(x / tanH, y / tanV));
        }
        return Math.Clamp(distance * margin, 0.5, 20);
    }

    private void OnCameraChanged(object? sender, RoutedEventArgs e) => AimLights();

    private void OnViewportDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ResetCamera();
        e.Handled = true;
    }

    /// <summary>Keeps the key, fill and rim lights fixed relative to the viewer.</summary>
    private void AimLights()
    {
        var look = Camera.LookDirection;
        if (look.LengthSquared < 1e-9) return;
        look.Normalize();
        var right = Vector3D.CrossProduct(look, Camera.UpDirection);
        if (right.LengthSquared < 1e-9) right = new Vector3D(1, 0, 0);
        right.Normalize();
        var up = Vector3D.CrossProduct(right, look);

        // Each vector points from the subject towards the light; the light travels the opposite way.
        KeyLight.Direction = -(-0.55 * right + 0.75 * up - 0.45 * look);
        FillLight.Direction = -(0.9 * right + 0.1 * up - 0.4 * look);
        RimLight.Direction = -(0.25 * right + 0.55 * up + 0.8 * look);
    }
}
