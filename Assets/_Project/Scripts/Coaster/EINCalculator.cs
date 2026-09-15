using UnityEngine;

namespace QuestParkTycoon.Coaster
{
    /// <summary>RCT1's ride ratings, 0-10 scale. Excitement sells tickets,
    /// Intensity filters guests, Nausea fills handymen's day.</summary>
    public struct EIN
    {
        public float excitement;
        public float intensity;
        public float nausea;
    }

    /// <summary>
    /// RCT1-inspired Excitement / Intensity / Nausea heuristics from ride stats.
    /// Tuning targets from the research brief: per-ride price ~= excitement
    /// rounded down; intensity > ~10 tanks excitement; guests refuse rides far
    /// above their intensity preference; high nausea -> vomit -> litter.
    /// </summary>
    public static class EINCalculator
    {
        /// <param name="stats">From CoasterLayout.ComputeStats().</param>
        /// <param name="sceneryBonus">0-1.5: scenery near track, track threading
        /// scenery/loops, water proximity, underground segments.</param>
        public static EIN Calculate(RideStats stats, float sceneryBonus = 0f)
        {
            sceneryBonus = Mathf.Clamp(sceneryBonus, 0f, 1.5f);

            float intensity = Mathf.Clamp(
                stats.maxG * 1.1f +
                stats.maxLateralG * 1.4f +
                stats.inversions * 0.7f, 0f, 12f);

            float excitement =
                Mathf.Clamp01(stats.maxSpeed / 28f) * 4f +   // 28 m/s ~= 100 km/h
                Mathf.Min(stats.drops * 0.55f, 2.2f) +
                Mathf.Min(stats.inversions * 1.0f, 3.0f) +
                Mathf.Min(stats.airTime * 0.6f, 1.8f) +
                Mathf.Clamp01((stats.length - 150f) / 600f) +
                sceneryBonus;
            // RCT1's anti-unrealistic punishment: absurd intensity kills appeal.
            if (intensity > 10f) excitement *= 0.45f;
            excitement = Mathf.Clamp(excitement, 0f, 10f);

            float nausea = Mathf.Clamp(
                stats.inversions * 1.1f +
                stats.maxLateralG * 1.6f +
                (stats.maxG > 4f ? 1.2f : 0f) +
                stats.drops * 0.15f, 0f, 10f);

            return new EIN { excitement = excitement, intensity = intensity, nausea = nausea };
        }
    }
}
