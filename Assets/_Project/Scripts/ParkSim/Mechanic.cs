using UnityEngine;

namespace QuestParkTycoon
{
    /// <summary>
    /// RCT1 mechanic ($55-80/mo). Two jobs:
    /// 1. Inspect rides on an interval — inspections restore reliability and
    ///    catch problems; reliability decays between inspections and low
    ///    reliability can trigger breakdowns.
    /// 2. Breakdown repair — radio-dispatched by StaffManager (nearest free
    ///    mechanic), walks to the ride and enters through the ride EXIT
    ///    (RCT1 rule: the exit must be path-reachable), repairs over time,
    ///    then signals ParkEvents.RaiseRepaired.
    /// </summary>
    public sealed class Mechanic : StaffMember
    {
        [Header("Mechanic")]
        public float inspectIntervalMinutes = 10f;   // RCT1 default inspection interval
        public float reliabilityDecayPerMinute = 0.002f;
        public float breakdownReliabilityThreshold = 0.3f;
        public float breakdownChanceOnInspect = 0.15f;
        public float repairTimeMinutes = 3f;

        public bool IsBusy => _job != null;

        private IRide _job;
        private float _repairTimer;
        private float _inspectTimer;
        private bool _repairing; // false = walking to the ride, true = repairing

        private static int _decayFrame = -1; // decay once per frame even with many mechanics

        /// <summary>Radio dispatch from StaffManager. Ignored when busy.</summary>
        public void DispatchTo(IRide ride)
        {
            if (IsBusy || ride == null) return;
            _job = ride;
            _repairing = false;
            _repairTimer = 0f;
            path.Clear();
            pathIndex = 0;
        }

        public override void WorkTick(float dt, float gdt)
        {
            TickMorale(gdt, BrokenRideWorkload());
            MaintainReliability(gdt);
            if (_job != null) TickRepairJob(dt, gdt);
            else if (FollowPath(dt, walkSpeed * 0.5f)) WanderPatrol();
        }

        /// <summary>0..1 workload: 4+ broken rides at once is maximum stress.</summary>
        private float BrokenRideWorkload()
        {
            int broken = 0;
            foreach (IRide ride in RideDirectory.Rides)
                if (ride.IsBroken) broken++;
            return Mathf.Clamp01(broken / 4f);
        }

        private void MaintainReliability(float gdt)
        {
            if (_decayFrame != Time.frameCount)
            {
                _decayFrame = Time.frameCount;
                foreach (IRide ride in RideDirectory.Rides)
                    ride.Reliability = Mathf.Clamp01(ride.Reliability - reliabilityDecayPerMinute * gdt);
            }

            _inspectTimer += gdt;
            if (_inspectTimer < inspectIntervalMinutes) return;
            _inspectTimer = 0f;

            foreach (IRide ride in RideDirectory.Rides)
            {
                if (ride.IsBroken) continue;
                ride.Reliability = Mathf.Min(1f, ride.Reliability + 0.25f); // inspection catches issues
                if (ride.Reliability < breakdownReliabilityThreshold
                    && Random.value < breakdownChanceOnInspect)
                    ParkEvents.RaiseBreakdown(ride); // -> StaffManager dispatches a mechanic
            }
        }

        private void TickRepairJob(float dt, float gdt)
        {
            if (!_job.IsBroken) { _job = null; return; } // fixed by someone else

            if (!_repairing)
            {
                if (pathIndex >= path.Count) SetDestination(_job.ExitPosition);
                if (FollowPath(dt, walkSpeed)) _repairing = true;
                return;
            }

            _repairTimer += gdt;
            if (_repairTimer >= repairTimeMinutes)
            {
                ParkEvents.RaiseRepaired(_job); // sets IsBroken=false, restores reliability, fires event
                _job = null;
                _repairing = false;
                _repairTimer = 0f;
            }
        }
    }
}
