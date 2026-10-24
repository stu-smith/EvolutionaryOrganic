using System.Numerics;
using HelixToolkit;
using HelixToolkit.SharpDX;

namespace EvolutionaryOrganic.App.Rendering;

/// <summary>
/// Shared unit-sphere meshes at several levels of detail. Each has normals, UVs and tangent frames,
/// so normal and bump maps can be added later without changing the geometry.
/// </summary>
public static class SphereMeshes
{
    public static MeshGeometry3D High { get; } = Create(64, 32);

    public static MeshGeometry3D Medium { get; } = Create(40, 20);

    public static MeshGeometry3D Low { get; } = Create(24, 12);

    /// <summary>Picks a level of detail that keeps the triangle count manageable.</summary>
    public static MeshGeometry3D ForSphereCount(int count) => count switch
    {
        <= 600 => High,
        <= 2500 => Medium,
        _ => Low,
    };

    private static MeshGeometry3D Create(int slices, int stacks)
    {
        var positions = new Vector3Collection();
        var normals = new Vector3Collection();
        var tangents = new Vector3Collection();
        var bitangents = new Vector3Collection();
        var uvs = new Vector2Collection();
        var indices = new IntCollection();

        for (int stack = 0; stack <= stacks; stack++)
        {
            float v = (float)stack / stacks;
            float theta = v * MathF.PI;
            float sinTheta = MathF.Sin(theta), cosTheta = MathF.Cos(theta);

            for (int slice = 0; slice <= slices; slice++)
            {
                float u = (float)slice / slices;
                float phi = u * MathF.PI * 2f;
                float sinPhi = MathF.Sin(phi), cosPhi = MathF.Cos(phi);

                var normal = new Vector3(sinTheta * cosPhi, cosTheta, sinTheta * sinPhi);
                var tangent = new Vector3(-sinPhi, 0f, cosPhi);
                positions.Add(normal);
                normals.Add(normal);
                tangents.Add(tangent);
                bitangents.Add(Vector3.Cross(normal, tangent));
                uvs.Add(new Vector2(u, v));
            }
        }

        int row = slices + 1;
        for (int stack = 0; stack < stacks; stack++)
        {
            for (int slice = 0; slice < slices; slice++)
            {
                int a = stack * row + slice;
                int b = a + row;
                // Skip the degenerate triangles that would sit on the poles.
                if (stack != 0) { indices.Add(a); indices.Add(a + 1); indices.Add(b); }
                if (stack != stacks - 1) { indices.Add(a + 1); indices.Add(b + 1); indices.Add(b); }
            }
        }

        return new MeshGeometry3D
        {
            Positions = positions,
            Normals = normals,
            Tangents = tangents,
            BiTangents = bitangents,
            TextureCoordinates = uvs,
            Indices = indices,
        };
    }
}
