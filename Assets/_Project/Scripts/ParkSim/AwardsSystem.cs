using System;
using System.Collections.Generic;
using UnityEngine;
using QuestParkTycoon.Rides;

namespace QuestParkTycoon
{
    public enum ParkAward { SafestPark, TidiestPark, BestValue }

    /// <summary>
    /// RCT1 monthly park awards, evaluated on GameDate.OnNewMonth:
    /// - Safest Park: zero breakdowns that month AND average ride reliability >= 75%
    /// - Tidiest Park: little litter lying around (<= 10 spots on the ground)
    /// - Best Value: average ticket price at or below the RCT1 suggested price
    /// Winning any award: +50 park rating for the next month and arrivals x1.2.
    /// Everything is null-safe: missing systems simply disqualify that award.
    /// </summary>
    public sealed class AwardsSystem : MonoBehaviour, ISimSystem
    {
        public static AwardsSystem Instance { get; private set; }

        /// <summary>Award spawn bonus (guest-AI team reads via GuestSpawner).</summary>
        public static float AwardSpawnMultiplier = 1f;

        public const float RatingBonus = 50f;
        public const int MaxLitterForTidy = 10;
        public const float MinReliabilityForSafe = 0.75f;

        public readonly List<ParkAward> lastMonthWinners = new List<ParkAward>();

        private int _breakdownsThisMonth;
        private bool _ratingBonusActive;
        private GameDate _date;
        private ParkRating _rating;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            _date = FindObjectOfType<GameDate>();
            if (_date != null) _date.OnNewMonth += Evaluate;
            _rating = FindObjectOfType<ParkRating>();
            ParkEvents.OnRideBrokenDown += OnBreakdown;
            var tick = FindObjectOfType<SimTick>();
            if (tick != null) tick.Register(this);
        }

        private void OnDestroy()
        {
            if (_date != null) _date.OnNewMonth -= Evaluate;
            ParkEvents.OnRideBrokenDown -= OnBreakdown;
            if (Instance == this) Instance = null;
        }

        public void Tick(float dt) { /* evaluation happens on GameDate.OnNewMonth */ }

        private void OnBreakdown(IRide ride) => _breakdownsThisMonth++;

        private void Evaluate()
        {
            // Revert last month's bonus before judging this month.
            if (_ratingBonusActive)
            {
                if (_rating != null) _rating.rating = Mathf.Max(0, _rating.rating - (int)RatingBonus);
                _ratingBonusActive = false;
            }
            AwardSpawnMultiplier = 1f;

            lastMonthWinners.Clear();
            var rides = RideDirectory.Rides;

            // Safest Park: no breakdowns + high average reliability.
            if (rides.Count > 0 && _breakdownsThisMonth == 0)
            {
                float sum = 0f;
                foreach (var r in rides) sum += r.Reliability;
                if (sum / rides.Count >= MinReliabilityForSafe)
                    lastMonthWinners.Add(ParkAward.SafestPark);
            }

            // Tidiest Park: cleanliness penalty near zero (<= 10 litter spots).
            // LitterSystem is the staff team's source of truth; ParkEvents.Litter
            // is the fallback if it ever goes missing (both are static, no reflection).
            float litterPenalty;
            try { litterPenalty = LitterSystem.CleanlinessPenalty(); }
            catch { litterPenalty = Mathf.Clamp01(ParkEvents.Litter.Count / 100f); }
            if (litterPenalty * 100f <= MaxLitterForTidy)
                lastMonthWinners.Add(ParkAward.TidiestPark);

            // Best Value: average ticket price <= average RCT1 suggested price.
            float priceSum = 0f, suggestedSum = 0f;
            int priced = 0;
            foreach (var r in rides)
            {
                if (r.TicketPrice <= 0f) continue; // free rides don't count
                priceSum += r.TicketPrice;
                suggestedSum += Economy.SuggestedRidePrice(r.Excitement, r.Intensity);
                priced++;
            }
            if (priced > 0 && priceSum / priced <= suggestedSum / priced)
                lastMonthWinners.Add(ParkAward.BestValue);

            _breakdownsThisMonth = 0;

            if (lastMonthWinners.Count > 0)
            {
                if (_rating != null)
                {
                    _rating.rating = Mathf.Min(999, _rating.rating + (int)RatingBonus);
                    _ratingBonusActive = true;
                }
                AwardSpawnMultiplier = 1.2f;
            }
            GuestSpawner.RecalcSpawnRateMultiplier();
        }
    }
}
