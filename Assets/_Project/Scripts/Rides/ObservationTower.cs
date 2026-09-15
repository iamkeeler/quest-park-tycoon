using UnityEngine;

namespace QuestParkTycoon.Rides
{
    /// <summary>
    /// RCT1 "Observation Tower". Gentle scenic ride, highest throughput:
    /// E2.5 / I0.5 / N0.5, ~$800 build cost.
    /// Animation: gondola rises slowly, rotates for the view, descends.
    /// Distinct effect: restful — guests leave less tired (RCT1 scenic rides
    /// calm guests down rather than wearing them out).
    /// </summary>
    public sealed class ObservationTower : FlatRide
    {
        [Header("Observation tower")]
        public float towerHeight = 25f;
        [Range(0f, 1f)] public float restAmount = 0.3f;

        protected override void Awake()
        {
            rideName = "Observation Tower";
            rideType = RideType.ObservationTower;
            category = RideCategory.FlatRide;
            excitement = 2.5f; intensity = 0.5f; nausea = 0.5f;
            buildCost = 800f;
            capacityPerCycle = 20;
            loadTime = 14f; runTime = 75f; unloadTime = 10f;
            ticketPrice = SuggestedTicketPrice();
            base.Awake();
        }

        protected override void Animate(float t)
        {
            if (animatedPart == null) return;
            // Slow rise (0-0.3), rotate at the top (0.3-0.7), slow descent (0.7-1.0).
            float h;
            if (t < 0.3f)      h = Mathf.SmoothStep(0f, towerHeight, t / 0.3f);
            else if (t < 0.7f) h = towerHeight;
            else               h = Mathf.SmoothStep(towerHeight, 0f, (t - 0.7f) / 0.3f);
            var p = animatedPart.localPosition;
            animatedPart.localPosition = new Vector3(p.x, h, p.z);
            if (t >= 0.3f && t < 0.7f)
                animatedPart.Rotate(Vector3.up, 30f * Time.deltaTime, Space.Self);
        }

        protected override void ApplyRideEffects(GuestAgent g)
        {
            base.ApplyRideEffects(g);
            g.tiredness = Mathf.Clamp01(g.tiredness - restAmount);
        }
    }
}
