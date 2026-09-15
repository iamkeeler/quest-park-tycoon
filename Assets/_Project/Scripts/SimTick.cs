using System.Collections.Generic;
using UnityEngine;

namespace QuestParkTycoon
{
    /// <summary>
    /// Implemented by every simulation system. Tick runs at a fixed rate,
    /// decoupled from the render loop (RCT1's peep AI ran on its own tick too).
    /// </summary>
    public interface ISimSystem
    {
        void Tick(float dt);
    }

    /// <summary>
    /// Fixed-timestep dispatcher: accumulates real time and fires Tick on all
    /// registered systems at 10 Hz. Render stays at 72 Hz; the sim never blocks it.
    /// </summary>
    public sealed class SimTick : MonoBehaviour
    {
        public const float TickRateHz = 10f;

        private readonly List<ISimSystem> _systems = new List<ISimSystem>();
        private float _accumulator;

        public void Register(ISimSystem system)
        {
            if (system != null && !_systems.Contains(system)) _systems.Add(system);
        }

        public void Unregister(ISimSystem system) => _systems.Remove(system);

        private void Awake()
        {
            // Phase 1 systems self-register here as they are built.
            Register(GetComponent<ParkRating>());
        }

        private void Update()
        {
            _accumulator += Time.deltaTime;
            float step = 1f / TickRateHz;
            int guard = 0; // never spiral: max 4 catch-up ticks per frame
            while (_accumulator >= step && guard++ < 4)
            {
                _accumulator -= step;
                for (int i = 0; i < _systems.Count; i++)
                    _systems[i]?.Tick(step);
            }
            if (_accumulator > step * 4f) _accumulator = 0f; // drop backlog after hitches
        }
    }
}
