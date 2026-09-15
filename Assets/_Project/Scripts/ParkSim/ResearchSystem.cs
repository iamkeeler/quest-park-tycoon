using System;
using System.Collections.Generic;
using UnityEngine;
using QuestParkTycoon.Rides;

namespace QuestParkTycoon
{
    public enum ResearchCategory { ThrillRides, GentleRides, Coasters, ShopsStalls }

    /// <summary>
    /// RCT1 research: fund one of four categories at level 0-3 ($/month).
    /// Funded categories accumulate research points each game month; crossing
    /// a tier threshold unlocks new ride types. RideManager.TryPlaceRide
    /// rejects locked types — research gates what the UI build menu offers.
    /// No Meta SDK dependency; driven by the 10 Hz sim tick + GameDate.
    /// </summary>
    public sealed class ResearchSystem : MonoBehaviour, ISimSystem
    {
        public static ResearchSystem Instance { get; private set; }

        /// <summary>
        /// Ride types the player may build. Phase 1 set starts unlocked;
        /// research adds to it. Static so RideManager can gate placement
        /// even before any ResearchSystem instance exists in the scene.
        /// </summary>
        public static readonly HashSet<RideType> UnlockedRides = new HashSet<RideType>
        {
            RideType.FerrisWheel, RideType.MerryGoRound, RideType.SwingingShip,
            RideType.SteelCoaster, RideType.BurgerStall, RideType.DrinksStall,
            RideType.CottonCandyStall, RideType.InfoKiosk,
        };

        /// <summary>Safe query for UI/build gating. Unknown types default to locked.</summary>
        public static bool IsUnlocked(RideType type) => UnlockedRides.Contains(type);

        /// <summary>Monthly funding cost per level (RCT1: research is a real budget line).</summary>
        public static readonly float[] MonthlyCostByLevel = { 0f, 100f, 200f, 400f };

        /// <summary>Research points earned per funded month per level.</summary>
        public const float PointsPerLevelPerMonth = 25f;

        /// <summary>Points needed to unlock tier 1 / 2 / 3 of a category.</summary>
        public static readonly float[] TierThresholds = { 25f, 75f, 150f };

        [Header("Funding level per category (0-3)")]
        public int[] funding = new int[4]; // indexed by ResearchCategory

        private readonly float[] _points = new float[4];
        private readonly int[] _tiersUnlocked = new int[4];
        private GameDate _date;

        /// <summary>Tier unlock tables. Rides teams add future rides here via RegisterUnlock.</summary>
        private readonly Dictionary<ResearchCategory, List<RideType>[]> _unlockTable =
            new Dictionary<ResearchCategory, List<RideType>[]>
            {
                { ResearchCategory.ThrillRides, new List<RideType>[]
                    {
                        new List<RideType> { RideType.TopSpin },
                        new List<RideType> { RideType.LaunchedFreefall },
                        new List<RideType>(),
                    }
                },
                { ResearchCategory.GentleRides, new List<RideType>[]
                    {
                        new List<RideType> { RideType.Twist },
                        new List<RideType> { RideType.ObservationTower },
                        new List<RideType> { RideType.HauntedHouse, RideType.GoKarts },
                    }
                },
                { ResearchCategory.Coasters,    new List<RideType>[] { new List<RideType>(), new List<RideType>(), new List<RideType>() } },
                { ResearchCategory.ShopsStalls, new List<RideType>[]
                    {
                        new List<RideType> { RideType.FriesStall, RideType.PopcornStall },
                        new List<RideType> { RideType.PizzaStall, RideType.IceCreamStall, RideType.CoffeeStall },
                        new List<RideType> { RideType.BalloonStall, RideType.SouvenirStall, RideType.Bathroom },
                    }
                },
            };

        public event Action<ResearchCategory, int, RideType> OnResearched; // (category, tier, unlocked type)

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            _date = FindObjectOfType<GameDate>();
            if (_date != null) _date.OnNewMonth += OnNewMonth;
            var tick = FindObjectOfType<SimTick>();
            if (tick != null) tick.Register(this);
        }

        private void OnDestroy()
        {
            if (_date != null) _date.OnNewMonth -= OnNewMonth;
            if (Instance == this) Instance = null;
        }

        public void Tick(float dt) { /* monthly work happens on GameDate.OnNewMonth */ }

        /// <summary>UI team: set funding for a category (0-3). Costs apply next month.</summary>
        public void SetFunding(ResearchCategory category, int level)
        {
            funding[(int)category] = Mathf.Clamp(level, 0, 3);
        }

        public float MonthlyCost()
        {
            float total = 0f;
            for (int i = 0; i < funding.Length; i++)
                total += MonthlyCostByLevel[Mathf.Clamp(funding[i], 0, 3)];
            return total;
        }

        /// <summary>Rides team: register a future ride unlock at a category tier (1-3).</summary>
        public void RegisterUnlock(ResearchCategory category, int tier, RideType type)
        {
            if (tier < 1 || tier > 3) return;
            _unlockTable[category][tier - 1].Add(type);
        }

        private void OnNewMonth()
        {
            Economy.Cash -= MonthlyCost(); // RCT1: research is a monthly budget line
            for (int i = 0; i < 4; i++)
            {
                int level = Mathf.Clamp(funding[i], 0, 3);
                if (level == 0) continue;
                var cat = (ResearchCategory)i;
                _points[i] += level * PointsPerLevelPerMonth;
                while (_tiersUnlocked[i] < 3 && _points[i] >= TierThresholds[_tiersUnlocked[i]])
                {
                    _points[i] -= TierThresholds[_tiersUnlocked[i]];
                    _tiersUnlocked[i]++;
                    foreach (var type in _unlockTable[cat][_tiersUnlocked[i] - 1])
                    {
                        if (UnlockedRides.Add(type))
                            OnResearched?.Invoke(cat, _tiersUnlocked[i], type);
                    }
                }
            }
        }
    }
}
