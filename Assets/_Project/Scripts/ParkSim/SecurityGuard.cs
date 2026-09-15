using UnityEngine;

namespace QuestParkTycoon
{
    /// <summary>
    /// RCT1 security guard ($50/mo). Patrols the assigned zone and suppresses
    /// vandalism: any vandal within suppressRadius gets calmed down
    /// (isVandal=false) with a "caught" thought. Heavy vandalism workloads
    /// erode morale — guards quit if the park stays lawless.
    /// No Meta SDK, no scene file.
    /// </summary>
    public sealed class SecurityGuard : StaffMember
    {
        [Header("Security")]
        public float suppressRadius = 10f;

        public override void WorkTick(float dt, float gdt)
        {
            TickMorale(gdt, VandalWorkload());

            if (GuestAI.Instance != null)
            {
                float r2 = suppressRadius * suppressRadius;
                foreach (GuestAgent guest in GuestAI.Instance.Guests)
                {
                    if (guest == null || !guest.isVandal) continue;
                    if (!InPatrolZone(guest.transform.position)) continue;
                    Vector3 d = guest.transform.position - transform.position;
                    d.y = 0f;
                    if (d.sqrMagnitude > r2) continue;
                    // Caught: vandal stands down.
                    guest.isVandal = false;
                    guest.vandalTimer = 0f;
                    guest.happiness = Mathf.Clamp01(guest.happiness + 0.2f);
                    ThoughtSystem.Think(guest, ThoughtType.VandalCaught);
                }
            }

            if (FollowPath(dt, walkSpeed * 0.5f)) WanderPatrol();
        }

        /// <summary>0..1 workload: 6+ active vandals park-wide is maximum stress.</summary>
        private float VandalWorkload()
        {
            if (GuestAI.Instance == null) return 0f;
            int vandals = 0;
            foreach (GuestAgent g in GuestAI.Instance.Guests)
                if (g != null && g.isVandal) vandals++;
            return Mathf.Clamp01(vandals / 6f);
        }
    }
}
