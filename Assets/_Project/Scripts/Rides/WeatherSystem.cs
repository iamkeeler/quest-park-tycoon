using System;
using UnityEngine;

namespace QuestParkTycoon.Rides
{
    public enum WeatherState { Sunny, Cloudy, Rain }

    /// <summary>
    /// RCT1 weather on the 10 Hz sim tick: Sunny/Cloudy/Rain with temperature.
    /// Rain -> umbrella demand (Stall sells at any price) + guest happiness drain.
    /// Hot days -> water-ride popularity boost (Phase 2 hook for guest-AI team).
    /// Guest-AI team: read IsRaining / temperatureC; subscribe to OnWeatherChanged.
    /// </summary>
    public sealed class WeatherSystem : MonoBehaviour, ISimSystem
    {
        public static WeatherSystem Instance { get; private set; }

        public WeatherState state = WeatherState.Sunny;
        [Range(-5f, 40f)] public float temperatureC = 22f;

        [Header("State durations (seconds)")]
        public float minStateDuration = 90f;
        public float maxStateDuration = 240f;

        public bool IsRaining => state == WeatherState.Rain;
        /// <summary>Phase 2 hook: water rides surge on hot days (RCT1 rule).</summary>
        public bool IsHotDay => temperatureC >= 28f;

        public event Action<WeatherState> OnWeatherChanged;

        private float _stateTimeLeft;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            _stateTimeLeft = UnityEngine.Random.Range(minStateDuration, maxStateDuration);
            var tick = FindObjectOfType<SimTick>();
            if (tick != null) tick.Register(this);
        }

        public void Tick(float dt)
        {
            _stateTimeLeft -= dt;
            if (_stateTimeLeft <= 0f) RollNewWeather();
        }

        private void RollNewWeather()
        {
            float roll = UnityEngine.Random.value;
            // Weighted: mostly sunny, occasional clouds, rarer rain (RCT1 feel).
            state = roll < 0.6f ? WeatherState.Sunny
                  : roll < 0.85f ? WeatherState.Cloudy
                  : WeatherState.Rain;
            temperatureC = Mathf.Clamp(temperatureC + UnityEngine.Random.Range(-3f, 3f), 12f, 34f);
            _stateTimeLeft = UnityEngine.Random.Range(minStateDuration, maxStateDuration);
            OnWeatherChanged?.Invoke(state);
        }

        /// <summary>Happiness drain per sim-second while raining without umbrella (guest-AI team).</summary>
        public float RainHappinessDrain(GuestAgent guest) =>
            (IsRaining && !guest.hasUmbrella) ? 0.02f : 0f;
    }
}
