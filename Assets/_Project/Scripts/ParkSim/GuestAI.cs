using System.Collections.Generic;
using UnityEngine;

namespace QuestParkTycoon
{
    /// <summary>Per-guest decision states, ticked at 10 Hz by GuestAI.</summary>
    public enum GuestState
    {
        Entering, Wandering, SeekingRide, Queuing, Riding,
        SeekingStall, Eating, SeekingBathroom, Resting, Leaving
    }

    /// <summary>
    /// Owns every guest's state machine, ticked at 10 Hz via SimTick.
    /// Guests are pooled by GuestSpawner; this class only advances AI.
    /// Decision logic lives in GuestAI.Brain.cs (same partial class).
    /// VR hooks: grab-a-peep + thought bubbles read guest.currentState,
    /// guest.lastThought and ThoughtSystem.GlobalFeed().
    /// </summary>
    public sealed partial class GuestAI : MonoBehaviour, ISimSystem
    {
        public static GuestAI Instance { get; private set; }

        [Header("Tuning")]
        public float walkSpeed = 1.4f;
        public float queuePatienceMinutes = 9f; // RCT1 queue pain threshold
        public float rideSafetyTimeoutMinutes = 30f;

        public IPathGrid PathGrid { get; set; }

        private readonly List<GuestAgent> _guests = new List<GuestAgent>();
        public int ActiveGuestCount => _guests.Count;

        /// <summary>Read-only guest list for staff (guards, entertainers) and UI.</summary>
        public IReadOnlyList<GuestAgent> Guests => _guests;

