using System;
using UnityEngine;

namespace QuestParkTycoon
{
    public enum MarketingCampaign { None, FreeEntryDay, RadioAd, TVAd }

    /// <summary>
    /// RCT1 marketing campaigns, bought with Cash from the UI:
    /// - Free Entry Day ($500): admission free for 1 day, arrivals x2.5
    /// - Radio Ad ($300): arrivals x1.5 for 7 days
    /// - TV Ad ($800): arrivals x2.0 for 7 days
    /// Multipliers stack multiplicatively and combine with the Awards
    /// multiplier; GuestSpawner reads the combined SpawnRateMultiplier.
    /// No Meta SDK dependency; durations tick on GameDate.OnNewDay.
    /// </summary>
    public sealed class MarketingSystem : MonoBehaviour, ISimSystem
    {
        public static MarketingSystem Instance { get; private set; }

        /// <summary>Combined campaign multiplier (guest-AI team reads via GuestSpawner).</summary>
        public static float CampaignSpawnMultiplier = 1f;

        public const float FreeEntryDayCost = 500f;
        public const float RadioAdCost = 300f;
        public const float TVAdCost = 800f;
        public const int AdDurationDays = 7;

        private int _freeEntryDaysLeft;
        private int _radioDaysLeft;
        private int _tvDaysLeft;
        private float _savedAdmissionFee;
        private GameDate _date;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            _date = FindObjectOfType<GameDate>();
            if (_date != null) _date.OnNewDay += OnNewDay;
            var tick = FindObjectOfType<SimTick>();
            if (tick != null) tick.Register(this);
        }

        private void OnDestroy()
        {
            if (_date != null) _date.OnNewDay -= OnNewDay;
            if (Instance == this) Instance = null;
        }

        public void Tick(float dt) { /* durations tick on GameDate.OnNewDay */ }

        public bool IsActive(MarketingCampaign campaign) => campaign switch
        {
            MarketingCampaign.FreeEntryDay => _freeEntryDaysLeft > 0,
            MarketingCampaign.RadioAd => _radioDaysLeft > 0,
            MarketingCampaign.TVAd => _tvDaysLeft > 0,
            _ => false,
        };

        /// <summary>UI team: buy and launch a campaign. Returns false if unaffordable.</summary>
        public bool LaunchCampaign(MarketingCampaign campaign)
        {
            float cost = campaign switch
            {
                MarketingCampaign.FreeEntryDay => FreeEntryDayCost,
                MarketingCampaign.RadioAd => RadioAdCost,
                MarketingCampaign.TVAd => TVAdCost,
                _ => 0f,
            };
            if (cost <= 0f || IsActive(campaign)) return false;
            if (Economy.Cash < cost) return false;
            Economy.Cash -= cost;

            switch (campaign)
            {
                case MarketingCampaign.FreeEntryDay:
                    _savedAdmissionFee = Economy.AdmissionFee;
                    Economy.AdmissionFee = 0f; // RCT1: free entry day packs the park
                    _freeEntryDaysLeft = 1;
                    break;
                case MarketingCampaign.RadioAd:
                    _radioDaysLeft = AdDurationDays;
                    break;
                case MarketingCampaign.TVAd:
                    _tvDaysLeft = AdDurationDays;
                    break;
            }
            RecalcMultiplier();
            return true;
        }

        private void OnNewDay()
        {
            if (_freeEntryDaysLeft > 0 && --_freeEntryDaysLeft == 0)
                Economy.AdmissionFee = _savedAdmissionFee; // restore the normal fee
            if (_radioDaysLeft > 0) _radioDaysLeft--;
            if (_tvDaysLeft > 0) _tvDaysLeft--;
            RecalcMultiplier();
        }

        private void RecalcMultiplier()
        {
            CampaignSpawnMultiplier =
                (_freeEntryDaysLeft > 0 ? 2.5f : 1f) *
                (_radioDaysLeft > 0 ? 1.5f : 1f) *
                (_tvDaysLeft > 0 ? 2.0f : 1f);
            GuestSpawner.RecalcSpawnRateMultiplier();
        }
    }
}
