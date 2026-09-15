using System.Collections.Generic;
using UnityEngine;

namespace QuestParkTycoon
{
    /// <summary>Path-finding contract. The park-building team replaces
    /// GridPathStub with the real network (paths, queue lines, plazas).</summary>
    public interface IPathGrid
    {
        bool IsWalkable(Vector2Int tile);
        Vector3 TileToWorld(Vector2Int tile);
        Vector2Int WorldToTile(Vector3 world);
        List<Vector2Int> FindPath(Vector2Int from, Vector2Int to);
    }

    /// <summary>Stub grid: whole 16x16 pad walkable, L-shaped Manhattan paths.
    /// Origin matches SpikeBootstrap's terrain (16 tiles x 4 m, centered at 0).</summary>
    public sealed class GridPathStub : IPathGrid
    {
        public int width = 16, height = 16;
        public float tileSize = 4f;
        public Vector3 origin = new Vector3(-32f, 0f, -32f);

        public bool IsWalkable(Vector2Int t) =>
            t.x >= 0 && t.y >= 0 && t.x < width && t.y < height;

        public Vector3 TileToWorld(Vector2Int t) =>
            origin + new Vector3((t.x + 0.5f) * tileSize, 0f, (t.y + 0.5f) * tileSize);

        public Vector2Int WorldToTile(Vector3 w)
        {
            Vector3 l = w - origin;
            return new Vector2Int(Mathf.FloorToInt(l.x / tileSize), Mathf.FloorToInt(l.z / tileSize));
        }

        public List<Vector2Int> FindPath(Vector2Int from, Vector2Int to)
        {
            var path = new List<Vector2Int>();
            Vector2Int cur = from;
            int guard = 0;
            while (cur != to && guard++ < 512)
            {
                if (cur.x != to.x) cur.x += (int)Mathf.Sign(to.x - cur.x);
                else if (cur.y != to.y) cur.y += (int)Mathf.Sign(to.y - cur.y);
                if (IsWalkable(cur)) path.Add(cur);
            }
            return path;
        }
    }
}
