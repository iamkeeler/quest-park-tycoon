using UnityEngine;

namespace QuestParkTycoon
{
    /// <summary>
    /// Park rating 0–999 (RCT1 scale). Phase 0: accumulator stub with the
    /// contributor channels defined; Phase 1 wires real inputs (ride variety,
    /// cleanliness, guest happiness) and the 600-point scenario threshold.
    /// </summary>
    public sealed class ParkRating : MonoBehaviour, ISimSystem
    {
        [Range(0, 999)] public int rating = 500;

        [Header("Contributors (0..1 each, Phase 1 fills these)")]
        [Range(0f, 1f)] public float rideVariety = 0.5f;
        [Range(0f, 1f)] public float rideQuality = 0.5f;
        [Range(0f, 1f)] public float cleanliness = 0.5f;
        [Range(0f, 1f)] public float guestHappiness = 0.5f;
        [Range(0f, 1f)] public float scenery = 0.5f;

        public void Tick(float dt)
        {
            // Cleanliness is driven live by the litter registry (RCT1: dirty paths tank the rating).
            cleanliness = 1f - LitterSystem.CleanlinessPenalty();
            float avg = (rideVariety + rideQuality + cleanliness + guestHappiness + scenery) / 5f;
            rating = Mathf.Clamp(Mathf.RoundToInt(avg * 999f), 0, 999);
        }
    }
}
