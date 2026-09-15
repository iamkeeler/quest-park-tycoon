using System;
using UnityEngine;

namespace QuestParkTycoon
{
    /// <summary>
    /// Game clock: converts real seconds to RCT1-style game minutes.
    /// 1 game-minute per 2 real-seconds by default — tune to taste.
    /// Months are 30 game-days; StaffManager pays wages on month rollover.
    /// </summary>
    public static class GameClock
    {
        public static float GameMinutesPerRealSecond = 0.5f;

        public static float TotalGameMinutes { get; private set; }

        public static int Day => 1 + (int)(TotalGameMinutes / 1440f);
        public static int Month => 1 + (Day - 1) / 30;
        public static string DateString => $"Day {Day} — Month {Month}";

        public static event Action OnMonthChanged;

        private static int _lastMonth = 1;

        public static void Advance(float realDt)
        {
            TotalGameMinutes += realDt * GameMinutesPerRealSecond;
            if (Month != _lastMonth)
            {
                _lastMonth = Month;
                OnMonthChanged?.Invoke();
            }
        }
    }

    /// <summary>Advances the game clock on the 10 Hz sim tick.</summary>
    public sealed class GameClockDriver : MonoBehaviour, ISimSystem
    {
        public void Tick(float dt) => GameClock.Advance(dt);

        private void Awake()
        {
            var tick = FindObjectOfType<SimTick>();
            if (tick != null) tick.Register(this);
        }
    }
}
