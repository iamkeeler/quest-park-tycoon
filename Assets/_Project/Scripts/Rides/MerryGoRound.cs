using UnityEngine;

namespace QuestParkTycoon.Rides
{
    /// <summary>
    /// Gentle ride. RCT1 defaults E1.2 / I0.5 / N0.2, ~$460 build cost.
    /// Animation: platform spins; art team provides carousel mesh on animatedPart.
    /// </summary>
    public sealed class MerryGoRound : FlatRide
    {
        [Header("Merry-go-round")]
        public float revolutionsPerCycle = 4f;

        protected override void Awake()
        {
            rideName = "Merry-Go-Round";
            rideType = RideType.MerryGoRound;
            category = RideCategory.FlatRide;
            excitement = 1.2f; intensity = 0.5f; nausea = 0.2f;
            buildCost = 460f;
            capacityPerCycle = 12;
            loadTime = 8f; runTime = 40f; unloadTime = 6f;
            ticketPrice = SuggestedTicketPrice();
            base.Awake();
        }

        protected override void Animate(float t)
        {
            if (animatedPart != null)
                animatedPart.Rotate(Vector3.up, 360f * revolutionsPerCycle * Time.deltaTime / Mathf.Max(runTime, 0.01f), Space.Self);
        }
    }
}
