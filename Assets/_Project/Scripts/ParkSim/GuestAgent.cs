using UnityEngine;

namespace QuestParkTycoon
{
    /// <summary>
    /// Data-only guest ("peep") for Phase 0. RCT1 fields, no AI yet:
    /// needs meters, wallet, intensity preference. Phase 1 adds the
    /// decision state machine (seek stalls, queue, ride, leave).
    /// </summary>
    public sealed class GuestAgent : MonoBehaviour, ISimSystem
    {
        [Header("Needs (0 = fine, 1 = desperate)")]
        [Range(0f, 1f)] public float hunger;
        [Range(0f, 1f)] public float thirst;
        [Range(0f, 1f)] public float nausea;
        [Range(0f, 1f)] public float tiredness;
        [Range(0f, 1f)] public float bladder;

        [Header("Profile")]
        public float wallet = 50f;
        [Range(0f, 10f)] public float intensityPreference = 5f;
        [Range(0f, 10f)] public float nauseaTolerance = 4f;
        [Range(0f, 1f)] public float happiness = 0.8f;

        [Header("Feedback")]
        public string lastThought = string.Empty;

        public void Tick(float dt)
        {
            // Phase 1: needs drift, stall seeking, queueing, thought bubbles.
        }
    }
}
