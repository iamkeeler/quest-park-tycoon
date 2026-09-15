using System;
using UnityEngine;

namespace QuestParkTycoon.Coaster
{
    /// <summary>First-person ride camera comfort levels for the coaster test.</summary>
    public enum RideComfortMode
    {
        Full,     // locked to train, full motion + banking
        Comfort,  // dynamic vignette hook + banking reduced to 35%
        Cinematic // offset chase camera, zero vection
    }

    /// <summary>
    /// Animates a train along a validated CoasterLayout (arc-length param) and
    /// drives the rider camera per the comfort mode. Fires OnRideComplete with
    /// RideStats so the E/I/N panel can update. The big physical TEST lever in
    /// the PRD calls BeginRide; AbortRide is the emergency stop.
    /// </summary>
    public sealed class TestRideController : MonoBehaviour
    {
        public event Action<RideStats> OnRideComplete;

        [Header("Ride")]
        public RideComfortMode comfortMode = RideComfortMode.Comfort;
        [Tooltip("Defaults to Camera.main at ride start.")]
        public Transform riderCamera;
        public Vector3 seatOffset = new Vector3(0f, 1.15f, 0.6f);
        public Vector3 cinematicOffset = new Vector3(7f, 3.5f, -9f);

        public bool IsRiding => _riding;

        private CoasterLayout _layout;
        private float[] _speeds;
        private float _step = 0.5f;
        private float _distance;
        private bool _riding;
        private GameObject _train;
        private Camera _cam;
        private Vector3 _savedPos;
        private Quaternion _savedRot;
        private Transform _savedParent;

        /// <summary>Returns false (and logs why) if the layout fails Validate().</summary>
        public bool BeginRide(CoasterLayout layout)
        {
            if (_riding || layout == null) return false;
            if (!layout.Validate(out string[] errors))
            {
                Debug.LogWarning("[TestRide] Layout invalid, cannot dispatch:\n- " +
                    string.Join("\n- ", errors));
                return false;
            }

            _layout = layout;
            _speeds = layout.GetSpeedProfile(_step);
            _distance = 0f;

            _train = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _train.name = "TestTrain";
            _train.transform.localScale = new Vector3(0.9f, 0.7f, 1.8f);
            var mat = new Material(Shader.Find("Standard")) { color = new Color(0.8f, 0.15f, 0.15f) };
            _train.GetComponent<Renderer>().sharedMaterial = mat;

            Transform camT = riderCamera != null ? riderCamera
                : (Camera.main != null ? Camera.main.transform : null);
            if (camT == null)
            {
                Debug.LogWarning("[TestRide] No camera found for the rider.");
                Destroy(_train);
                return false;
            }
            _cam = camT.GetComponent<Camera>();
            if (_cam == null)
            {
                Debug.LogWarning("[TestRide] riderCamera has no Camera component.");
                Destroy(_train);
                return false;
            }
            _savedParent = camT.parent;
            _savedPos = camT.position;
            _savedRot = camT.rotation;
            camT.SetParent(null, true); // drive in world space during the ride

            ApplyComfortVignette(comfortMode == RideComfortMode.Comfort ? 0.6f : 0f);
            _riding = true;
            Debug.Log("[TestRide] Dispatch! Mode: " + comfortMode);
            return true;
        }

        public void AbortRide()
        {
            if (_riding) EndRide();
        }

        private void Update()
        {
            if (!_riding) return;

            int idx = Mathf.Clamp(Mathf.FloorToInt(_distance / _step), 0, _speeds.Length - 1);
            float v = Mathf.Max(_speeds[idx], 1f);
            _distance += v * Time.deltaTime;

            if (_distance >= _layout.TotalLength || !_layout.SampleFrame(
                    _distance, out Vector3 p, out float yaw, out float pitch, out float bank))
            {
                EndRide();
                return;
            }

            Quaternion fullRot = CoasterLayout.ComposeRot(yaw, pitch, bank);
            _train.transform.SetPositionAndRotation(p, fullRot);

            Transform camT = _cam.transform;
            switch (comfortMode)
            {
                case RideComfortMode.Full:
                    camT.SetPositionAndRotation(p + fullRot * seatOffset, fullRot);
                    break;
                case RideComfortMode.Comfort:
                    // Most VR nausea comes from visual roll: keep 35% of banking.
                    Quaternion soft = CoasterLayout.ComposeRot(yaw, pitch, bank * 0.35f);
                    camT.SetPositionAndRotation(p + soft * seatOffset, soft);
                    break;
                case RideComfortMode.Cinematic:
                    camT.position = p + fullRot * cinematicOffset;
                    camT.LookAt(p + Vector3.up * 1.5f);
                    break;
            }
        }

        private void EndRide()
        {
            _riding = false;
            ApplyComfortVignette(0f);
            if (_cam != null)
            {
                _cam.transform.SetParent(_savedParent, false);
                _cam.transform.SetPositionAndRotation(_savedPos, _savedRot);
            }
            if (_train != null) Destroy(_train);

            RideStats stats = _layout.ComputeStats();
            Debug.Log($"[TestRide] Complete: {stats.rideTime:F1}s, " +
                $"vmax {stats.maxSpeed:F1} m/s, peak {stats.maxG:F1} G, " +
                $"{stats.drops} drops, {stats.inversions} inversions.");
            OnRideComplete?.Invoke(stats);
            _layout = null;
        }

        /// <summary>
        /// META-SDK HOOK: wire the Meta XR comfort vignette here (fullscreen
        /// overlay or platform vignette API), driven by `strength` and, in
        /// Phase 1, live lateral G. No-op until the SDK integration lands.
        /// </summary>
        private void ApplyComfortVignette(float strength) { }
    }
}
