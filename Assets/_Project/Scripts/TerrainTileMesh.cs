using System.Collections.Generic;
using UnityEngine;

namespace QuestParkTycoon
{
    /// <summary>
    /// Builds the spike's terrain: a flat grid of tiles with per-vertex colors
    /// (greens with slight variation, like a diorama base). Non-indexed triangles
    /// with explicit face normals = crisp flat shading with zero lighting cost.
    /// </summary>
    public static class TerrainTileMesh
    {
        public static Mesh Build(int tilesX, int tilesZ, float tileSize)
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color>();

            var rng = new System.Random(1337);
            float ox = -tilesX * tileSize * 0.5f;
            float oz = -tilesZ * tileSize * 0.5f;

            for (int x = 0; x < tilesX; x++)
            {
                for (int z = 0; z < tilesZ; z++)
                {
                    float x0 = ox + x * tileSize, x1 = x0 + tileSize;
                    float z0 = oz + z * tileSize, z1 = z0 + tileSize;

                    // Diorama grass: base green, jittered per tile for the handmade feel.
                    float j = (float)rng.NextDouble() * 0.08f;
                    var c = new Color(0.32f + j, 0.62f + j, 0.28f + j * 0.5f, 1f);
                    // Path cross through the middle, warm gravel tone.
                    if (x == tilesX / 2 || z == tilesZ / 2)
                        c = new Color(0.72f + j, 0.62f + j, 0.47f, 1f);

                    Vector3 a = new Vector3(x0, 0f, z0), b = new Vector3(x1, 0f, z0);
                    Vector3 c2 = new Vector3(x1, 0f, z1), d = new Vector3(x0, 0f, z1);
                    AddTri(verts, normals, colors, a, b, c2, c);
                    AddTri(verts, normals, colors, a, c2, d, c);
                }
            }

            var mesh = new Mesh { name = "TerrainTiles" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddTri(List<Vector3> v, List<Vector3> n, List<Color> c,
            Vector3 a, Vector3 b, Vector3 c2, Color col)
        {
            v.Add(a); v.Add(b); v.Add(c2);
            var normal = Vector3.Cross(b - a, c2 - a).normalized;
            n.Add(normal); n.Add(normal); n.Add(normal);
            c.Add(col); c.Add(col); c.Add(col);
        }
    }
}
