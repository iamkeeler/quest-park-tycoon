using System.Collections.Generic;
using UnityEngine;

namespace QuestParkTycoon.Rides
{
    /// <summary>
    /// Central registry for everything built. Handles placement validation
    /// against the 64x64 m terrain pad, footprint overlap, open/close, pricing,
    /// and the RCT1 duplicate-type value penalty. UI team: call the public
    /// methods here; don't touch Ride internals directly.
    /// </summary>
    public sealed class RideManager : MonoBehaviour
    {
        public static RideManager Instance { get; private set; }

        public float terrainHalfSize = 32f; // matches SpikeBootstrap 64 m pad

        private readonly List<Ride> _rides = new List<Ride>();
        private readonly Dictionary<Ride, float> _footprints = new Dictionary<Ride, float>();

        public IReadOnlyList<Ride> Rides => _rides;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        public void Register(Ride ride, float footprintRadius)
        {
            if (ride == null || _rides.Contains(ride)) return;
            _rides.Add(ride);
            _footprints[ride] = footprintRadius;
        }

        public void Unregister(Ride ride)
        {
            _rides.Remove(ride);
            _footprints.Remove(ride);
        }

        /// <summary>
        /// Validate + place a ride. Checks terrain bounds and footprint overlap.
        /// UI team: call this from the build-mode placement ghost.
        /// </summary>
        public bool TryPlaceRide(Ride ride, Vector3 position, float footprintRadius,
                                 out string error)
        {
            error = null;
            if (ride == null) { error = "No ride to place."; return false; }

            // Research gate (economy team): unresearched types can't be built yet.
            if (!ResearchSystem.IsUnlocked(ride.rideType))
            {
                error = $"{ride.rideName} hasn't been researched yet.";
                return false;
            }

            if (Mathf.Abs(position.x) + footprintRadius > terrainHalfSize ||
                Mathf.Abs(position.z) + footprintRadius > terrainHalfSize)
            {
                error = "Outside park boundary.";
                return false;
            }

            foreach (var other in _rides)
            {
                if (!_footprints.TryGetValue(other, out float r)) continue;
                float need = r + footprintRadius;
                Vector3 d = other.transform.position - position;
                d.y = 0f;
                if (d.magnitude < need)
                {
                    error = $"Overlaps {other.rideName}.";
                    return false;
                }
            }

            ride.transform.position = position;
            Register(ride, footprintRadius);
            return true;
        }

        // ---- pricing / operation (UI team API) ----

        public void SetTicketPrice(Ride ride, float price)
        {
            if (ride != null) ride.ticketPrice = Mathf.Max(0f, price);
        }

        public void SetOpen(Ride ride, bool open)
        {
            if (ride == null) return;
            if (open) ride.Open(); else ride.Close();
        }

        public void SetAdmissionFee(float fee) => Economy.AdmissionFee = Mathf.Max(0f, fee);

        // ---- economy helpers ----

        /// <summary>RCT1: each duplicate of a type is worth ~25% less.</summary>
        public int DuplicateCount(RideType type)
        {
            int n = 0;
            foreach (var r in _rides) if (r.rideType == type) n++;
            return n;
        }

        public float DuplicatePenaltyFor(Ride ride) =>
            Economy.DuplicatePenalty(Mathf.Max(0, DuplicateCount(ride.rideType) - 1));

        /// <summary>Park value = sum of ride current values (scenario objective).</summary>
        public float ParkValue()
        {
            float v = 0f;
            foreach (var r in _rides) v += r.CurrentValue(DuplicatePenaltyFor(r));
            return v;
        }

        /// <summary>Guest-AI team: nearest open stall of a type (stall seeking).</summary>
        public Stall FindNearestStall(StallType type, Vector3 fromPos)
        {
            Stall best = null;
            float bestD = float.MaxValue;
            foreach (var r in _rides)
            {
                if (r is Stall s && s.stallType == type && s.isOpen)
                {
                    float d = (s.transform.position - fromPos).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = s; }
                }
            }
            return best;
        }

        /// <summary>Guest-AI team: nearest open flat ride matching intensity taste.</summary>
        public FlatRide FindNearestRide(Vector3 fromPos, float maxIntensity)
        {
            FlatRide best = null;
            float bestD = float.MaxValue;
            foreach (var r in _rides)
            {
                if (r is FlatRide f && f.isOpen && !f.isBroken && f.intensity <= maxIntensity)
                {
                    float d = (f.transform.position - fromPos).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = f; }
                }
            }
            return best;
        }
    }
}
