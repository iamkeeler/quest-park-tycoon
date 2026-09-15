using UnityEngine;

namespace QuestParkTycoon.Rides
{
    /// <summary>
    /// RCT1 "Go Karts". Racing ride with a long cycle:
    /// E5.0 / I4.0 / N1.5, ~$700 build cost.
    /// Animation: karts rumble (bounce + yaw wobble) to suggest racing;
    /// art team places the karts as children of animatedPart.
    /// Distinct effect: driving is tiring — guests leave more tired (RCT1).
    /// </summary>
    public sealed class GoKarts : FlatRide
    {
        [Header("Go karts")]
        public float rumbleAmount = 0.05f;
        [Range(0f, 1f)] public float tirednessPerRace = 0.15f;

        protected override void Awake()
        {
            rideName = "Go Karts";
            rideType = RideType.GoKarts;
            category = RideCategory.FlatRide;
            excitement = 5f; intensity = 4f; nausea = 1.5f;
            buildCost = 700f;
            capacityPerCycle = 8;
            loadTime = 14f; runTime = 90f; unloadTime = 8f;
            ticketPrice = SuggestedTicketPrice();
            base.Awake();
        }

        protected override void Animate(float t)
        {
            if (animatedPart == null) return;
            // Engine rumble + gentle yaw sway; karts "race" without leaving the pad.
            float rumble = rumbleAmount * Mathf.Sin(Time.time * 40f);
            var p = animatedPart.localPosition;
            animatedPart.localPosition = new Vector3(p.x, rumble, p.z);
            animatedPart.Rotate(Vector3.up, 8f * Mathf.Sin(Time.time * 2f) * Time.deltaTime, Space.Self);
        }

        protected override void ApplyRideEffects(GuestAgent g)
        {
            base.ApplyRideEffects(g);
            g.tiredness = Mathf.Clamp01(g.tiredness + tirednessPerRace);
        }
    }
}
