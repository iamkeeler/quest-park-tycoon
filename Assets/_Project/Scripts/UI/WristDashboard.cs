using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuestParkTycoon.UI
{
    /// <summary>
    /// Data provider for the wrist-mounted park dashboard. Deliberately UI-free:
    /// a later UI layer (Unity UI, diegetic panels) binds these public fields.
    /// No Meta SDK references — compiles anywhere.
    /// </summary>
    public sealed class WristDashboard : MonoBehaviour
    {
        [Header("Live values (bind your UI to these)")]
        public float cash;
        public float loan;
        public int parkRating;
        public int guestCount;
        public string date = string.Empty;

        [Header("Feeds (newest last)")]
        public List<string> alerts = new List<string>();
        public List<string> thoughtFeed = new List<string>();

        public int maxAlerts = 8;
        public float refreshSeconds = 0.25f;

        private ParkRating _rating;
        private float _timer;
        private Action<IRide> _onBreakdown;
        private Action<IRide> _onRepaired;

        private void Awake()
        {
            _rating = FindObjectOfType<ParkRating>();
            _onBreakdown = ride => AddAlert($"BREAKDOWN: {ride.RideName} — mechanic dispatched");
            _onRepaired = ride => AddAlert($"Repaired: {ride.RideName}");
            ParkEvents.OnRideBrokenDown += _onBreakdown;
            ParkEvents.OnRideRepaired += _onRepaired;
        }

        private void OnDestroy()
        {
            ParkEvents.OnRideBrokenDown -= _onBreakdown;
            ParkEvents.OnRideRepaired -= _onRepaired;
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer < refreshSeconds) return;
            _timer = 0f;
            Refresh();
        }

        /// <summary>Pulls fresh values; also callable manually from tests.</summary>
        public void Refresh()
        {
            cash = Economy.Cash;
            loan = Economy.Loan;
            parkRating = _rating != null ? _rating.rating : 0;
            guestCount = GuestAI.Instance != null ? GuestAI.Instance.ActiveGuestCount : 0;
            date = GameClock.DateString;

            thoughtFeed.Clear();
            thoughtFeed.AddRange(ThoughtSystem.GlobalFeed());

            if (cash < 1000f) AddAlert("Warning: cash below $1,000");
        }

        public void AddAlert(string alert)
        {
            if (string.IsNullOrEmpty(alert)) return;
            alerts.Add(alert);
            while (alerts.Count > maxAlerts) alerts.RemoveAt(0);
        }

        public void ClearAlerts() => alerts.Clear();
    }
}
