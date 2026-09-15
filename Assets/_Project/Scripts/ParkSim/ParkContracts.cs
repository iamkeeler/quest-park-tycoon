using System;
using System.Collections.Generic;
using UnityEngine;
using QuestParkTycoon.Rides;

namespace QuestParkTycoon
{
    // ------------------------------------------------------------------
    // RIDES — implemented by the coaster/rides team.
    // ------------------------------------------------------------------

    /// <summary>
    /// Contract every ride implements so guests, staff and economy can use it.
    /// The rides team owns the implementation; this interface is the handshake.
    /// </summary>
    public interface IRide
    {
        int RideId { get; }
        string RideName { get; }
        float Excitement { get; }   // 0..10, drives willingness to pay
        float Intensity { get; }    // 0..10, matched against guest preference
        float Nausea { get; }       // 0..10
        float TicketPrice { get; set; }
        bool IsOpen { get; }
        bool IsBroken { get; }
        float Reliability { get; set; } // 0..1, decays; mechanics restore it
        Vector3 EntrancePosition { get; }
        Vector3 ExitPosition { get; }   // mechanics enter through here (RCT1 rule)
        int QueueCount { get; }
        int QueueCapacity { get; }
        void SetBroken(bool broken);
        bool TryJoinQueue(GuestAgent guest);
        void LeaveQueue(GuestAgent guest);
    }

    /// <summary>
    /// Single source of truth for stall kinds (owned by the rides team).
    /// Guest AI seeks these concrete types; see StallDirectory.FindNearestAny
    /// for multi-type (food = burger OR cotton candy) lookups.
    /// Contract for shops/stalls. Serve() charges the guest via
    /// Economy.TakePayment, applies hunger/thirst/bladder effects, and
    /// returns false when the guest can't/won't pay.
    /// </summary>
    public interface IStall
    {
        string StallName { get; }
        StallType Type { get; }
        float Price { get; set; }
        Vector3 Position { get; }
        bool IsOpen { get; }
        bool Serve(GuestAgent guest);
    }

    /// <summary>Registry the rides team populates (Register in Awake/OnEnable).</summary>
    public static class RideDirectory
    {
        private static readonly List<IRide> _rides = new List<IRide>();
        public static IReadOnlyList<IRide> Rides => _rides;
        public static void Register(IRide ride)
        {
            if (ride != null && !_rides.Contains(ride)) _rides.Add(ride);
        }
        public static void Unregister(IRide ride) => _rides.Remove(ride);
    }

    /// <summary>Registry the stalls team populates.</summary>
    public static class StallDirectory
    {
        private static readonly List<IStall> _stalls = new List<IStall>();
        public static IReadOnlyList<IStall> Stalls => _stalls;
        public static void Register(IStall stall)
        {
            if (stall != null && !_stalls.Contains(stall)) _stalls.Add(stall);
        }
        public static void Unregister(IStall stall) => _stalls.Remove(stall);

        public static IStall FindNearest(StallType type, Vector3 from) =>
            FindNearestAny(new[] { type }, from);

        /// <summary>
        /// Nearest open stall matching ANY of the given types.
        /// Guest AI uses this: hungry -&gt; {Burger, CottonCandy}, thirsty -&gt; {Drinks}.
        /// </summary>
        public static IStall FindNearestAny(IEnumerable<StallType> types, Vector3 from)
        {
            var wanted = new HashSet<StallType>(types);
            IStall best = null;
            float bestDist = float.MaxValue;
            foreach (IStall s in _stalls)
            {
                if (!wanted.Contains(s.Type) || !s.IsOpen) continue;
                float d = (s.Position - from).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = s; }
            }
            return best;
        }
    }

    // ------------------------------------------------------------------
    // LITTER — the handyman gameplay loop.
    // ------------------------------------------------------------------

    public enum LitterKind { Trash, Vomit }

    public sealed class LitterSpot
    {
        public Vector3 position;
        public LitterKind kind;
        public float ageMinutes;
    }

    // ------------------------------------------------------------------
    // PARK EVENTS — cross-team message bus. SDK-free, no UnityEvents needed.
    // ------------------------------------------------------------------

    /// <summary>
    /// How teams talk to each other:
    /// - Coaster team signals a breakdown: ParkEvents.RaiseBreakdown(ride)
    /// - Mechanic signals repair done: ParkEvents.RaiseRepaired(ride)
    /// - Anyone spawns litter/vomit: ParkEvents.RaiseLitter(pos, kind)
    /// - Handyman clears it: ParkEvents.ClearLitter(spot)
    /// - Staff quits are announced: ThoughtSystem.Announce(text)
    /// The litter list is backed by LitterSystem (single registry, cap 200).
    /// </summary>
    public static class ParkEvents
    {
        public static event Action<LitterSpot> OnLitterSpawned;
        public static event Action<LitterSpot> OnLitterCleared;
        public static event Action<IRide> OnRideBrokenDown;
        public static event Action<IRide> OnRideRepaired;

        /// <summary>Live litter registry (backed by LitterSystem).</summary>
        public static IReadOnlyList<LitterSpot> Litter => LitterSystem.Spots;
        public const int MaxLitter = LitterSystem.MaxSpots;

        public static LitterSpot RaiseLitter(Vector3 pos, LitterKind kind)
        {
            var spot = LitterSystem.AddSpot(pos, kind);
            OnLitterSpawned?.Invoke(spot);
            return spot;
        }

        public static void ClearLitter(LitterSpot spot)
        {
            if (LitterSystem.RemoveSpot(spot)) OnLitterCleared?.Invoke(spot);
        }

        public static void RaiseBreakdown(IRide ride)
        {
            if (ride == null || ride.IsBroken) return;
            ride.SetBroken(true);
            OnRideBrokenDown?.Invoke(ride);
        }

        public static void RaiseRepaired(IRide ride)
        {
            if (ride == null || !ride.IsBroken) return;
            ride.SetBroken(false);
            ride.Reliability = Mathf.Max(ride.Reliability, 0.85f);
            OnRideRepaired?.Invoke(ride);
        }
    }

    // ------------------------------------------------------------------
    // SHARED MOVEMENT — guests and staff steer the same way.
    // ------------------------------------------------------------------

    public static class AgentMovement
    {
        /// <summary>Walks a transform toward dest at speed m/s. Returns true on arrival.</summary>
        public static bool MoveToward(Transform t, Vector3 dest, float speed, float dt)
        {
            Vector3 to = dest - t.position;
            to.y = 0f;
            float dist = to.magnitude;
            float step = speed * dt;
            if (dist <= Mathf.Max(step, 0.05f))
            {
                t.position = new Vector3(dest.x, t.position.y, dest.z);
                return true;
            }
            t.position += to.normalized * step;
            t.rotation = Quaternion.Slerp(t.rotation,
                Quaternion.LookRotation(to.normalized), Mathf.Min(1f, dt * 8f));
            return false;
        }
    }
}
