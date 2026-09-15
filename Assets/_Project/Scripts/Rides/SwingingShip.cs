using UnityEngine;

namespace QuestParkTycoon.Rides
{
    /// <summary>
    /// Thrill ride. RCT1 defaults E3.5 / I4.7 / N4.7, ~$387 build cost.
    /// Animation: pendulum swing that ramps up then eases off; high nausea
    /// means handymen + benches nearby are a real gameplay need (RCT1 rule).
    /// </summary>
    public sealed class SwingingShip : FlatRide
    {
        [Header("Swinging ship")]
        public float maxSwingDegrees = 60f;

        protected override void Awake()
        {
            rideName = "Swinging Ship";
            rideType = RideType.SwingingShip;
            category = RideCategory.FlatRide;
            excitement = 3.5f; intensity = 4.7f; nausea = 4.7f;
            buildCost = 387f;
            capacityPerCycle = 20;
            loadTime = 10f; runTime = 50f; unloadTime = 6f;
            ticketPrice = SuggestedTicketPrice();
            base.Awake();
        }

        protected override void Animate(float t)
        {
            if (animatedPart == null) return;
            // Envelope: ramps up over first third, full swing mid-cycle, eases off.
            float envelope = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t));
            float angle = maxSwingDegrees * envelope * Mathf.Sin(t * Mathf.PI * 10f);
            animatedPart.localRotation = Quaternion.Euler(angle, 0f, 0f);
        }
    }
}
