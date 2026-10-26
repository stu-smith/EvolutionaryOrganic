using System.Numerics;
using EvolutionaryOrganic.Core.Genetics.Imaging;

namespace EvolutionaryOrganic.Core.Development;

/// <summary>
/// A triangle mesh in world space for a cluster of spheres that flow into one another. Colour is sRGB in [0, 1].
/// Triangles wind counter-clockwise seen from outside, as the sphere meshes do. <see cref="Spheres"/> indexes the
/// morphology's spheres that the mesh replaces.
/// </summary>
public sealed record FusedSurface(
    Vector3[] Positions,
    Vector3[] Normals,
    Vector3[] Colors,
    float[] Roughness,
    int[] Indices,
    int[] Spheres);

/// <summary>
/// Melts fused spheres together. Each sphere is a signed distance field, and neighbours are combined with a
/// polynomial smooth minimum whose blend radius is about the smaller of the two spheres' fusion × radius. A sphere on
/// its own keeps exactly its radius, and with no fusion the result is the plain union. Clusters of spheres that
/// actually touch are polygonised with surface nets; every other sphere is left to be drawn as a sphere.
/// Colour, roughness, textures and bumps are blended across each join with the same weights as the shape.
/// </summary>
public static class FusedSurfaceBuilder
{
    /// <summary>Fusion below this is treated as none, rather than meshing spheres that would look the same.</summary>
    public const float MinFusion = 0.02f;

    /// <summary>Spheres smaller than this many grid cells are too coarse to mesh, and are drawn as spheres instead.</summary>
    private const float MinRadiusInCells = 1.5f;

    /// <summary>Cells per side of the blocks that bin spheres for field evaluation.</summary>
    private const int BlockSize = 8;

    /// <summary>Field value of grid corners that no sphere is near: comfortably outside.</summary>
    private const float Outside = float.MaxValue;

    public static IReadOnlyList<FusedSurface> Build(
        IReadOnlyList<SphereInstance> spheres,
        IReadOnlyList<BakedImage?> textures,
        IReadOnlyList<BumpGene> bumpMaps,
        MorphologyLimits limits)
    {
        var clusters = FindClusters(spheres);
        if (clusters.Count == 0) return [];

        // Cells are sized against the whole creation, finer for small clusters, then coarsened together to fit the budget.
        var (min, max) = Bounds(spheres, Enumerable.Range(0, spheres.Count), 0f);
        float diameter = MathF.Max((max - min).Length(), 1e-4f);
        var plans = clusters.Select(c =>
        {
            var (cmin, cmax) = Bounds(spheres, c, 0f);
            float extent = MathF.Max(MathF.Max(cmax.X - cmin.X, cmax.Y - cmin.Y), cmax.Z - cmin.Z);
            return (Cluster: c, Cell: MathF.Min(diameter / limits.FusionResolution, MathF.Max(extent, 1e-4f) / 40f));
        }).ToList();

        float coarsen = 1f;
        for (int guard = 0; guard < 8; guard++)
        {
            double samples = plans.Sum(p => (double)Grid.For(spheres, p.Cluster, p.Cell * coarsen).CornerCount);
            if (samples <= limits.MaxFusionSamples) break;
            coarsen *= MathF.Max(1.05f, MathF.Cbrt((float)(samples / limits.MaxFusionSamples)));
        }

        var surfaces = new List<FusedSurface>();
        foreach (var (cluster, baseCell) in plans)
        {
            float cell = baseCell * coarsen;
            var members = cluster.Where(i => spheres[i].Radius >= cell * MinRadiusInCells).ToArray();
            if (members.Length < 2) continue;
            var surface = Polygonise(spheres, members, cell, textures, bumpMaps);
            if (surface is not null) surfaces.Add(surface);
        }
        return surfaces;
    }

    /// <summary>The blend radius of a sphere in world units.</summary>
    private static float Blend(in SphereInstance s) => s.Fusion >= MinFusion ? s.Fusion * s.Radius : 0f;

    /// <summary>
    /// How far from its centre a sphere can affect the field: a pair's blend radius is at most twice the
    /// smaller of the two (see <see cref="PairBlend"/>).
    /// </summary>
    private static float Reach(in SphereInstance s) => s.Radius + 2f * Blend(s);

