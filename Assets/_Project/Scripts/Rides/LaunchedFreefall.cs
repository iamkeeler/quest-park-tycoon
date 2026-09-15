using UnityEngine;

namespace QuestParkTycoon.Rides
{
    /// <summary>
    /// RCT1 "Roto-Drop" / drop tower. Short, intense vertical ride:
    /// E6.0 / I8.0 / N5.0, ~$1200 build cost.
    /// Animation: carriage winches up slowly, holds at the top, then drops
    /// fast with a bounce settle (RCT1 drop-tower program).
    /// </summary>
    public sealed class LaunchedFreefall : FlatRide
    {
        [Header("Drop tower")]
        public float towerHeight = 18f;

        protected override void Awake()
        {
            rideName = "Launched Freefall";
            rideType = RideType.LaunchedFreefall;
            category = RideCategory.FlatRide;
            excitement = 6f; intensity = 8f; nausea = 5f;
            buildCost = 1200f;
            capacityPerCycle = 8;
            loadTime = 10f; runTime = 30f; unloadTime = 6f;
            ticketPrice = SuggestedTicketPrice();
            base.Awake();
        }

        protected override void Animate(float t)
        {
            if (animatedPart == null) return;
            // Program: 0-0.6 winch up, 0.6-0.7 hold, 0.7-0.8 drop, 0.8-1.0 bounce settle.
            float h;
            if (t < 0.6f)      h = Mathf.SmoothStep(0f, towerHeight, t / 0.6f);
            else if (t < 0.7f) h = towerHeight;
            else if (t < 0.8f) h = Mathf.Lerp(towerHeight, 0f, (t - 0.7f) / 0.1f);
            else               h = Mathf.Abs(Mathf.Sin((t - 0.8f) / 0.2f * Mathf.PI * 2f)) * 1.5f * (1f - (t - 0.8f) / 0.2f);
            var p = animatedPart.localPosition;
            animatedPart.localPosition = new Vector3(p.x, h, p.z);
        }
    }
}
