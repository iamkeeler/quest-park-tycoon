using UnityEngine;

namespace QuestParkTycoon.Rides
{
    /// <summary>
    /// RCT1 "Twist" (spinning teacups). Moderate family thrill:
    /// E5.5 / I4.5 / N4.0, ~$600 build cost.
    /// Animation: platform spins while cups counter-rotate (art team mounts
    /// cups as children of animatedPart so the wobble reads as cup spin).
    /// </summary>
    public sealed class Twist : FlatRide
    {
        [Header("Twist")]
        public float platformRevolutions = 6f;
        public float wobbleDegrees = 12f;

        protected override void Awake()
        {
            rideName = "Twist";
            rideType = RideType.Twist;
            category = RideCategory.FlatRide;
            excitement = 5.5f; intensity = 4.5f; nausea = 4f;
            buildCost = 600f;
            capacityPerCycle = 12;
            loadTime = 10f; runTime = 40f; unloadTime = 6f;
            ticketPrice = SuggestedTicketPrice();
            base.Awake();
        }

        protected override void Animate(float t)
        {
            if (animatedPart == null) return;
            // Steady platform spin plus a growing wobble that peaks mid-cycle.
            float spinRate = 360f * platformRevolutions / Mathf.Max(runTime, 0.01f);
            animatedPart.Rotate(Vector3.up, spinRate * Time.deltaTime, Space.Self);
            float wobble = wobbleDegrees * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t))
                         * Mathf.Sin(Time.time * 3f);
            var e = animatedPart.localRotation.eulerAngles;
            animatedPart.localRotation = Quaternion.Euler(wobble, e.y, e.z);
        }
    }
}