    /// <summary>
    /// Groups fused spheres into clusters of two or more whose surfaces come within blending distance of each
    /// other; only those need a mesh.
    /// </summary>
    private static List<int[]> FindClusters(IReadOnlyList<SphereInstance> spheres)
    {
        var fused = Enumerable.Range(0, spheres.Count).Where(i => Blend(spheres[i]) > 0f).OrderBy(i => spheres[i].Position.X).ToArray();
        if (fused.Length < 2) return [];

        var parent = new Dictionary<int, int>();
        foreach (int i in fused) parent[i] = i;
        int Find(int i)
        {
            while (parent[i] != i) i = parent[i] = parent[parent[i]];
            return i;
        }

        float maxReach = fused.Max(i => spheres[i].Radius) * 2f + 2f * fused.Max(i => Blend(spheres[i]));
        for (int a = 0; a < fused.Length; a++)
        {
            var sa = spheres[fused[a]];
            for (int b = a + 1; b < fused.Length; b++)
            {
                var sb = spheres[fused[b]];
                // Sorted by x, so nothing further along can reach.
                if (sb.Position.X - sa.Position.X > maxReach) break;
                float reach = sa.Radius + sb.Radius + PairBlend(Blend(sa), Blend(sb));
                if (Vector3.DistanceSquared(sa.Position, sb.Position) < reach * reach)
                {
                    parent[Find(fused[a])] = Find(fused[b]);
                }
            }
        }

        return fused.GroupBy(Find).Where(g => g.Count() > 1).Select(g => g.Order().ToArray()).ToList();
    }

