using System;
using UnityEngine;

namespace QuestParkTycoon.Rides
{
    public enum ObjectiveType { None, ParkValue, GuestCount, ParkRating, Profit }

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

        // Profit objective bookkeeping: cash snapshot at each month start,
        // plus how many days of that month saw rain (scenario flavor/UI).
        private float _cashAtMonthStart;
        private int _rainyDaysThisMonth;

        public float ProfitThisMonth => Economy.Cash - _cashAtMonthStart;
        public int RainyDaysThisMonth => _rainyDaysThisMonth;

        private void Awake()
        {
            _date = FindObjectOfType<GameDate>();
            _rating = FindObjectOfType<ParkRating>();
            if (_date != null)
            {
                _date.OnNewMonth += OnNewMonth;
                _date.OnNewDay += OnNewDay;
            }
            var tick = FindObjectOfType<SimTick>();
            if (tick != null) tick.Register(this);
        }

        private void OnDestroy()
        {
            if (_date != null)
            {
                _date.OnNewMonth -= OnNewMonth;
                _date.OnNewDay -= OnNewDay;
            }
        }

        private void OnNewMonth()
        {
            _cashAtMonthStart = Economy.Cash;
            _rainyDaysThisMonth = 0;
        }

        private void OnNewDay()
        {
            if (WeatherSystem.Instance != null && WeatherSystem.Instance.IsRaining)
                _rainyDaysThisMonth++;
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

        /// <summary>Scenario 2: keep the park spotless; rating follows cleanliness.</summary>
        public static ScenarioDef TidyParkChallenge() => new ScenarioDef
        {
            scenarioName = "Tidy Park Challenge",
            briefing = "Reach a park rating of 700 by the end of October, Year 2. " +
                       "Hint: place litter bins everywhere and hire handymen — guests hate a messy park.",
            objective = ObjectiveType.ParkRating,
            targetValue = 700f,
            deadlineYear = 2,
            deadlineMonthIndex = 7, // October
        };

        /// <summary>Scenario 3: build big; value follows thrilling rides.</summary>
        public static ScenarioDef CoasterCapital() => new ScenarioDef
        {
            scenarioName = "Coaster Capital",
            briefing = "Build a park worth $40,000 by the end of October, Year 2. " +
                       "Big coasters and high-excitement rides drive park value fastest.",
            objective = ObjectiveType.ParkValue,
            targetValue = 40000f,
            deadlineYear = 2,
            deadlineMonthIndex = 7, // October
        };

        /// <summary>Scenario 4: profit sprint; rain makes umbrellas print money.</summary>
        public static ScenarioDef RainySeason() => new ScenarioDef
        {
            scenarioName = "Rainy Season",
            briefing = "Earn $8,000 profit in a single month before the end of October, Year 2. " +
                       "Hint: rainy months are gold — guests buy umbrellas at any price, " +
                       "and indoor rides stay busy while the rain keeps casual visitors home.",
            objective = ObjectiveType.Profit,
            targetValue = 8000f,
            deadlineYear = 2,
            deadlineMonthIndex = 7, // October
        };

        public void StartScenario(ScenarioDef def)
        {
            active = def;
            isComplete = false;
            hasFailed = false;
            _cashAtMonthStart = Economy.Cash; // profit objective starts counting now
            _rainyDaysThisMonth = 0;
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
                // Profit: cash gained since the start of the current game month.
                ObjectiveType.Profit => ProfitThisMonth,
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
