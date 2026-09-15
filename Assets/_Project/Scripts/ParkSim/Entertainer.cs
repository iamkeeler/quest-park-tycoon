using System.Collections.Generic;
using UnityEngine;

namespace QuestParkTycoon
{
    /// <summary>RCT1 entertainer costume variants (panda/tiger/elephant suits).</summary>
    public enum EntertainerCostume { Panda, Tiger, Elephant }

    /// <summary>
    /// RCT1 entertainer ($45/mo). Wanders/patrols; guests within effectRadius
    /// gain happiness slowly and accrue tiredness at half rate (see
    /// GuestAI.DriftNeeds). Most effective parked near queue tails — the
    /// radius effect does the rest. No Meta SDK, no scene file.
    /// </summary>
    public sealed class Entertainer : StaffMember
    {
        [Header("Entertainer")]
        public EntertainerCostume costume = EntertainerCostume.Panda;
        public float effectRadius = 8f;
        public float happinessPerMinute = 0.03f;

        private static readonly List<Entertainer> _active = new List<Entertainer>();

        private void Awake()
        {
            wage = 45f; // RCT1 entertainer pay band
        }

        private void OnEnable()
        {
            if (!_active.Contains(this)) _active.Add(this);
        }

        private void OnDisable() => _active.Remove(this);

        /// <summary>
        /// Tiredness-gain multiplier at a position: 0.5 near any entertainer, 1 otherwise.
        /// Called per guest per tick from GuestAI.DriftNeeds — kept allocation-free.
        /// </summary>
        public static float TirednessGainMultiplier(Vector3 pos)
        {
            foreach (Entertainer e in _active)
            {
                if (e == null || !e.isActiveAndEnabled) continue;
                Vector3 d = e.transform.position - pos;
                d.y = 0f;
                if (d.sqrMagnitude <= e.effectRadius * e.effectRadius) return 0.5f;
            }
            return 1f;
        }

        public override void WorkTick(float dt, float gdt)
        {
            if (GuestAI.Instance != null)
            {
                float r2 = effectRadius * effectRadius;
                foreach (GuestAgent guest in GuestAI.Instance.Guests)
                {
                    if (guest == null) continue;
                    Vector3 d = guest.transform.position - transform.position;
                    d.y = 0f;
                    if (d.sqrMagnitude > r2) continue;
                    guest.happiness = Mathf.Clamp01(guest.happiness + gdt * happinessPerMinute);
                    if (guest.entertainCooldown <= 0f)
                    {
                        guest.entertainCooldown = 25f; // one thought per guest per ~25 game-min
                        ThoughtSystem.Think(guest, ThoughtType.Entertained, costume.ToString());
                    }
                }
            }

            if (FollowPath(dt, walkSpeed * 0.4f)) WanderPatrol();
        }
    }
}
