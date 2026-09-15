using System;
using System.Collections.Generic;
using UnityEngine;
using QuestParkTycoon.Rides;

namespace QuestParkTycoon
{
    /// <summary>
    /// Guest decision-making: the "brain" half of the GuestAI partial class.
    /// Ride choice = intensity-preference match + wallet check (RCT1 rules).
    /// </summary>
    public sealed partial class GuestAI
    {
        private void DecideNext(GuestAgent g)
        {
            if (g.wallet < 1f || g.happiness < 0.15f) { Leave(g); return; } // broke/miserable go home
            // Phase 2: bathrooms are real buildings — seek the nearest free one.
            if (g.bladder > 0.65f) { SeekStall(g, new[] { StallType.Bathroom }, ThoughtType.NeedBathroom, true); return; }
            if (g.hunger > 0.65f) { SeekStall(g, new[] { StallType.Burger, StallType.CottonCandy, StallType.Fries, StallType.Pizza, StallType.Popcorn, StallType.IceCream }, ThoughtType.Hungry, false); return; }
            if (g.thirst > 0.65f) { SeekStall(g, new[] { StallType.Drinks, StallType.Coffee }, ThoughtType.Thirsty, false); return; }
            // Tired guests rest eagerly; rest duration scales with exhaustion.
            if (g.tiredness > 0.75f)
            {
                ThoughtSystem.Think(g, ThoughtType.Tired);
                g.currentState = GuestState.Resting;
                g.stateTimer = 2f + g.tiredness * 8f;
                return;
            }
            if (Random.value < 0.55f && ChooseRide(g) != null) { g.currentState = GuestState.SeekingRide; return; }
            WanderTo(g);
        }

        private void SeekStall(GuestAgent g, IEnumerable<StallType> types, ThoughtType thought, bool bathroom)
        {
            ThoughtSystem.Think(g, thought);
            g.targetStall = StallDirectory.FindNearestAny(types, g.transform.position);
            g.currentState = bathroom ? GuestState.SeekingBathroom : GuestState.SeekingStall;
            if (g.targetStall != null) SetDestination(g, g.targetStall.Position);
        }

        private IRide ChooseRide(GuestAgent g)
        {
            IRide best = null;
            float bestScore = -1f;
            foreach (IRide ride in RideDirectory.Rides)
            {
                if (!ride.IsOpen || ride.IsBroken || ride.QueueCount >= ride.QueueCapacity) continue;
                if (!Economy.CanAfford(g, ride.TicketPrice)) continue;
                float score = (1f - Mathf.Abs(ride.Intensity - g.intensityPreference) / 10f) * 0.7f
                            + (ride.Excitement / 10f) * 0.3f;
                if (score > bestScore) { bestScore = score; best = ride; }
            }
            g.targetRide = best;
            return best;
        }

        private void TickSeekingRide(GuestAgent g, float dt)
        {
            if (g.targetRide == null || !g.targetRide.IsOpen || g.targetRide.IsBroken)
                if (ChooseRide(g) == null) { g.currentState = GuestState.Wandering; WanderTo(g); return; }
            if (!FollowPath(g, dt)) return;
            if (g.targetRide.TryJoinQueue(g))
            {
                g.currentState = GuestState.Queuing;
                g.patience = queuePatienceMinutes;
                g.thoughtQueueComplained = false;
            }
            else { g.currentState = GuestState.Wandering; WanderTo(g); }
        }

        private void TickQueuing(GuestAgent g, float gdt)
        {
            g.patience -= gdt;
            if (g.patience < 3f && !g.thoughtQueueComplained)
            {
                g.thoughtQueueComplained = true;
                ThoughtSystem.Think(g, ThoughtType.QueuingTooLong, g.targetRide != null ? g.targetRide.RideName : "this ride");
                g.happiness = Mathf.Clamp01(g.happiness - 0.1f);
            }
            if (g.patience > 0f) return;
            g.targetRide?.LeaveQueue(g);
            ThoughtSystem.Think(g, ThoughtType.QueuingTooLong, g.targetRide != null ? g.targetRide.RideName : "this ride");
            g.targetRide = null;
            g.currentState = GuestState.Wandering;
            WanderTo(g);
            // The ride advances the queue by calling guest.NotifyBoarded().
        }

