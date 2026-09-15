using System;
using UnityEngine;

namespace QuestParkTycoon.Rides
{
    public enum ObjectiveType { None, ParkValue, GuestCount, ParkRating }

    [Serializable]
    public class ScenarioDef
    {
        public string scenarioName = "Scenario";
        [TextArea] public string briefing = "";
        public ObjectiveType objective = ObjectiveType.None;
        public float targetValue;
        public int deadlineYear = 1;        // 1-based game year
        public int deadlineMonthIndex = 7;  // 0=March .. 7=October
    }

    /// <summary>
    /// Scenario objectives evaluated on the sim tick (RCT1 structure):
    /// sandbox has no objective; Greenfield Challenge = $15k park value
    /// within 1 game year. Win/Lose events for the UI team to surface.
    /// </summary>
    public sealed class ScenarioManager : MonoBehaviour, ISimSystem
    {
        public ScenarioDef active;

        public bool isComplete { get; private set; }
        public bool hasFailed { get; private set; }

        public event Action<ScenarioDef> OnWin;
        public event Action<ScenarioDef> OnLose;

        private GameDate _date;
        private ParkRating _rating;

        private void Awake()
        {
            _date = FindObjectOfType<GameDate>();
            _rating = FindObjectOfType<ParkRating>();
            var tick = FindObjectOfType<SimTick>();
            if (tick != null) tick.Register(this);
        }

        public static ScenarioDef SandboxScenario() => new ScenarioDef
        {
            scenarioName = "Sandbox",
            briefing = "No objectives. Build the park of your dreams.",
            objective = ObjectiveType.None,
        };

        /// <summary>Scenario 1 (Gary-approved): sandbox-style start, one value goal.</summary>
        public static ScenarioDef GreenfieldChallenge() => new ScenarioDef
        {
            scenarioName = "Greenfield Challenge",
            briefing = "Grow this empty greenfield into a park worth $15,000 before the end of October, Year 1.",
            objective = ObjectiveType.ParkValue,
            targetValue = 15000f,
            deadlineYear = 1,
            deadlineMonthIndex = 7, // October
        };

        public void StartScenario(ScenarioDef def)
        {
            active = def;
            isComplete = false;
            hasFailed = false;
        }

        public void Tick(float dt)
        {
            if (active == null || active.objective == ObjectiveType.None) return;
            if (isComplete || hasFailed) return;

            if (ObjectiveMet())
            {
                isComplete = true;
                OnWin?.Invoke(active);
                return;
            }
            if (PastDeadline())
            {
                hasFailed = true;
                OnLose?.Invoke(active);
            }
        }

        public float CurrentProgress()
        {
            if (active == null) return 0f;
            return active.objective switch
            {
                ObjectiveType.ParkValue => RideManager.Instance != null ? RideManager.Instance.ParkValue() : 0f,
                ObjectiveType.ParkRating => _rating != null ? _rating.rating : 0f,
                // GuestCount: wired to the live guest registry (GuestSpawner).
                ObjectiveType.GuestCount =>
                    GuestSpawner.Instance != null ? GuestSpawner.Instance.ActiveCount : 0f,
                _ => 0f,
            };
        }

        private bool ObjectiveMet() => CurrentProgress() >= active.targetValue;

        private bool PastDeadline()
        {
            if (_date == null) return false;
            if (_date.Year > active.deadlineYear) return true;
            return _date.Year == active.deadlineYear && _date.MonthIndex > active.deadlineMonthIndex;
        }
    }
}
