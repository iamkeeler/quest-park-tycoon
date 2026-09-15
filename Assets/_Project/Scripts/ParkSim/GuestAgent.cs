using System.Collections.Generic;
using UnityEngine;

namespace QuestParkTycoon
{
    /// <summary>
    /// Data-only guest ("peep"). RCT1 fields + AI state owned by GuestAI.
    /// Rides drive it via NotifyBoarded() / NotifyRideFinished(ride).
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

        [Header("Inventory (stall purchases)")]
        public bool hasMap;      // Info Kiosk: reduces lost-guest behavior (guest-AI team)
        public bool hasUmbrella; // Info Kiosk: no rain happiness drain (WeatherSystem)

        [Header("Identity")]
        public int guestId;
        public string GuestName => $"Guest #{guestId}";

        [Header("AI state (owned by GuestAI — see GuestAI.cs)")]
        [HideInInspector] public GuestState currentState = GuestState.Entering;
        [HideInInspector] public float stateTimer;       // game-minutes left in timed states
        [HideInInspector] public float patience;         // queue patience, game-minutes
        [HideInInspector] public bool thoughtQueueComplained;
        [HideInInspector] public float vomitCooldown;    // game-minutes
        [HideInInspector] public IRide targetRide;
        [HideInInspector] public IStall targetStall;
        [HideInInspector] public Vector3 homeExit;
        [HideInInspector] public List<Vector3> path = new List<Vector3>();
        [HideInInspector] public int pathIndex;

        /// <summary>Called by the ride when this guest boards. (Rides team.)</summary>
        public void NotifyBoarded()
        {
            currentState = GuestState.Riding;
            stateTimer = 0f;
            lastThought = string.Empty;
        }

        /// <summary>Called by the ride when the ride ends. Applies nausea/happiness. (Rides team.)</summary>
        public void NotifyRideFinished(IRide ride)
        {
            if (ride != null)
            {
                float nauseaHit = ride.Nausea * (1f - nauseaTolerance / 12f);
                nausea = Mathf.Clamp01(nausea + nauseaHit * 0.35f);
                happiness = Mathf.Clamp01(happiness + ride.Excitement * 0.04f);
                ThoughtSystem.Think(this, ThoughtType.GreatRide, ride.RideName);
            }
            targetRide = null;
            currentState = GuestState.Wandering;
            stateTimer = 0f;
        }

        /// <summary>Resets AI state when (re)spawned from the pool.</summary>
        public void ResetForSpawn()
        {
            currentState = GuestState.Entering;
            stateTimer = 0f;
            patience = 0f;
            thoughtQueueComplained = false;
            vomitCooldown = 0f;
            targetRide = null;
            targetStall = null;
            path.Clear();
            pathIndex = 0;
            lastThought = string.Empty;
        }

        public void Tick(float dt)
        {
            // Driven by GuestAI (registered there), not per-guest.
        }
    }
}