    private static (Vector3 Min, Vector3 Max) Bounds(IReadOnlyList<SphereInstance> spheres, IEnumerable<int> indices, float pad)
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (int i in indices)
        {
            var s = spheres[i];
            var reach = new Vector3(Reach(s) + pad);
            min = Vector3.Min(min, s.Position - reach);
            max = Vector3.Max(max, s.Position + reach);
        }
        return (min, max);
    }

    /// <summary>A grid of field samples over one cluster, with its spheres binned into blocks of cells.</summary>
    private sealed class Grid
    {
        private Grid(Vector3 origin, float cell, int nx, int ny, int nz)
        {
            Origin = origin;
            Cell = cell;
            Nx = nx;
            Ny = ny;
            Nz = nz;
        }

        public Vector3 Origin { get; }

        public float Cell { get; }

        /// <summary>Cells along each axis; there is one more corner than cells.</summary>
        public int Nx { get; }

        public int Ny { get; }

        public int Nz { get; }

        public long CornerCount => (long)(Nx + 1) * (Ny + 1) * (Nz + 1);

        public Blob[][] Blocks { get; private set; } = [];

        private int BlocksX => Nx / BlockSize + 1;

        private int BlocksY => Ny / BlockSize + 1;

        private int BlocksZ => Nz / BlockSize + 1;

        public static Grid For(IReadOnlyList<SphereInstance> spheres, IEnumerable<int> members, float cell)
        {
            // Two cells of margin keep the surface off the edge of the grid.
            var (min, max) = Bounds(spheres, members, cell * 2f);
            var size = max - min;
            return new Grid(min, cell, Cells(size.X), Cells(size.Y), Cells(size.Z));

            int Cells(float length) => Math.Max(1, (int)MathF.Ceiling(length / cell));
        }

        public void Bin(IReadOnlyList<SphereInstance> spheres, int[] members)
        {
            var lists = new List<Blob>?[BlocksX * BlocksY * BlocksZ];
            foreach (int i in members)
            {
                var s = spheres[i];
                var blob = new Blob(s.Position, s.Radius, Blend(s), i);
                var reach = new Vector3(Reach(s) + Cell);
                var lo = BlockOf(s.Position - reach);
                var hi = BlockOf(s.Position + reach);
                for (int z = lo.Z; z <= hi.Z; z++)
                for (int y = lo.Y; y <= hi.Y; y++)
                for (int x = lo.X; x <= hi.X; x++)
                {
                    (lists[(z * BlocksY + y) * BlocksX + x] ??= []).Add(blob);
                }
            }
            Blocks = lists.Select(l => l?.ToArray() ?? []).ToArray();
        }

        public Blob[] BlobsNear(Vector3 p)
        {
            var b = BlockOf(p);
            return Blocks[(b.Z * BlocksY + b.Y) * BlocksX + b.X];
        }

        public Blob[] BlobsForCorner(int x, int y, int z) =>
            Blocks[((z / BlockSize) * BlocksY + y / BlockSize) * BlocksX + x / BlockSize];

        public Vector3 Corner(int x, int y, int z) => Origin + new Vector3(x, y, z) * Cell;

        public int CornerIndex(int x, int y, int z) => (z * (Ny + 1) + y) * (Nx + 1) + x;

        private (int X, int Y, int Z) BlockOf(Vector3 p)
        {
            var g = (p - Origin) / (Cell * BlockSize);
            return (Clamp(g.X, BlocksX), Clamp(g.Y, BlocksY), Clamp(g.Z, BlocksZ));

            static int Clamp(float v, int count) => Math.Clamp((int)MathF.Floor(v), 0, count - 1);
        }
    }

    private readonly record struct Blob(Vector3 Center, float Radius, float Blend, int Index);

    /// <summary>
    /// The fused distance field at <paramref name="p"/>: negative inside. Spheres are smoothly unioned in a fixed
    /// order (their order in the morphology), which keeps the field smooth everywhere, unlike ordering them by
    /// distance. The union so far carries a blend radius mixed from its spheres, and each new sphere blends with it
    /// at about the smaller of that and its own, so two spheres only flow together as far as both are fused.
    /// A sphere further than the blend radius from the union so far leaves it exactly as it was, so leaving out the
    /// spheres binned elsewhere does not change the field near the surface.
    /// </summary>
    private static float Field(Vector3 p, Blob[] blobs)
    {
        float result = Outside, blend = 0f;
        foreach (var blob in blobs)
        {
            float d = Vector3.Distance(p, blob.Center) - blob.Radius;
            if (result == Outside)
            {
                (result, blend) = (d, blob.Blend);
                continue;
            }
            float k = PairBlend(blend, blob.Blend);
            if (d - result >= k) continue;
            float h = Mix(result, d, k);
            result = float.Lerp(d, result, h) - k * h * (1f - h);
            blend = float.Lerp(blob.Blend, blend, h);
        }
        return result;
    }

    /// <summary>
    /// The blend radius between two spheres: the harmonic mean, which is their radius when they are alike and
    /// close to the smaller when they are not, and which varies smoothly, unlike min.
    /// </summary>
    private static float PairBlend(float a, float b) => a + b > 0f ? 2f * a * b / (a + b) : 0f;

    /// <summary>How much of the smooth union of <paramref name="a"/> and <paramref name="b"/> comes from <paramref name="a"/>.</summary>
    private static float Mix(float a, float b, float k) => k > 0f ? Math.Clamp(0.5f + 0.5f * (b - a) / k, 0f, 1f) : (a <= b ? 1f : 0f);

    /// <summary>Polynomial smooth minimum: exactly min(a, b) once they differ by more than <paramref name="k"/>.</summary>
    public static float SmoothMin(float a, float b, float k)
    {
        float h = Mix(a, b, k);
        return float.Lerp(b, a, h) - k * h * (1f - h);
    }

    private static Vector3 Gradient(Vector3 p, Blob[] blobs, float step)
    {
        var dx = new Vector3(step, 0f, 0f);
        var dy = new Vector3(0f, step, 0f);
        var dz = new Vector3(0f, 0f, step);
        return new Vector3(
            Field(p + dx, blobs) - Field(p - dx, blobs),
            Field(p + dy, blobs) - Field(p - dy, blobs),
            Field(p + dz, blobs) - Field(p - dz, blobs)) / (2f * step);
    }

    private static FusedSurface? Polygonise(
        IReadOnlyList<SphereInstance> spheres, int[] members, float cell,
        IReadOnlyList<BakedImage?> textures, IReadOnlyList<BumpGene> bumpMaps)
    {
        var grid = Grid.For(spheres, members, cell);
        grid.Bin(spheres, members);
        int nx = grid.Nx, ny = grid.Ny, nz = grid.Nz;

        var field = new float[grid.CornerCount];
        Parallel.For(0, nz + 1, z =>
        {
            for (int y = 0; y <= ny; y++)
            for (int x = 0; x <= nx; x++)
            {
                field[grid.CornerIndex(x, y, z)] = Field(grid.Corner(x, y, z), grid.BlobsForCorner(x, y, z));
            }
        });

        // Surface nets: one vertex in each cell the surface passes through, at the mean of its edge crossings.
        var cellVertex = new int[nx * ny * nz];
        var positions = new List<Vector3>();
        Span<float> v = stackalloc float[8];
        for (int z = 0; z < nz; z++)
        for (int y = 0; y < ny; y++)
        for (int x = 0; x < nx; x++)
        {
            int inside = 0;
            for (int c = 0; c < 8; c++)
            {
                v[c] = field[grid.CornerIndex(x + (c & 1), y + ((c >> 1) & 1), z + ((c >> 2) & 1))];
                if (v[c] < 0f) inside |= 1 << c;
            }
            int index = (z * ny + y) * nx + x;
            if (inside == 0 || inside == 0xFF)
            {
                cellVertex[index] = -1;
                continue;
            }

            var sum = Vector3.Zero;
            int crossings = 0;
            foreach (var (a, b) in CellEdges)
            {
                if (((inside >> a) & 1) == ((inside >> b) & 1)) continue;
                float t = v[a] / (v[a] - v[b]);
                sum += Vector3.Lerp(CornerOffset(a), CornerOffset(b), t);
                crossings++;
            }
            cellVertex[index] = positions.Count;
            positions.Add(grid.Corner(x, y, z) + sum / crossings * cell);
        }
        if (positions.Count == 0) return null;

        // Each grid edge the surface crosses gets a quad joining the four cells around it, facing outwards.
        var indices = new List<int>();
        for (int z = 1; z < nz; z++)
        for (int y = 1; y < ny; y++)
        for (int x = 1; x < nx; x++)
        {
            bool here = field[grid.CornerIndex(x, y, z)] < 0f;
            if (here != field[grid.CornerIndex(x + 1, y, z)] < 0f)
                AddQuad(here, Cell(x, y - 1, z - 1), Cell(x, y, z - 1), Cell(x, y, z), Cell(x, y - 1, z));
            if (here != field[grid.CornerIndex(x, y + 1, z)] < 0f)
                AddQuad(here, Cell(x - 1, y, z - 1), Cell(x - 1, y, z), Cell(x, y, z), Cell(x, y, z - 1));
            if (here != field[grid.CornerIndex(x, y, z + 1)] < 0f)
                AddQuad(here, Cell(x - 1, y - 1, z), Cell(x, y - 1, z), Cell(x, y, z), Cell(x - 1, y, z));
        }
        if (indices.Count == 0) return null;

        int count = positions.Count;
        var finalPositions = new Vector3[count];
        var normals = new Vector3[count];
        var colors = new Vector3[count];
        var roughness = new float[count];
        var displacement = new float[count];
        bool displaced = false;
        Parallel.For(0, count, i =>
        {
            var p = positions[i];
            var blobs = grid.BlobsNear(p);
            // One Newton step pulls the vertex onto the true surface, which smooths the faceting of the grid.
            var g = Gradient(p, blobs, cell * 0.25f);
            float length2 = g.LengthSquared();
            if (length2 > 1e-12f)
            {
                var move = g * (Field(p, blobs) / length2);
                if (move.LengthSquared() < cell * cell) p -= move;
            }
            var normal = Gradient(p, blobs, cell * 0.25f);
            normal = normal.LengthSquared() > 1e-12f ? Vector3.Normalize(normal) : Vector3.UnitY;

            (colors[i], roughness[i], displacement[i]) = Shade(p, blobs, spheres, textures, bumpMaps);
            if (displacement[i] != 0f) displaced = true;
            finalPositions[i] = p + normal * displacement[i];
            normals[i] = normal;
        });

        // Bumps move the surface itself, so their shading comes from the moved triangles.
        if (displaced) normals = FaceNormals(finalPositions, indices, normals);

        return new FusedSurface(finalPositions, normals, colors, roughness, [.. indices], members);

        int Cell(int x, int y, int z) => cellVertex[(z * ny + y) * nx + x];

        void AddQuad(bool startInside, int a, int b, int c, int d)
        {
            if (a < 0 || b < 0 || c < 0 || d < 0) return;
            // The quads above face along +axis; flip them when the outside is the start of the edge.
            if (startInside) indices.AddRange([a, b, c, a, c, d]);
            else indices.AddRange([a, c, b, a, d, c]);
        }
    }

    private static readonly (int A, int B)[] CellEdges =
    [
        (0, 1), (2, 3), (4, 5), (6, 7), // along x
        (0, 2), (1, 3), (4, 6), (5, 7), // along y
        (0, 4), (1, 5), (2, 6), (3, 7), // along z
    ];

    private static Vector3 CornerOffset(int c) => new(c & 1, (c >> 1) & 1, (c >> 2) & 1);

    /// <summary>
    /// The colour, roughness and bump displacement at a surface point, mixed from the spheres by the same smooth
    /// union as <see cref="Field"/>, so attributes blend exactly where the shapes do.
    /// </summary>
    private static (Vector3 Color, float Roughness, float Displacement) Shade(
        Vector3 p, Blob[] blobs, IReadOnlyList<SphereInstance> spheres,
        IReadOnlyList<BakedImage?> textures, IReadOnlyList<BumpGene> bumpMaps)
    {
        float result = Outside, blend = 0f;
        var color = Vector3.Zero;
        float rough = 0f, height = 0f;
        foreach (var blob in blobs)
        {
            float d = Vector3.Distance(p, blob.Center) - blob.Radius;
            float k = PairBlend(blend, blob.Blend);
            float h = 0f;
            if (result != Outside)
            {
                if (d - result >= k) continue;
                h = Mix(result, d, k);
            }

            var (c, r, b) = Attributes(p, spheres[blob.Index], textures, bumpMaps);
            color = Vector3.Lerp(c, color, h);
            rough = float.Lerp(r, rough, h);
            height = float.Lerp(b, height, h);
            result = result == Outside ? d : float.Lerp(d, result, h) - k * h * (1f - h);
            blend = float.Lerp(blob.Blend, blend, h);
        }
        return (color, rough, height);
    }

    /// <summary>One sphere's colour, roughness and bump height in the direction of <paramref name="p"/>.</summary>
    private static (Vector3 Color, float Roughness, float Height) Attributes(
        Vector3 p, in SphereInstance s, IReadOnlyList<BakedImage?> textures, IReadOnlyList<BumpGene> bumpMaps)
    {
        // The direction in the sphere's own frame, so textures and bumps line up with the drawn spheres.
        var offset = p - s.Position;
        var local = offset.LengthSquared() > 1e-12f
            ? Vector3.Transform(Vector3.Normalize(offset), Quaternion.Conjugate(s.Orientation))
            : Vector3.UnitY;

        var color = s.Color;
        if (s.Texture >= 0 && s.Texture < textures.Count && textures[s.Texture] is { } texture) color *= Sample(texture, local);
        float height = 0f;
        if (s.BumpMap >= 0 && s.BumpMap < bumpMaps.Count)
        {
            var bump = bumpMaps[s.BumpMap];
            height = (ImageBaker.Evaluate(bump.Pattern, local) - 0.5f) * bump.Depth * s.Radius;
        }
        return (color, s.Roughness, height);
    }

    /// <summary>Bilinearly samples a baked image in the direction <paramref name="d"/>, using the sphere meshes' UV layout.</summary>
    internal static Vector3 Sample(BakedImage image, Vector3 d)
    {
        float theta = MathF.Acos(Math.Clamp(d.Y, -1f, 1f));
        float phi = MathF.Atan2(d.Z, d.X);
        if (phi < 0f) phi += MathF.PI * 2f;
        float x = phi / (MathF.PI * 2f) * image.Width - 0.5f;
        float y = theta / MathF.PI * image.Height - 0.5f;

        int x0 = (int)MathF.Floor(x), y0 = (int)MathF.Floor(y);
        float fx = x - x0, fy = y - y0;
        var top = Vector3.Lerp(Texel(x0, y0), Texel(x0 + 1, y0), fx);
        var bottom = Vector3.Lerp(Texel(x0, y0 + 1), Texel(x0 + 1, y0 + 1), fx);
        return Vector3.Lerp(top, bottom, fy) / 255f;

        Vector3 Texel(int tx, int ty)
        {
            // Wrap around the equator; clamp at the poles.
            tx = ((tx % image.Width) + image.Width) % image.Width;
            ty = Math.Clamp(ty, 0, image.Height - 1);
            int i = (ty * image.Width + tx) * 4;
            return new Vector3(image.Pixels[i], image.Pixels[i + 1], image.Pixels[i + 2]);
        }
    }

    private static Vector3[] FaceNormals(Vector3[] positions, List<int> indices, Vector3[] fallback)
    {
        var normals = new Vector3[positions.Length];
        for (int i = 0; i < indices.Count; i += 3)
        {
            int a = indices[i], b = indices[i + 1], c = indices[i + 2];
            // Unnormalised, so larger triangles count for more.
            var n = Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]);
            normals[a] += n;
            normals[b] += n;
            normals[c] += n;
        }
        for (int i = 0; i < normals.Length; i++)
        {
            normals[i] = normals[i].LengthSquared() > 1e-20f ? Vector3.Normalize(normals[i]) : fallback[i];
        }
        return normals;
    }
}
