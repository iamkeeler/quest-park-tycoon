using UnityEngine;

namespace QuestParkTycoon
{
    /// <summary>
    /// Ensures every Phase-1 sim system exists exactly once on a "ParkSim" root.
    /// Called at the end of SpikeBootstrap.Boot (after SimTick exists) so each
    /// system's Awake can self-register with SimTick; registration is also
    /// forced here to be robust against script-execution-order surprises.
    /// No Meta SDK, no scene file — pure runtime wiring.
    /// </summary>
    public static class ParkSimBootstrap
    {
        public static GameObject EnsureParkSystems()
        {
            GameObject root = GameObject.Find("ParkSim") ?? new GameObject("ParkSim");
            Object.DontDestroyOnLoad(root);

            Ensure<GameClockDriver>(root);
            Ensure<ParkRating>(root);
            Ensure<GuestAI>(root);
            Ensure<GuestSpawner>(root);
            Ensure<StaffManager>(root);

            var tick = Object.FindObjectOfType<SimTick>();
            if (tick != null)
                foreach (ISimSystem s in root.GetComponents<ISimSystem>())
                    tick.Register(s);

            Debug.Log("[QuestParkTycoon] Park sim systems ready.");
            return root;
        }

        private static T Ensure<T>(GameObject root) where T : Component =>
            root.GetComponent<T>() ?? root.AddComponent<T>();
    }
}
