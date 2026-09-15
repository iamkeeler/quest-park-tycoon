using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuestParkTycoon
{
    /// <summary>
    /// Base class for the 4 RCT1 staff types. Staff are grabbable/relocatable
    /// (VR hook: the hand interaction calls PaintPatrolZone on tiles the
    /// player sweeps over) and work their zone on the 10 Hz sim tick.
    /// </summary>
    public abstract class StaffMember : MonoBehaviour
    {
        [Header("Staff")]
        public string staffName = "Staff";
        public float wage = 50f; // $/month. RCT1: handyman 35-50, mechanic 55-80, guard 45-60, entertainer 40-55

        [Header("Patrol zone (tile coords; empty = whole park)")]
        public List<Vector2Int> patrolZone = new List<Vector2Int>();

        public float walkSpeed = 1.6f;

        [Header("Morale (0..1; hits 0 -> quits)")]
        [Range(0f, 1f)] public float morale = 1f;

        [HideInInspector] public List<Vector3> path = new List<Vector3>();
        [HideInInspector] public int pathIndex;

        protected IPathGrid Grid =>
            GuestAI.Instance != null ? GuestAI.Instance.PathGrid : null;

        /// <summary>VR hook: hand-painting adds tiles to this staff's zone.</summary>
        public void PaintPatrolZone(Vector2Int tile)
        {
            if (!patrolZone.Contains(tile)) patrolZone.Add(tile);
        }

        public void ClearPatrolZone() => patrolZone.Clear();

        public bool InPatrolZone(Vector3 worldPos)
        {
            if (patrolZone.Count == 0 || Grid == null) return true;
            return patrolZone.Contains(Grid.WorldToTile(worldPos));
        }

        protected void SetDestination(Vector3 dest)
        {
            path.Clear();
            pathIndex = 0;
            if (Grid == null) { path.Add(new Vector3(dest.x, 0f, dest.z)); return; }
            foreach (Vector2Int tile in Grid.FindPath(Grid.WorldToTile(transform.position), Grid.WorldToTile(dest)))
                path.Add(Grid.TileToWorld(tile));
            path.Add(new Vector3(dest.x, 0f, dest.z));
        }

        protected bool FollowPath(float dt, float speed)
        {
            if (pathIndex >= path.Count) return true;
            if (AgentMovement.MoveToward(transform, path[pathIndex], speed, dt)) pathIndex++;
            return pathIndex >= path.Count;
        }

        /// <summary>Wanders to a random tile inside the patrol zone (or nearby).</summary>
        protected void WanderPatrol()
        {
            if (Grid == null) return;
            for (int i = 0; i < 8; i++)
            {
                Vector2Int t = patrolZone.Count > 0
                    ? patrolZone[UnityEngine.Random.Range(0, patrolZone.Count)]
                    : Grid.WorldToTile(transform.position) + new Vector2Int(UnityEngine.Random.Range(-4, 5), UnityEngine.Random.Range(-4, 5));
                if (Grid.IsWalkable(t)) { SetDestination(Grid.TileToWorld(t)); return; }
            }
        }

        /// <summary>Per-tick work. dt = real seconds, gdt = game-minutes.</summary>
        public abstract void WorkTick(float dt, float gdt);

        /// <summary>
        /// Call each tick with 0..1 workload. Sustained heavy workload erodes
        /// morale; at 0 the staffer quits (see StaffManager). A full day of
        /// maximum workload (~1000 game-minutes) drains morale completely.
        /// </summary>
        protected void TickMorale(float gdt, float workload01)
        {
            if (workload01 > 0.4f)
                morale = Mathf.Clamp01(morale - gdt * 0.001f * workload01);
        }

        /// <summary>Payday: wages land, morale recovers (RCT1).</summary>
        public void OnPayday() => morale = Mathf.Clamp01(morale + 0.4f);
    }

    /// <summary>
    /// Owns hiring/firing, monthly wages, and the mechanic radio-dispatch:
    /// on breakdown, the nearest free mechanic is assigned (RCT1 rule).
    /// </summary>
    public sealed class StaffManager : MonoBehaviour, ISimSystem
    {
        public static StaffManager Instance { get; private set; }

        public readonly List<StaffMember> staff = new List<StaffMember>();

        private Action<IRide> _onBreakdown;

        private void Awake()
        {
            Instance = this;
            var tick = FindObjectOfType<SimTick>();
            if (tick != null) tick.Register(this);
            _onBreakdown = DispatchMechanic;
            ParkEvents.OnRideBrokenDown += _onBreakdown;
            GameClock.OnMonthChanged += PayWages;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            ParkEvents.OnRideBrokenDown -= _onBreakdown;
            GameClock.OnMonthChanged -= PayWages;
        }

        /// <summary>Hires a staff member of type T (Handyman, Mechanic, ...).</summary>
        public T Hire<T>(string name, float wage) where T : StaffMember
        {
            var go = new GameObject(name);
            T s = go.AddComponent<T>();
            s.staffName = name;
            s.wage = wage;
            staff.Add(s);
            return s;
        }

        public void Fire(StaffMember s)
        {
            if (s == null) return;
            staff.Remove(s);
            Destroy(s.gameObject);
        }

        private void PayWages()
        {
            float total = 0f;
            foreach (StaffMember s in staff)
            {
                total += s.wage;
                s.OnPayday(); // payday restores morale (RCT1)
            }
            Economy.Cash -= total;
            Debug.Log($"[QuestParkTycoon] Monthly wages paid: ${total:F0} ({staff.Count} staff).");
        }

        public void Tick(float dt)
        {
            float gdt = dt * GameClock.GameMinutesPerRealSecond;
            for (int i = staff.Count - 1; i >= 0; i--)
            {
                StaffMember s = staff[i];
                if (s == null) { staff.RemoveAt(i); continue; }
                s.WorkTick(dt, gdt);
                // Morale hit zero: the staffer quits on the spot.
                if (s.morale <= 0f) Quit(s, i);
            }
        }

        /// <summary>Despawn + global-feed announcement when morale bottoms out.</summary>
        private void Quit(StaffMember s, int index)
        {
            staff.RemoveAt(index);
            ThoughtSystem.Announce($"{s.staffName} the {RoleName(s)} quit — morale hit rock bottom.");
            Debug.Log($"[QuestParkTycoon] {s.staffName} quit (morale 0).");
            Destroy(s.gameObject);
        }

        private static string RoleName(StaffMember s) =>
            s is Handyman ? "handyman"
            : s is Mechanic ? "mechanic"
            : s is SecurityGuard ? "security guard"
            : s is Entertainer ? "entertainer"
            : "staff";

        /// <summary>RCT1 radio dispatch: nearest free mechanic gets the job.</summary>
        private void DispatchMechanic(IRide ride)
        {
            Mechanic best = null;
            float bestDist = float.MaxValue;
            foreach (StaffMember s in staff)
            {
                if (!(s is Mechanic m) || m.IsBusy) continue;
                float d = Vector3.Distance(m.transform.position, ride.EntrancePosition);
                if (d < bestDist) { bestDist = d; best = m; }
            }
            if (best != null) best.DispatchTo(ride);
            else Debug.LogWarning($"[QuestParkTycoon] Breakdown on {ride.RideName} — no free mechanic!");
        }
    }
}