        private void Awake()
        {
            Instance = this;
            if (PathGrid == null) PathGrid = new GridPathStub();
            var tick = FindObjectOfType<SimTick>();
            if (tick != null) tick.Register(this);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void RegisterGuest(GuestAgent g) { if (g != null && !_guests.Contains(g)) _guests.Add(g); }
        public void UnregisterGuest(GuestAgent g) => _guests.Remove(g);

        public void SetDestination(GuestAgent g, Vector3 dest)
        {
            g.path.Clear();
            g.pathIndex = 0;
            foreach (Vector2Int tile in PathGrid.FindPath(PathGrid.WorldToTile(g.transform.position), PathGrid.WorldToTile(dest)))
                g.path.Add(PathGrid.TileToWorld(tile));
            g.path.Add(new Vector3(dest.x, 0f, dest.z));
        }

        public void Tick(float dt)
        {
            float gdt = dt * GameClock.GameMinutesPerRealSecond;
            for (int i = _guests.Count - 1; i >= 0; i--) TickGuest(_guests[i], dt, gdt);
        }

        private void TickGuest(GuestAgent g, float dt, float gdt)
        {
            if (g == null) return;
            DriftNeeds(g, gdt);

            // Vomit: high-nausea guests puke on paths; handymen clean it up.
            if (g.nausea >= 0.9f && g.vomitCooldown <= 0f && g.currentState != GuestState.Riding)
            {
                ParkEvents.RaiseLitter(g.transform.position, LitterKind.Vomit);
                g.nausea = 0.55f;
                g.happiness = Mathf.Clamp01(g.happiness - 0.15f);
                g.vomitCooldown = 30f;
                g.lastThought = "I feel sick...";
            }
            // Ambient park-impression thoughts.
            if (Random.value < gdt * 0.004f)
            {
                if (ParkEvents.Litter.Count > 25) ThoughtSystem.Think(g, ThoughtType.DisgustingPaths);
                else if (g.happiness > 0.85f) ThoughtSystem.Think(g, ThoughtType.CleanPark);
            }
            // Disgust at litter right underfoot (probability-gated: 300 guests x 200 spots is too hot to scan raw).
            if (Random.value < 0.002f && LitterSystem.NearestWithin(g.transform.position, 4f))
            {
                ThoughtSystem.Think(g, ThoughtType.LitterDisgust);
                g.happiness = Mathf.Clamp01(g.happiness - 0.05f);
            }
            // Miserable guests litter when no bin is near (RCT1).
            if (g.happiness < 0.3f && g.currentState != GuestState.Riding && Random.value < gdt * 0.03f)
                LitterSystem.TryDropTrash(g.transform.position);

            TickVandalState(g, gdt);
            TickVandal(g, gdt);

            switch (g.currentState)
            {
                case GuestState.Entering: TickEntering(g, dt); break;
                case GuestState.Wandering: if (FollowPath(g, dt)) DecideNext(g); break;
                case GuestState.SeekingRide: TickSeekingRide(g, dt); break;
                case GuestState.Queuing: TickQueuing(g, gdt); break;
                case GuestState.Riding: TickRiding(g, gdt); break;
                case GuestState.SeekingStall: TickSeekingStall(g, dt); break;
                case GuestState.Eating:
                    g.stateTimer -= gdt;
                    if (g.stateTimer <= 0f)
                    {
                        // Finished snack: wrappers hit the ground when no bin is near (RCT1).
                        if (Random.value < 0.3f) LitterSystem.TryDropTrash(g.transform.position);
                        g.currentState = GuestState.Wandering;
                        WanderTo(g);
                    }
                    break;
                case GuestState.SeekingBathroom: TickSeekingBathroom(g, dt); break;
                case GuestState.Resting: // rest restores tiredness over time (benches: 3x — park team)
                    g.tiredness = Mathf.Clamp01(g.tiredness - gdt * 0.06f);
                    g.nausea = Mathf.Clamp01(g.nausea - gdt * 0.03f);
                    g.stateTimer -= gdt;
                    if (g.stateTimer <= 0f) { g.currentState = GuestState.Wandering; WanderTo(g); }
                    break;
                case GuestState.Leaving: TickLeaving(g, dt); break;
            }
        }

        private static void DriftNeeds(GuestAgent g, float gdt)
        {
            g.hunger = Mathf.Clamp01(g.hunger + gdt * 0.004f);
            g.thirst = Mathf.Clamp01(g.thirst + gdt * 0.005f);
            g.bladder = Mathf.Clamp01(g.bladder + gdt * 0.003f);
            // Entertainers nearby make the day feel shorter: tiredness accrues slower.
            g.tiredness = Mathf.Clamp01(g.tiredness + gdt * 0.0015f * Entertainer.TirednessGainMultiplier(g.transform.position));
            g.nausea = Mathf.Clamp01(g.nausea - gdt * 0.01f);
            g.vomitCooldown = Mathf.Max(0f, g.vomitCooldown - gdt);
            g.entertainCooldown = Mathf.Max(0f, g.entertainCooldown - gdt);
            float stress = Mathf.Max(Mathf.Max(g.hunger, g.thirst), Mathf.Max(g.bladder, g.tiredness));
            g.happiness = Mathf.MoveTowards(g.happiness, 0.85f - stress * 0.6f, gdt * 0.05f);
        }

        /// <summary>
        /// RCT1 vandalism: guests stuck below 0.25 happiness long enough turn
        /// nasty (harassment + littering). Guards calm them; happiness recovery
        /// calms them naturally.
        /// </summary>
        private void TickVandalState(GuestAgent g, float gdt)
        {
            if (!g.isVandal)
            {
                if (g.happiness < 0.25f)
                {
                    g.vandalTimer += gdt;
                    if (g.vandalTimer > 15f && Random.value < 0.35f)
                    {
                        g.isVandal = true;
                        g.vandalCooldown = 2f;
                        ThoughtSystem.Think(g, ThoughtType.VandalAngry);
                    }
                }
                else g.vandalTimer = 0f;
                return;
            }
            if (g.happiness > 0.6f)
            {
                g.isVandal = false;
                g.vandalTimer = 0f;
                ThoughtSystem.Think(g, ThoughtType.VandalCalmed);
            }
        }

        private bool FollowPath(GuestAgent g, float dt)
        {
            if (g.pathIndex >= g.path.Count) return true;
            if (AgentMovement.MoveToward(g.transform, g.path[g.pathIndex], walkSpeed, dt)) g.pathIndex++;
            return g.pathIndex >= g.path.Count;
        }

        private void TickEntering(GuestAgent g, float dt)
        {
            if (!FollowPath(g, dt)) return;
            // RCT1: guests whose wallet can't cover admission don't come in.
            if (Economy.TakePayment(g, Economy.AdmissionFee) <= 0f)
            {
                ThoughtSystem.Think(g, ThoughtType.TooExpensive, "park admission");
                Leave(g);
                return;
            }
            g.currentState = GuestState.Wandering;
        }

        private void Leave(GuestAgent g)
        {
            g.currentState = GuestState.Leaving;
            SetDestination(g, g.homeExit);
        }

        private void TickLeaving(GuestAgent g, float dt)
        {
            if (!FollowPath(g, dt)) return;
            GuestSpawner spawner = FindObjectOfType<GuestSpawner>();
            if (spawner != null) spawner.Release(g);
            else { UnregisterGuest(g); Object.Destroy(g.gameObject); }
        }

        private void WanderTo(GuestAgent g)
        {
            Vector2Int cur = PathGrid.WorldToTile(g.transform.position);
            for (int i = 0; i < 8; i++)
            {
                Vector2Int t = cur + new Vector2Int(Random.Range(-4, 5), Random.Range(-4, 5));
                if (PathGrid.IsWalkable(t)) { SetDestination(g, PathGrid.TileToWorld(t)); return; }
            }
        }
    }
}
