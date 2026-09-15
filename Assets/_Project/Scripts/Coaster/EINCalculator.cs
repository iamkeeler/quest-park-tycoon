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
    /// Track construction style. Wooden coasters ride rougher (higher
    /// intensity/nausea) and top out at lower speeds than steel; their
    /// supports are stacked lumber bents instead of tubular steel columns
    /// (art team: swap the TrackSupport prefab per style).
    /// </summary>
    public enum CoasterStyle { Steel, Wooden }

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
        /// <param name="style">Wooden adds roughness (intensity/nausea up) and
        /// caps the speed-driven excitement term, per RCT1 wooden coasters.</param>
        public static EIN Calculate(RideStats stats, float sceneryBonus = 0f,
            CoasterStyle style = CoasterStyle.Steel)
        {
            sceneryBonus = Mathf.Clamp(sceneryBonus, 0f, 1.5f);

            // Wooden roughness: the rattle adds lateral punishment.
            float roughnessI = style == CoasterStyle.Wooden ? 1.0f : 0f;
            float roughnessN = style == CoasterStyle.Wooden ? 0.8f : 0f;

            float intensity = Mathf.Clamp(
                stats.maxG * 1.1f +
                stats.maxLateralG * 1.4f +
                stats.inversions * 0.7f +
                roughnessI, 0f, 12f);

            // Wooden coasters top out around ~85 km/h, so the speed term
            // saturates earlier; they gain a small classic-wood charm bonus.
            float speedForExcitement = style == CoasterStyle.Wooden
                ? Mathf.Min(stats.maxSpeed, 24f) : stats.maxSpeed;
            float excitement =
                Mathf.Clamp01(speedForExcitement / 28f) * 4f +   // 28 m/s ~= 100 km/h
                Mathf.Min(stats.drops * 0.55f, 2.2f) +
                Mathf.Min(stats.inversions * 1.0f, 3.0f) +
                Mathf.Min(stats.airTime * 0.6f, 1.8f) +
                Mathf.Clamp01((stats.length - 150f) / 600f) +
                (style == CoasterStyle.Wooden ? 0.3f : 0f) +
                sceneryBonus;
            // RCT1's anti-unrealistic punishment: absurd intensity kills appeal.
            if (intensity > 10f) excitement *= 0.45f;
            excitement = Mathf.Clamp(excitement, 0f, 10f);

            float nausea = Mathf.Clamp(
                stats.inversions * 1.1f +
                stats.maxLateralG * 1.6f +
                (stats.maxG > 4f ? 1.2f : 0f) +
                stats.drops * 0.15f +
                roughnessN, 0f, 10f);

            return new EIN { excitement = excitement, intensity = intensity, nausea = nausea };
        }
    }
}
