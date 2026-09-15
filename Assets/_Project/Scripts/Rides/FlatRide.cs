using System.Collections.Generic;
using UnityEngine;

namespace QuestParkTycoon.Rides
{
    /// <summary>
    /// Contract the guest-AI team implements against: rides expose a queue,
    /// guests join it, the ride boards whoever fits and can pay.
    /// </summary>
    public interface IQueueClient
    {
        void Enqueue(GuestAgent guest);
        GuestAgent DequeueNext();
        int QueueLength { get; }
    }

    /// <summary>
    /// Cycle-based flat ride: Load -> Run (procedural animation) -> Unload.
    /// Guests are charged on boarding; effects (nausea/happiness) apply on exit.
    /// Art team: assign animatedPart to the moving pivot; meshes arrive later.
    /// </summary>
    public abstract class FlatRide : Ride, IQueueClient, ISimSystem
    {
        public enum Phase { Idle, Loading, Running, Unloading }

        [Header("Cycle timings (seconds)")]
        public float loadTime = 10f;
        public float runTime = 45f;
        public float unloadTime = 5f;

        [Header("Animation (art team)")]
        public Transform animatedPart; // null-safe: logic runs without meshes

        [Header("Stats")]
        public int ridersServed;
        public float lifetimeRevenue;

        public Phase phase { get; private set; } = Phase.Idle;

        private readonly List<GuestAgent> _queue = new List<GuestAgent>();
        private readonly List<GuestAgent> _riders = new List<GuestAgent>();
        private float _phaseT;

        public int QueueLength => _queue.Count;

        protected override void Awake()
        {
            base.Awake(); // RideDirectory registration (guest AI finds this ride)
            var tick = FindObjectOfType<SimTick>();
            if (tick != null) tick.Register(this);
        }

        protected override int QueueCountValue => _queue.Count;
        protected override int QueueCapacityValue => capacityPerCycle * 6;
        protected override bool JoinQueue(GuestAgent guest) { Enqueue(guest); return true; }
        protected override void ExitQueue(GuestAgent guest) { _queue.Remove(guest); }

        public void Enqueue(GuestAgent guest)
        {
            if (guest == null || _queue.Contains(guest) || _riders.Contains(guest)) return;
            _queue.Add(guest);
        }

        public GuestAgent DequeueNext()
        {
            if (_queue.Count == 0) return null;
            var g = _queue[0];
            _queue.RemoveAt(0);
            return g;
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (isBroken || !isOpen) return;

            _phaseT += dt;
            switch (phase)
            {
                case Phase.Idle:
                    if (_queue.Count > 0) SetPhase(Phase.Loading);
                    break;
                case Phase.Loading:
                    BoardWaitingGuests();
                    if (_phaseT >= loadTime || _riders.Count >= capacityPerCycle)
                        SetPhase(Phase.Running);
                    break;
                case Phase.Running:
                    Animate(Mathf.Clamp01(_phaseT / runTime)); // procedural animation hook
                    if (_phaseT >= runTime) SetPhase(Phase.Unloading);
                    break;
                case Phase.Unloading:
                    if (_phaseT >= unloadTime) { ReleaseRiders(); SetPhase(Phase.Idle); }
                    break;
            }
        }

        private void SetPhase(Phase p) { phase = p; _phaseT = 0f; }

        private void BoardWaitingGuests()
        {
            while (_riders.Count < capacityPerCycle && _queue.Count > 0)
            {
                var g = DequeueNext();
                if (Economy.CanAfford(g, ticketPrice))
                {
                    Economy.TakePayment(g, ticketPrice);
                    lifetimeRevenue += ticketPrice;
                    _riders.Add(g);
                    g.NotifyBoarded(); // guest AI: state -> Riding
                }
                else
                {
                    // RCT1: guests refuse overpriced rides; thought feeds park rating.
                    g.lastThought = $"I'm not paying that much to go on {rideName}.";
                }
            }
        }

        private void ReleaseRiders()
        {
            foreach (var g in _riders)
            {
                ApplyRideEffects(g);
                g.NotifyRideFinished(this); // guest AI: nausea/happiness aftermath + wandering
            }
            ridersServed += _riders.Count;
            _riders.Clear();
        }

        /// <summary>RCT1 ride aftermath: nausea rises, happiness follows excitement.</summary>
        protected virtual void ApplyRideEffects(GuestAgent g)
        {
            g.nausea = Mathf.Clamp01(g.nausea + nausea * 0.04f);
            g.happiness = Mathf.Clamp01(g.happiness + (excitement - intensity * 0.3f) * 0.03f);
        }

        /// <summary>
        /// ProceduralAnimation hook. t is 0..1 over runTime. Drive transform
        /// params only (rotation/position of animatedPart); art team supplies meshes.
        /// </summary>
        protected abstract void Animate(float t);
    }
}
