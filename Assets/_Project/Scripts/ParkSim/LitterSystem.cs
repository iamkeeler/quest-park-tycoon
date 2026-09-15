using System.Collections.Generic;
using UnityEngine;

namespace QuestParkTycoon
{
    /// <summary>
    /// Static litter registry backing the handyman loop and park cleanliness.
    /// Guests drop trash when no bin is near (RCT1: bins every few tiles or
    /// the paths get filthy); handymen sweep via ParkEvents; spots older than
    /// ~30 game-minutes crumble away on their own.
    /// ParkRating consumes CleanlinessPenalty() (0 = spotless, 1 = filthy).
    /// No Meta SDK, no scene file — ticked at 10 Hz via SimTick.
    /// </summary>
    public sealed class LitterSystem : MonoBehaviour, ISimSystem
    {
        /// <summary>
        /// World positions of litter bins. The world bootstrap fills this as
        /// the park-building team places bins; empty = guests always litter.
        /// </summary>
        public static readonly List<Vector3> BinPositions = new List<Vector3>();

        public const int MaxSpots = 200;
        public const float DespawnAgeMinutes = 30f;
        public const float BinRadius = 6f;

        private static readonly List<LitterSpot> _spots = new List<LitterSpot>();

        /// <summary>Read-only live registry (ParkEvents.Litter points here too).</summary>
        public static IReadOnlyList<LitterSpot> Spots => _spots;
        public static int Count => _spots.Count;

        private void Awake()
        {
            var tick = FindObjectOfType<SimTick>();
            if (tick != null) tick.Register(this);
        }

        /// <summary>Adds a spot, evicting the oldest past the cap. Fires no events (ParkEvents wraps this).</summary>
        public static LitterSpot AddSpot(Vector3 pos, LitterKind kind)
        {
            if (_spots.Count >= MaxSpots) _spots.RemoveAt(0);
            var spot = new LitterSpot { position = pos, kind = kind, ageMinutes = 0f };
            _spots.Add(spot);
            return spot;
        }

        public static bool RemoveSpot(LitterSpot spot) => spot != null && _spots.Remove(spot);

        /// <summary>Drops trash at pos unless a bin is within BinRadius. Returns true if littered.</summary>
        public static bool TryDropTrash(Vector3 pos)
        {
            if (BinNearby(pos, BinRadius)) return false;
            AddSpot(pos, LitterKind.Trash);
            return true;
        }

        /// <summary>Drops trash unconditionally (vandals don't care about bins).</summary>
        public static void DropTrash(Vector3 pos) => AddSpot(pos, LitterKind.Trash);

        public static bool BinNearby(Vector3 pos, float radius)
        {
            float r2 = radius * radius;
            foreach (Vector3 b in BinPositions)
            {
                Vector3 d = b - pos;
                d.y = 0f;
                if (d.sqrMagnitude <= r2) return true;
            }
            return false;
        }

        /// <summary>True if any litter lies within radius of pos (for disgust checks).</summary>
        public static bool NearestWithin(Vector3 pos, float radius)
        {
            float r2 = radius * radius;
            foreach (LitterSpot s in _spots)
            {
                Vector3 d = s.position - pos;
                d.y = 0f;
                if (d.sqrMagnitude <= r2) return true;
            }
            return false;
        }

        /// <summary>
        /// 0 = spotless, 1 = filthy. RCT1 feel: ~25 spots is noticeably dirty,
        /// 100+ is a health hazard. Consumed by ParkRating.cleanliness.
        /// </summary>
        public static float CleanlinessPenalty() => Mathf.Clamp01(_spots.Count / 100f);

        public void Tick(float dt)
        {
            float gdt = dt * GameClock.GameMinutesPerRealSecond;
            for (int i = _spots.Count - 1; i >= 0; i--)
            {
                LitterSpot s = _spots[i];
                s.ageMinutes += gdt;
                // Route through ParkEvents so OnLitterCleared fires and handymen drop their target.
                if (s.ageMinutes >= DespawnAgeMinutes) ParkEvents.ClearLitter(s);
            }
        }
    }
}
