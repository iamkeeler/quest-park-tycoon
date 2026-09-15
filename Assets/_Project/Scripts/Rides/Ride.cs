using System;
using UnityEngine;

namespace QuestParkTycoon.Rides
{
    public enum RideCategory { FlatRide, Coaster, Stall }

    /// <summary>
    /// MVP ride types. SteelCoaster is the Phase 2 free-build coaster hook;
    /// stalls reuse the Ride base so pricing, value and reliability flow through
    /// the same economy code.
    /// </summary>
    public enum RideType
    {
        FerrisWheel, MerryGoRound, SwingingShip, SteelCoaster,
        BurgerStall, DrinksStall, CottonCandyStall, InfoKiosk,
        FriesStall, PizzaStall, IceCreamStall, PopcornStall,
        CoffeeStall, BalloonStall, SouvenirStall, Bathroom,
        Twist, TopSpin, GoKarts, LaunchedFreefall, HauntedHouse, ObservationTower
    }

    /// <summary>
    /// Abstract base for everything buildable: rides, coasters and stalls.
    /// Reliability decays with use/age (RCT1); mechanics Inspect() to top it
    /// up and Repair() after a breakdown. No Meta SDK dependency.
    /// </summary>
    public abstract class Ride : MonoBehaviour, IRide
    {
        [Header("Identity")]
        public string rideName = "Ride";
        public RideType rideType;
        public RideCategory category = RideCategory.FlatRide;

        [Header("RCT1 ratings (0-10)")]
        [Range(0f, 10f)] public float excitement = 3f;
        [Range(0f, 10f)] public float intensity = 3f;
        [Range(0f, 10f)] public float nausea = 1f;

        [Header("Economy")]
        public float buildCost = 500f;
        public float ticketPrice = 2f;

        [Header("Operation")]
        public bool isOpen;
        public int capacityPerCycle = 8;
        public Transform queueEntrance; // guests queue here (queue path attaches)
        public Transform exitPoint;     // mechanics enter here on breakdown (RCT1 rule)

        [Header("Reliability (0-100)")]
        [Range(0f, 100f)] public float reliability = 100f;
        public bool isBroken;

        public event Action<Ride> OnBreakdown;
        public event Action<Ride> OnRepaired;

        private static int _nextRideId = 1;
        public int RideId { get; private set; }

        /// <summary>
        /// Registers with the guest-AI-facing RideDirectory.
        /// Subclasses overriding Awake MUST call base.Awake().
        /// </summary>
        protected virtual void Awake()
        {
            RideId = _nextRideId++;
            RideDirectory.Register(this);
        }

        protected virtual void OnDestroy() => RideDirectory.Unregister(this);

        // ---------------- IRide (guest-AI contract) ----------------
        string IRide.RideName => rideName;
        float IRide.Excitement => excitement;
        float IRide.Intensity => intensity;
        float IRide.Nausea => nausea;
        float IRide.TicketPrice { get => ticketPrice; set => ticketPrice = value; }
        bool IRide.IsOpen => isOpen;
        bool IRide.IsBroken => isBroken;
        float IRide.Reliability
        {
            get => reliability / 100f;
            set => reliability = Mathf.Clamp01(value) * 100f;
        }
        Vector3 IRide.EntrancePosition =>
            queueEntrance != null ? queueEntrance.position : transform.position;
        Vector3 IRide.ExitPosition =>
            exitPoint != null ? exitPoint.position : transform.position;
        int IRide.QueueCount => QueueCountValue;
        int IRide.QueueCapacity => QueueCapacityValue;
        bool IRide.TryJoinQueue(GuestAgent guest) => JoinQueue(guest);
        void IRide.LeaveQueue(GuestAgent guest) => ExitQueue(guest);
        void IRide.SetBroken(bool broken)
        {
            if (broken && !isBroken) BreakDown();
            else if (!broken && isBroken) Repair();
        }

        /// <summary>Queue hooks for FlatRide to override; base rides have no queue.</summary>
        protected virtual int QueueCountValue => 0;
        protected virtual int QueueCapacityValue => 0;
        protected virtual bool JoinQueue(GuestAgent guest) => false;
        protected virtual void ExitQueue(GuestAgent guest) { }

        /// <summary>Mechanic preventive maintenance: 10-min inspection interval (RCT1).</summary>
        public virtual void Inspect()
        {
            reliability = Mathf.Min(100f, reliability + 15f);
        }

        /// <summary>Mechanic repair after breakdown. Exit must be path-reachable (RCT1).</summary>
        public virtual void Repair()
        {
            if (!isBroken) return;
            isBroken = false;
            reliability = Mathf.Max(reliability, 60f);
            OnRepaired?.Invoke(this);
        }

        public virtual void Open() => isOpen = true;
        public virtual void Close() => isOpen = false;

        public virtual void Tick(float dt)
        {
            if (isBroken || !isOpen) return;
            TickReliability(dt);
        }

        protected virtual void TickReliability(float dt)
        {
            // RCT1: reliability decays with use and age; breakdown chance
            // scales once reliability drops below ~30.
            reliability = Mathf.Max(0f, reliability - dt * 0.05f);
            if (reliability < 30f &&
                UnityEngine.Random.value < dt * 0.01f * (30f - reliability))
            {
                BreakDown();
            }
        }

        protected virtual void BreakDown()
        {
            isBroken = true;
            OnBreakdown?.Invoke(this); // staff team: dispatch nearest mechanic here
        }

        /// <summary>RCT1 willingness-to-pay: ~excitement rounded down, adjusted by (I-E)/10.</summary>
        public float SuggestedTicketPrice() => Economy.SuggestedRidePrice(excitement, intensity);

        /// <summary>Park-value contribution: build cost x duplicate penalty x condition.</summary>
        public virtual float CurrentValue(float duplicatePenalty) =>
            buildCost * duplicatePenalty * (reliability / 100f);
    }
}