        private void TickRiding(GuestAgent g, float gdt)
        {
            g.stateTimer += gdt;
            // Safety net: a ride that never calls NotifyRideFinished can't trap a guest.
            if (g.stateTimer > rideSafetyTimeoutMinutes && g.targetRide != null)
                g.NotifyRideFinished(g.targetRide);
        }

        private void TickSeekingStall(GuestAgent g, float dt)
        {
            if (g.targetStall == null || !g.targetStall.IsOpen) { g.currentState = GuestState.Wandering; WanderTo(g); return; }
            if (!FollowPath(g, dt)) return;
            // IStall.Serve charges the guest and applies hunger/thirst effects.
            if (g.targetStall.Serve(g)) { g.currentState = GuestState.Eating; g.stateTimer = 2f; }
            else
            {
                ThoughtSystem.Think(g, ThoughtType.TooExpensive, g.targetStall.StallName);
                g.currentState = GuestState.Wandering;
                WanderTo(g);
            }
            g.targetStall = null;
        }

        private void TickSeekingBathroom(GuestAgent g, float dt)
        {
            if (g.targetStall == null || !g.targetStall.IsOpen)
            {
                // No bathroom in the park: guests suffer (RCT1 "I need the bathroom").
                g.happiness = Mathf.Clamp01(g.happiness - 0.2f);
                ThoughtSystem.Think(g, ThoughtType.NeedBathroom);
                g.currentState = GuestState.Wandering;
                WanderTo(g);
                return;
            }
            if (!FollowPath(g, dt)) return;
            // Bathroom stalls are free service buildings; Serve() resets bladder.
            if (g.targetStall.Serve(g))
            {
                g.bladder = 0f; // belt-and-suspenders; the stall owns the real effect
                g.happiness = Mathf.Clamp01(g.happiness + 0.15f);
                ThoughtSystem.Think(g, ThoughtType.BathroomRelief);
            }
            else ThoughtSystem.Think(g, ThoughtType.NeedBathroom);
            g.targetStall = null;
            g.currentState = GuestState.Wandering;
            WanderTo(g);
        }

        /// <summary>
        /// Vandal mischief, ticked alongside the state machine: harass a random
        /// nearby guest and trash the paths. Security guards suppress this in
        /// their own WorkTick (see SecurityGuard).
        /// </summary>
        private void TickVandal(GuestAgent g, float gdt)
        {
            if (!g.isVandal) return;
            g.vandalCooldown -= gdt;
            if (g.vandalCooldown > 0f) return;
            g.vandalCooldown = 4f; // one nasty act every ~4 game-minutes

            GuestAgent victim = RandomGuestNear(g.transform.position, 6f, g);
            if (victim != null)
            {
                victim.happiness = Mathf.Clamp01(victim.happiness - 0.15f);
                ThoughtSystem.Think(g, ThoughtType.VandalAngry);
            }
            LitterSystem.DropTrash(g.transform.position); // vandals ignore bins
        }

        private GuestAgent RandomGuestNear(Vector3 pos, float radius, GuestAgent exclude)
        {
            GuestAgent pick = null;
            int seen = 0;
            float r2 = radius * radius;
            foreach (GuestAgent other in _guests)
            {
                if (other == null || other == exclude) continue;
                Vector3 d = other.transform.position - pos;
                d.y = 0f;
                if (d.sqrMagnitude > r2) continue;
                seen++;
                if (Random.value < 1f / seen) pick = other; // reservoir: uniform among nearby
            }
            return pick;
        }
    }
}
