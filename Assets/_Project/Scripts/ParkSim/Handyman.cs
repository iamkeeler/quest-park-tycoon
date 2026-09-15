using UnityEngine;

namespace QuestParkTycoon
{
    /// <summary>RCT1 handyman task toggles.</summary>
    [System.Flags]
    public enum HandymanTasks
    {
        SweepPaths = 1,
        EmptyBins = 2,
        WaterGardens = 4,
        MowGrass = 8
    }

    /// <summary>
    /// RCT1 handyman ($35-50/mo): sweeps litter and vomit inside the patrol
    /// zone. Subscribes to litter events but re-scans each tick so pooled or
    /// missed events can't strand it. Bin emptying / garden watering / mowing
    /// are toggles; visuals for those arrive with the park-building team.
    /// </summary>
    public sealed class Handyman : StaffMember
    {
        [Header("Handyman")]
        public HandymanTasks tasks = HandymanTasks.SweepPaths
            | HandymanTasks.EmptyBins
            | HandymanTasks.WaterGardens
            | HandymanTasks.MowGrass;

        public float sweepTimeMinutes = 1f; // game-minutes per litter spot

        private LitterSpot _target;
        private float _sweepTimer;

        public override void WorkTick(float dt, float gdt)
        {
            // Morale: drowning in litter is miserable work (RCT1).
            TickMorale(gdt, Mathf.Clamp01(LitterSystem.Count / 60f));

            if (_target != null && !ParkEvents.Litter.Contains(_target)) _target = null;

            bool sweepOn = (tasks & HandymanTasks.SweepPaths) != 0;
            if (_target == null && sweepOn) _target = NearestLitter();

            if (_target != null)
            {
                Vector3 tp = new Vector3(_target.position.x, 0f, _target.position.z);
                Vector3 mp = new Vector3(transform.position.x, 0f, transform.position.z);
                if (Vector3.Distance(mp, tp) > 0.6f)
                {
                    if (pathIndex >= path.Count) SetDestination(tp);
                    FollowPath(dt, walkSpeed);
                }
                else
                {
                    _sweepTimer += gdt;
                    if (_sweepTimer >= sweepTimeMinutes)
                    {
                        ParkEvents.ClearLitter(_target);
                        _target = null;
                        _sweepTimer = 0f;
                    }
                }
                return;
            }

            if (FollowPath(dt, walkSpeed * 0.6f)) WanderPatrol();
            // TODO(park-building): EmptyBins needs bin entities; WaterGardens/MowGrass need garden tiles.
        }

        /// <summary>Vomit first (it tanks happiness fastest), then nearest.</summary>
        private LitterSpot NearestLitter()
        {
            LitterSpot best = null;
            float bestScore = float.MaxValue;
            foreach (LitterSpot spot in ParkEvents.Litter)
            {
                if (!InPatrolZone(spot.position)) continue;
                float d = (spot.position - transform.position).sqrMagnitude;
                float score = d + (spot.kind == LitterKind.Vomit ? -100000f : 0f);
                if (score < bestScore) { bestScore = score; best = spot; }
            }
            return best;
        }
    }
}
