using System;
using UnityEngine;

namespace QuestParkTycoon
{
    /// <summary>
    /// RCT1 game calendar on the 10 Hz sim tick: 8 months per year
    /// (March-October), 7 days per month. A full game year = 56 game days.
    /// Scenario deadlines (e.g. "October, Year 1") are expressed in this calendar.
    /// </summary>
    public sealed class GameDate : MonoBehaviour, ISimSystem
    {
        public static readonly string[] MonthNames =
            { "March", "April", "May", "June", "July", "August", "September", "October" };

        public const int MonthsPerYear = 8;
        public const int DaysPerMonth = 7;

        [Header("Pacing")]
        [Tooltip("Real seconds per game day. 30s => one game year is ~28 minutes.")]
        public float dayLengthSeconds = 30f;

        [Header("Current date")]
        public int Year = 1;        // 1-based
        public int MonthIndex;      // 0 = March .. 7 = October
        public int Day = 1;         // 1-based

        public event Action OnNewDay;
        public event Action OnNewMonth;

        private float _acc;

        private void Awake()
        {
            var tick = FindObjectOfType<SimTick>();
            if (tick != null) tick.Register(this);
        }

        public void Tick(float dt)
        {
            _acc += dt;
            while (_acc >= dayLengthSeconds)
            {
                _acc -= dayLengthSeconds;
                AdvanceDay();
            }
        }

        private void AdvanceDay()
        {
            Day++;
            if (Day > DaysPerMonth)
            {
                Day = 1;
                MonthIndex++;
                if (MonthIndex >= MonthsPerYear)
                {
                    MonthIndex = 0;
                    Year++;
                }
                OnNewMonth?.Invoke();
            }
            OnNewDay?.Invoke();
        }

        public string DateString => $"{MonthNames[MonthIndex]} {Day}, Year {Year}";

        public int TotalDaysElapsed =>
            ((Year - 1) * MonthsPerYear + MonthIndex) * DaysPerMonth + (Day - 1);
    }
}
