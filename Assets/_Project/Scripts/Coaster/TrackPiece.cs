using System.Collections.Generic;
using UnityEngine;

namespace QuestParkTycoon.Coaster
{
    /// <summary>Buildable track piece types (RCT1 "Steel Corkscrew" set, VR edition).</summary>
    public enum PieceType { Straight, CurveLeft, CurveRight, SlopeUp, SlopeDown,
        BankedCurveLeft, BankedCurveRight, HelixLeft, HelixRight, Loop, Corkscrew, Station, LiftHill }
    /// <summary>Position + orientation of a piece endpoint. Angles in radians.</summary>
    public struct PiecePose
    {
        public Vector3 position;
        public float yaw, pitch, bank; // yaw: heading around Y. pitch: nose-up +. bank: roll.
        public static PiecePose Identity => new PiecePose();
        public Quaternion Rotation =>
            Quaternion.AngleAxis(yaw * Mathf.Rad2Deg, Vector3.up) *
            Quaternion.AngleAxis(pitch * Mathf.Rad2Deg, Vector3.right) *
            Quaternion.AngleAxis(bank * Mathf.Rad2Deg, Vector3.forward);
    }
    /// <summary>Static per-type data: cost model + physics use this, not the mesh.</summary>
    public struct PieceSpec
    {
        public float length, heightDelta;
        public int inversions;
        public bool isLift, isStation;
    }
    /// <summary>
    /// Track piece data + procedural mesh builder. Entry is always at the local
    /// origin heading +Z; Sample(t) gives the centerline so layouts can chain
    /// pieces and the test-ride camera can follow them.
    /// </summary>
    public static class TrackPiece
    {
        public const float Gauge = 1.2f; // rail separation, meters
        private const float RailSize = 0.10f, CurveRadius = 8f, HelixRadius = 6f, LoopRadius = 4f, BankMax = 0.6f;
        public static PieceSpec GetSpec(PieceType t)
        {
            var s = new PieceSpec();
            switch (t)
            {
                case PieceType.Straight: s.length = 8f; break;
                case PieceType.CurveLeft: case PieceType.CurveRight:
                case PieceType.BankedCurveLeft: case PieceType.BankedCurveRight:
                    s.length = CurveRadius * Mathf.PI * 0.5f; break;
                case PieceType.SlopeUp: s.length = 8.94f; s.heightDelta = 4f; break;
                case PieceType.SlopeDown: s.length = 8.94f; s.heightDelta = -4f; break;
                case PieceType.HelixLeft: case PieceType.HelixRight:
                    s.length = 38f; s.heightDelta = 5f; break;
                case PieceType.Loop: s.length = 25.13f; s.inversions = 1; break;
                case PieceType.Corkscrew: s.length = 12f; s.inversions = 1; break;
                case PieceType.Station: s.length = 12f; s.isStation = true; break;
                case PieceType.LiftHill: s.length = 13.42f; s.heightDelta = 6f; s.isLift = true; break;
            }
            return s;
        }
        /// <summary>Centerline sample in piece-local space (entry at origin, +Z).</summary>
        public static void Sample(PieceType type, float t,
            out Vector3 pos, out float yaw, out float pitch, out float bank)
        {
            t = Mathf.Clamp01(t);
            yaw = 0f; pitch = 0f; bank = 0f; pos = Vector3.zero;
            switch (type)
            {
                case PieceType.Straight: pos = new Vector3(0f, 0f, 8f * t); break;
                case PieceType.SlopeUp:
                    pos = new Vector3(0f, 4f * t, 8f * t); pitch = Mathf.Atan2(4f, 8f); break;
                case PieceType.SlopeDown:
                    pos = new Vector3(0f, -4f * t, 8f * t); pitch = -Mathf.Atan2(4f, 8f); break;
                case PieceType.CurveLeft: case PieceType.CurveRight:
                case PieceType.BankedCurveLeft: case PieceType.BankedCurveRight:
                {
                    float sgn = (type == PieceType.CurveRight || type == PieceType.BankedCurveRight) ? 1f : -1f;
                    float th = t * Mathf.PI * 0.5f;
                    pos = new Vector3(sgn * CurveRadius * Mathf.Sin(th), 0f, CurveRadius * (1f - Mathf.Cos(th)));
                    yaw = sgn * th;
                    bool banked = type == PieceType.BankedCurveLeft || type == PieceType.BankedCurveRight;
                    if (banked) bank = sgn * BankMax * Mathf.Sin(Mathf.PI * t);
                    break;
                }
                case PieceType.HelixLeft: case PieceType.HelixRight:
                {
                    float sgn = type == PieceType.HelixRight ? 1f : -1f;
                    float th = t * Mathf.PI * 2f;
                    pos = new Vector3(sgn * HelixRadius * Mathf.Sin(th), 5f * t, HelixRadius * (1f - Mathf.Cos(th)));
                    yaw = sgn * th; bank = sgn * 0.45f; break;
                }
                case PieceType.Loop:
                {
                    float ph = t * Mathf.PI * 2f;
                    pos = new Vector3(0f, LoopRadius * (1f - Mathf.Cos(ph)), LoopRadius * Mathf.Sin(ph));
                    pitch = ph; break;
                }
                case PieceType.Corkscrew:
                    pos = new Vector3(0f, 0f, 12f * t); bank = t * Mathf.PI * 2f; break;
                case PieceType.Station: pos = new Vector3(0f, 0f, 12f * t); break;
                case PieceType.LiftHill:
                    pos = new Vector3(0f, 6f * t, 12f * t); pitch = Mathf.Atan2(6f, 12f); break;
            }
        }
        /// <summary>Procedural low-poly mesh: two rails + cross ties, vertex-colored
        /// clay style. Non-indexed; supports/terrain footings are Phase 1.</summary>
        public static Mesh BuildMesh(PieceType type, int seed = 0)
        {
            var spec = GetSpec(type);
            var mb = new MeshBuilder();
            var rng = new System.Random(seed * 7919 + (int)type * 131 + 7);
            Color railCol = spec.isLift ? new Color(0.80f, 0.62f, 0.22f)
                          : spec.isStation ? new Color(0.45f, 0.47f, 0.52f)
                          : new Color(0.16f, 0.34f, 0.40f);
            float j = 0.94f + (float)rng.NextDouble() * 0.12f; // handmade jitter
            railCol *= j;
            Color tieCol = new Color(0.55f, 0.36f, 0.22f) * j;
            int steps = type == PieceType.Loop ? 28 : 16;
            var ringL = new Vector3[4]; var ringR = new Vector3[4];
            Vector3[] prevL = null, prevR = null;
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                FrameAt(type, t, out Vector3 p, out Vector3 fwd, out Vector3 right, out Vector3 up);
                BuildRing(p + right * (Gauge * 0.5f), right, up, ringL);
                BuildRing(p - right * (Gauge * 0.5f), right, up, ringR);
                if (prevL != null) { Strip(mb, prevL, ringL, railCol); Strip(mb, prevR, ringR, railCol); }
                if (i % 2 == 0 && i < steps)
                    mb.Box(p - up * 0.22f, right, up, fwd, Gauge + 0.6f, 0.12f, 0.55f, tieCol);
                prevL = (Vector3[])ringL.Clone(); prevR = (Vector3[])ringR.Clone();
            }
            if (spec.isStation) // boarding platform alongside the track
            {
                Sample(type, 0.5f, out Vector3 sp, out _, out _, out _);
                mb.Box(sp + new Vector3(Gauge * 0.5f + 1.1f, -0.45f, 0f), Vector3.right, Vector3.up,
                    Vector3.forward, 2.2f, 0.3f, 12f, new Color(0.72f, 0.62f, 0.45f));
            }
            return mb.ToMesh("Track_" + type);
        }
        private static void FrameAt(PieceType type, float t,
            out Vector3 pos, out Vector3 fwd, out Vector3 right, out Vector3 up)
        {
            Sample(type, t, out pos, out float yaw, out float pitch, out float bank);
            fwd = new Vector3(Mathf.Sin(yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch),
                              Mathf.Cos(yaw) * Mathf.Cos(pitch)).normalized;
            Vector3 r0 = Vector3.Cross(Vector3.up, fwd);
            r0 = r0.sqrMagnitude < 1e-6f ? Vector3.right : r0.normalized;
            Vector3 u0 = Vector3.Cross(fwd, r0).normalized;
            float cb = Mathf.Cos(bank), sb = Mathf.Sin(bank);
            right = (r0 * cb + u0 * sb).normalized;
            up = (u0 * cb - r0 * sb).normalized;
        }
        private static void BuildRing(Vector3 c, Vector3 r, Vector3 u, Vector3[] ring)
        {
            float h = RailSize * 0.5f;
            ring[0] = c - r * h - u * h; ring[1] = c + r * h - u * h;
            ring[2] = c + r * h + u * h; ring[3] = c - r * h + u * h;
        }
        private static void Strip(MeshBuilder mb, Vector3[] a, Vector3[] b, Color col)
        {
            for (int k = 0; k < 4; k++) mb.Quad(a[k], a[(k + 1) % 4], b[(k + 1) % 4], b[k], col);
        }
        /// <summary>Non-indexed triangle soup; double-wound quads render under any cull mode.</summary>
        private sealed class MeshBuilder
        {
            private readonly List<Vector3> _v = new List<Vector3>();
            private readonly List<Vector3> _n = new List<Vector3>();
            private readonly List<Color> _c = new List<Color>();
            public void Tri(Vector3 a, Vector3 b, Vector3 c, Color col)
            {
                Vector3 nrm = Vector3.Cross(b - a, c - a);
                nrm = nrm.sqrMagnitude < 1e-8f ? Vector3.up : nrm.normalized;
                _v.Add(a); _v.Add(b); _v.Add(c);
                _n.Add(nrm); _n.Add(nrm); _n.Add(nrm);
                _c.Add(col); _c.Add(col); _c.Add(col);
            }
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
            {
                Tri(a, b, c, col); Tri(a, c, d, col);
                Tri(a, c, b, col); Tri(a, d, c, col);
            }
            public void Box(Vector3 center, Vector3 x, Vector3 y, Vector3 z,
                float sx, float sy, float sz, Color col)
            {
                Vector3 hx = x * sx * 0.5f, hy = y * sy * 0.5f, hz = z * sz * 0.5f;
                Vector3 v000 = center - hx - hy - hz, v100 = center + hx - hy - hz;
                Vector3 v110 = center + hx + hy - hz, v010 = center - hx + hy - hz;
                Vector3 v001 = center - hx - hy + hz, v101 = center + hx - hy + hz;
                Vector3 v111 = center + hx + hy + hz, v011 = center - hx + hy + hz;
                Quad(v100, v110, v111, v101, col); Quad(v000, v001, v011, v010, col);
                Quad(v010, v011, v111, v110, col); Quad(v000, v100, v101, v001, col);
                Quad(v001, v101, v111, v011, col); Quad(v000, v010, v110, v100, col);
            }
            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name };
                m.SetVertices(_v); m.SetNormals(_n); m.SetColors(_c);
                m.RecalculateBounds();
                return m;
            }
        }
    }
}
