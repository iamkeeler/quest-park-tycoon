using UnityEngine;

namespace QuestParkTycoon.Rides
{
    /// <summary>
    /// RCT1 "Top Spin". High-thrill inverting ride:
    /// E6.5 / I7.5 / N6.0, ~$1500 build cost.
    /// Animation: gondola row does periodic full flips over the run;
    /// flips cluster in the middle of the cycle (RCT1 program feel).
    /// </summary>
    public sealed class TopSpin : FlatRide
    {
        [Header("Top spin")]
        public int flipsPerCycle = 5;

        protected override void Awake()
        {
            rideName = "Top Spin";
            rideType = RideType.TopSpin;
            category = RideCategory.FlatRide;
            excitement = 6.5f; intensity = 7.5f; nausea = 6f;
            buildCost = 1500f;
            capacityPerCycle = 10;
            loadTime = 12f; runTime = 50f; unloadTime = 8f;
            ticketPrice = SuggestedTicketPrice();
            base.Awake();
        }

        protected override void Animate(float t)
        {
            if (animatedPart == null) return;
            // Flip envelope: eases in, full flips mid-cycle, eases out.
            float envelope = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t));
            float rate = 360f * flipsPerCycle * envelope / Mathf.Max(runTime, 0.01f);
            animatedPart.Rotate(Vector3.right, rate * Time.deltaTime, Space.Self);
        }
    }
}
