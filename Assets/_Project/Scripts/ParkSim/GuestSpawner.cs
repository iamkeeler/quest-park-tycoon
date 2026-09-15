using System.Collections.Generic;
using UnityEngine;

namespace QuestParkTycoon
{
    /// <summary>
    /// Spawns guests at the park entrance. Arrival rate follows the park rating
    /// (RCT1: guests come when the rating is high) plus marketing campaigns.
    /// Guests are pooled; cap 300 active (Quest 3 perf budget, PRD §2).
    /// </summary>
    public sealed class GuestSpawner : MonoBehaviour, ISimSystem
    {
        public static GuestSpawner Instance { get; private set; }

        [Header("Layout")]
        public Vector3 gatePosition = new Vector3(0f, 0f, -30f);   // where Entering ends
        public Vector3 spawnPosition = new Vector3(0f, 0f, -36f);  // where guests appear

        [Header("Tuning")]
        public float spawnIntervalSeconds = 3f;
        public int maxGuests = 300;
        [Range(0, 6)] public int marketingLevel; // +25 guests per level (RCT1: ~+100/week per campaign)

        private readonly Queue<GuestAgent> _pool = new Queue<GuestAgent>();
        private readonly List<GuestAgent> _active = new List<GuestAgent>();
        private float _timer;
        private int _nextId = 1;
        private ParkRating _rating;

        public int ActiveCount => _active.Count;

        private void Awake()
        {
            Instance = this;
            _rating = FindObjectOfType<ParkRating>();
            var tick = FindObjectOfType<SimTick>();
            if (tick != null) tick.Register(this);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void Tick(float dt)
        {
            _timer += dt;
            if (_timer < spawnIntervalSeconds) return;
            _timer = 0f;

            int rating = _rating != null ? _rating.rating : 500;
            int target = Mathf.Clamp(Mathf.RoundToInt(rating / 999f * 150f) + marketingLevel * 25, 0, maxGuests);
            if (_active.Count < target) Spawn();
        }

        public GuestAgent Spawn()
        {
            GuestAgent g = _pool.Count > 0 ? _pool.Dequeue() : new GameObject("Guest").AddComponent<GuestAgent>();
            g.gameObject.SetActive(true);
            g.guestId = _nextId++;
            g.name = g.GuestName;

            // RCT1 min-cash rule: the entrance fee can't exceed the poorest guest's wallet,
            // or those guests simply never enter.
            float minWallet = Mathf.Max(Economy.AdmissionFee, 20f);
            g.wallet = Random.Range(minWallet, minWallet + 80f);
            g.hunger = Random.Range(0f, 0.3f);
            g.thirst = Random.Range(0f, 0.3f);
            g.nausea = Random.Range(0f, 0.1f);
            g.tiredness = Random.Range(0f, 0.2f);
            g.bladder = Random.Range(0f, 0.3f);
            g.intensityPreference = Random.Range(1f, 10f);
            g.nauseaTolerance = Random.Range(1f, 10f);
            g.happiness = Random.Range(0.6f, 0.9f);

            g.ResetForSpawn();
            g.homeExit = spawnPosition;
            g.transform.position = spawnPosition + new Vector3(Random.Range(-2f, 2f), 0f, Random.Range(-1f, 1f));
            g.transform.rotation = Quaternion.identity;

            _active.Add(g);
            GuestAI ai = FindObjectOfType<GuestAI>();
            if (ai != null)
            {
                ai.RegisterGuest(g);
                ai.SetDestination(g, gatePosition); // walk to the gate -> Entering pays admission
            }
            else
            {
                g.currentState = GuestState.Wandering;
            }
            return g;
        }

        /// <summary>Returns a guest to the pool (called when it reaches the exit).</summary>
        public void Release(GuestAgent g)
        {
            if (g == null) return;
            _active.Remove(g);
            GuestAI ai = FindObjectOfType<GuestAI>();
            if (ai != null) ai.UnregisterGuest(g);
            ThoughtSystem.ForgetGuest(g);
            g.gameObject.SetActive(false);
            if (_pool.Count < maxGuests) _pool.Enqueue(g);
            else Destroy(g.gameObject);
        }
    }
}
