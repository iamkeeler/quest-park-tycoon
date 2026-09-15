using System.Collections.Generic;
using UnityEngine;

namespace QuestParkTycoon.Coaster
{
    /// <summary>Physics summary of a circuit. Feeds the E/I/N panel and pricing.</summary>
    public struct RideStats
    {
        public float maxSpeed, avgSpeed, rideTime, maxG, maxLateralG, airTime, length;
        public int drops, inversions; // drops: descents of 4 m+
    }
    /// <summary>
    /// Ordered piece list forming a coaster circuit. Pure data + math, no
    /// MonoBehaviour, no rendering — the sim, builder, and test ride share it.
    /// </summary>
    public sealed class CoasterLayout
    {
        private readonly List<PieceType> _pieces = new List<PieceType>();
        public IReadOnlyList<PieceType> Pieces => _pieces;
        public int PieceCount => _pieces.Count;
        public void AddPiece(PieceType type) => _pieces.Add(type);
        public bool RemoveLast()
        {
            if (_pieces.Count == 0) return false;
            _pieces.RemoveAt(_pieces.Count - 1);
            return true;
        }
        public void Clear() => _pieces.Clear();
        public float TotalLength
        {
            get { float l = 0f; foreach (var p in _pieces) l += TrackPiece.GetSpec(p).length; return l; }
        }
        /// <summary>Entry pose of piece[index]; index == PieceCount = final exit pose.</summary>
        public PiecePose GetEntryPose(int index)
        {
            var pose = PiecePose.Identity;
            for (int i = 0; i < Mathf.Min(index, _pieces.Count); i++)
            {
                TrackPiece.Sample(_pieces[i], 1f, out Vector3 lp, out float y, out float p, out float b);
                pose = new PiecePose
                {
                    position = pose.position + pose.Rotation * lp,
                    yaw = pose.yaw + y, pitch = p, bank = b, // pieces declare exit pitch
                };
            }
            return pose;
        }
        /// <summary>RCT1-style build rules: station first, lift hill present,
        /// closed loop, no impossible climbs.</summary>
        public bool Validate(out string[] errors)
        {
            var errs = new List<string>();
            if (_pieces.Count == 0) errs.Add("Layout is empty.");
            else
            {
                if (_pieces[0] != PieceType.Station) errs.Add("Circuit must start with a Station.");
                bool lift = false;
                foreach (var p in _pieces) if (TrackPiece.GetSpec(p).isLift) lift = true;
                if (!lift) errs.Add("Circuit needs a LiftHill (the train cannot climb otherwise).");
                var exit = GetEntryPose(_pieces.Count);
                Vector3 d = exit.position - PiecePose.Identity.position;
                if (new Vector3(d.x, 0f, d.z).magnitude > 1.5f)
                    errs.Add($"Not a closed loop: exit is {d.magnitude:F1} m from the station entry.");
                if (Mathf.Abs(d.y) > 1.0f) errs.Add($"Height mismatch at closure: {d.y:F1} m.");
                float yawErr = Mathf.Abs(Mathf.DeltaAngle(0f, exit.yaw * Mathf.Rad2Deg));
                if (yawErr > 15f) errs.Add($"Heading mismatch at closure: {yawErr:F0} deg.");
                for (int i = 0; i < _pieces.Count; i++)
                {
                    var s = TrackPiece.GetSpec(_pieces[i]);
                    if (!s.isLift && s.heightDelta > 7f)
                        errs.Add($"Piece {i} ({_pieces[i]}) climbs {s.heightDelta:F0} m with no lift.");
                }
            }
            errors = errs.ToArray();
            return errs.Count == 0;
        }
        public float[] GetSpeedProfile(float step = 0.5f)
        {
            var sim = Simulate(step);
            var speeds = new float[sim.Count];
            for (int i = 0; i < sim.Count; i++) speeds[i] = sim[i].speed;
            return speeds;
        }
        public RideStats ComputeStats()
        {
            var s = new RideStats();
            if (_pieces.Count == 0) return s;
            var samples = Simulate(0.5f);
            const float g = 9.81f;
            float time = 0f, air = 0f, maxV = 0f, maxG = 0f, maxLat = 0f, descent = 0f, prevY = samples[0].pos.y;
            int drops = 0;
            Vector3 prevVel = Vector3.zero;
            bool hasPrev = false;
            foreach (var p in _pieces) s.inversions += TrackPiece.GetSpec(p).inversions;
            for (int i = 0; i < samples.Count; i++)
            {
                var sm = samples[i];
                maxV = Mathf.Max(maxV, sm.speed);
                float dt = sm.ds / Mathf.Max(sm.speed, 0.5f);
                time += dt;
                float dy = sm.pos.y - prevY; prevY = sm.pos.y;
                if (dy < 0f) descent -= dy; else { if (descent >= 4f) drops++; descent = 0f; }
                Vector3 dir = new Vector3(Mathf.Sin(sm.yaw) * Mathf.Cos(sm.pitch), Mathf.Sin(sm.pitch),
                                          Mathf.Cos(sm.yaw) * Mathf.Cos(sm.pitch)).normalized;
                Vector3 vel = dir * sm.speed;
                if (hasPrev && dt > 1e-5f)
                {
                    Vector3 felt = (vel - prevVel) / dt / g + Vector3.up; // (a - gravity)/g
                    RiderFrame(dir, sm.bank, out Vector3 right, out Vector3 up);
                    float gVert = Vector3.Dot(felt, up), gLat = Vector3.Dot(felt, right);
                    maxG = Mathf.Max(maxG, gVert);
                    maxLat = Mathf.Max(maxLat, Mathf.Abs(gLat));
                    if (gVert < 0.5f) air += dt;
                }
                prevVel = vel; hasPrev = true;
            }
            s.maxSpeed = maxV; s.rideTime = time;
            s.avgSpeed = time > 0f ? TotalLength / time : 0f;
            s.maxG = maxG; s.maxLateralG = maxLat; s.airTime = air;
            s.length = TotalLength; s.drops = drops;
            return s;
        }
        /// <summary>World-space frame at `distance` meters along the circuit.</summary>
        public bool SampleFrame(float distance, out Vector3 pos, out float yaw, out float pitch, out float bank)
        {
            pos = Vector3.zero; yaw = 0f; pitch = 0f; bank = 0f;
            if (_pieces.Count == 0 || distance < 0f || distance > TotalLength) return false;
            float acc = 0f;
            for (int i = 0; i < _pieces.Count; i++)
            {
                float len = TrackPiece.GetSpec(_pieces[i]).length;
                if (distance <= acc + len || i == _pieces.Count - 1)
                {
                    float t = Mathf.Clamp01((distance - acc) / len);
                    var entry = GetEntryPose(i);
                    TrackPiece.Sample(_pieces[i], t, out Vector3 lp, out float y, out float p, out float b);
                    pos = entry.position + entry.Rotation * lp;
                    yaw = entry.yaw + y; pitch = p; bank = b;
                    return true;
                }
                acc += len;
            }
            return false;
        }
        public bool SampleWorld(float distance, out Vector3 pos, out Quaternion rot)
        {
            if (!SampleFrame(distance, out pos, out float yaw, out float pitch, out float bank))
            { rot = Quaternion.identity; return false; }
            rot = ComposeRot(yaw, pitch, bank);
            return true;
        }
        public static Quaternion ComposeRot(float yaw, float pitch, float bank) =>
            Quaternion.AngleAxis(yaw * Mathf.Rad2Deg, Vector3.up) *
            Quaternion.AngleAxis(pitch * Mathf.Rad2Deg, Vector3.right) *
            Quaternion.AngleAxis(bank * Mathf.Rad2Deg, Vector3.forward);
        /// <summary>Rider frame: right/up perpendicular to travel, rolled by bank.</summary>
        public static void RiderFrame(Vector3 dir, float bank, out Vector3 right, out Vector3 up)
        {
            Vector3 r0 = Vector3.Cross(Vector3.up, dir);
            r0 = r0.sqrMagnitude < 1e-6f ? Vector3.right : r0.normalized;
            Vector3 u0 = Vector3.Cross(dir, r0).normalized;
            float cb = Mathf.Cos(bank), sb = Mathf.Sin(bank);
            right = (r0 * cb + u0 * sb).normalized;
            up = (u0 * cb - r0 * sb).normalized;
        }
        private struct SimSample { public Vector3 pos; public float yaw, pitch, bank, speed, ds; }
        // Energy model: v^2 += 2g(-dy)*0.92, rolling friction, 1.5 m/s floor (anti-rollback).
        private List<SimSample> Simulate(float step)
        {
            var result = new List<SimSample>();
            float v = 4f; // station dispatch speed
            const float g = 9.81f;
            Vector3 prevPos = Vector3.zero;
            bool first = true;
            for (int i = 0; i < _pieces.Count; i++)
            {
                var entry = GetEntryPose(i);
                var spec = TrackPiece.GetSpec(_pieces[i]);
                int n = Mathf.Max(2, Mathf.RoundToInt(spec.length / step));
                for (int k = 0; k <= n; k++)
                {
                    if (i > 0 && k == 0) continue; // joints belong to the previous piece
                    float t = (float)k / n;
                    TrackPiece.Sample(_pieces[i], t, out Vector3 lp, out float y, out float p, out float b);
                    Vector3 wp = entry.position + entry.Rotation * lp;
                    float ds = first ? 0f : Vector3.Distance(wp, prevPos);
                    if (!first && ds > 1e-4f)
                    {
                        float dy = wp.y - prevPos.y;
                        v = Mathf.Sqrt(Mathf.Max(2.25f, v * v + 2f * g * (-dy) * 0.92f));
                        v *= 1f / (1f + 0.012f * ds);
                    }
                    result.Add(new SimSample { pos = wp, yaw = entry.yaw + y, pitch = p, bank = b, speed = v, ds = ds });
                    prevPos = wp; first = false;
                }
            }
            return result;
        }
    }
}
