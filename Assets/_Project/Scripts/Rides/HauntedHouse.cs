using UnityEngine;

namespace QuestParkTycoon.Rides
{
    /// <summary>
    /// RCT1 "Haunted House" (ghost train). Gentle dark ride, high throughput:
    /// E3.5 / I2.0 / N1.0, ~$950 build cost.
    /// Animation: cars creep through the dark with a spooky sway; art team
    /// places the car train as a child of animatedPart.
    /// Distinct effect: big happiness boost, barely any nausea.
    /// </summary>
    public sealed class HauntedHouse : FlatRide
    {
        [Header("Haunted house")]
        public float swayDegrees = 8f;
        [Range(0f, 1f)] public float spookBonus = 0.2f;

        protected override void Awake()
        {
            rideName = "Haunted House";
            rideType = RideType.HauntedHouse;
            category = RideCategory.FlatRide;
            excitement = 3.5f; intensity = 2f; nausea = 1f;
            buildCost = 950f;
            capacityPerCycle = 16;
            loadTime = 12f; runTime = 60f; unloadTime = 8f;
            ticketPrice = SuggestedTicketPrice();
            base.Awake();
        }

        protected override void Animate(float t)
        {
            if (animatedPart == null) return;
            // Slow spooky sway, drifting forward through the dark.
            float sway = swayDegrees * Mathf.Sin(t * Mathf.PI * 6f);
            var e = animatedPart.localRotation.eulerAngles;
            animatedPart.localRotation = Quaternion.Euler(e.x, sway, e.z);
            animatedPart.Translate(Vector3.forward * 0.4f * Time.deltaTime, Space.Self);
        }

        protected override void ApplyRideEffects(GuestAgent g)
        {
            base.ApplyRideEffects(g);
            // RCT1: dark rides delight without churning stomachs.
            g.happiness = Mathf.Clamp01(g.happiness + spookBonus);
            g.nausea = Mathf.Max(0f, g.nausea - 0.05f);
        }
    }
}
