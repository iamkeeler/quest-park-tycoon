using UnityEngine;

namespace QuestParkTycoon.Rides
{
    /// <summary>
    /// Gentle ride. RCT1 defaults E1.5 / I0.8 / N0.3, ~$450 build cost.
    /// Animation: slow wheel rotation; art team provides the wheel mesh on animatedPart.
    /// </summary>
    public sealed class FerrisWheel : FlatRide
    {
        [Header("Ferris wheel")]
        public float revolutionsPerCycle = 2f;

        protected override void Awake()
        {
            rideName = "Ferris Wheel";
            rideType = RideType.FerrisWheel;
            category = RideCategory.FlatRide;
            excitement = 1.5f; intensity = 0.8f; nausea = 0.3f;
            buildCost = 450f;
            capacityPerCycle = 16;
            loadTime = 12f; runTime = 60f; unloadTime = 8f;
            ticketPrice = SuggestedTicketPrice();
            base.Awake();
        }

        protected override void Animate(float t)
        {
            if (animatedPart != null)
                animatedPart.Rotate(Vector3.right, 360f * revolutionsPerCycle * Time.deltaTime / Mathf.Max(runTime, 0.01f), Space.Self);
        }
    }
}
