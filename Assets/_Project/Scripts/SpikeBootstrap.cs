using System;
using UnityEngine;

namespace QuestParkTycoon
{
    /// <summary>
    /// Phase 0 spike bootstrap. No hand-authored .unity scene is used:
    /// everything the spike needs is created procedurally at runtime so the
    /// project boots cleanly the first time Gary opens it.
    ///
    /// Phase 0 exit criteria covered here:
    ///   1. Terrain tile grid renders (TerrainTileMesh).
    ///   2. One grabbable cube (Meta XR Interaction SDK when installed).
    ///   3. 10 Hz sim tick skeleton runs, decoupled from render (SimTick).
    /// </summary>
    public static class SpikeBootstrap
    {
        // Meta XR Interaction SDK assembly-qualified type names.
        // Resolved by REFLECTION so this file compiles BEFORE the SDK is
        // installed via Package Manager. After installing
        // com.meta.xr.sdk.interaction, these resolve and the cube becomes
        // truly grabbable; until then a visible fallback is used.
        private const string GrabbableType = "Oculus.Interaction.Grabbable, Oculus.Interaction";
        private const string GrabTransformerType = "Oculus.Interaction.GrabFreeTransformer, Oculus.Interaction";
        private const string CameraRigType = "OVRCameraRig, Oculus.VR";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var root = new GameObject("SpikeRoot");
            UnityEngine.Object.DontDestroyOnLoad(root);

            // XR rig: Meta's OVRCameraRig once com.meta.xr.sdk.core is present,
            // otherwise a plain fallback camera so the spike still runs in-editor.
            var rigRoot = TryCreateMetaRig(root.transform) ?? CreateFallbackCamera(root.transform);

            // Terrain: 16x16 vertex-colored tiles, 4 m each -> 64 m x 64 m park pad.
            var terrain = new GameObject("Terrain", typeof(MeshFilter), typeof(MeshRenderer));
            terrain.transform.SetParent(root.transform, false);
            terrain.GetComponent<MeshFilter>().sharedMesh = TerrainTileMesh.Build(16, 16, 4f);
            var mat = new Material(Shader.Find("QuestParkTycoon/VertexColorUnlit"));
            if (mat.shader == null || !mat.shader.isSupported)
            {
                Debug.LogWarning("[QuestParkTycoon] Custom shader missing; using Standard fallback.");
                mat = new Material(Shader.Find("Standard"));
            }
            terrain.GetComponent<MeshRenderer>().sharedMaterial = mat;

            // The one grabbable cube: 25 cm, floating at chest height in front of spawn.
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "GrabbableCube";
            cube.transform.SetParent(root.transform, false);
            cube.transform.position = rigRoot.position + rigRoot.forward * 1.0f + Vector3.up * 1.2f;
            cube.transform.localScale = Vector3.one * 0.25f;
            AttachGrabbable(cube);

            // Sim tick: fixed 10 Hz, never on the render thread's mercy.
            root.AddComponent<SimTick>();

            // Phase-1 park sim: clock, rating, guests, spawner, staff.
            ParkSimBootstrap.EnsureParkSystems();

            Debug.Log("[QuestParkTycoon] Spike bootstrap complete.");
        }

        private static Transform TryCreateMetaRig(Transform parent)
        {
            var rigType = Type.GetType(CameraRigType);
            if (rigType == null)
            {
                Debug.Log("[QuestParkTycoon] com.meta.xr.sdk.core not installed yet — using fallback camera.");
                return null;
            }
            var rig = new GameObject("OVRCameraRig");
            rig.transform.SetParent(parent, false);
            rig.AddComponent(rigType);
            Debug.Log("[QuestParkTycoon] OVRCameraRig created from Meta XR Core SDK.");
            return rig.transform;
        }

        private static Transform CreateFallbackCamera(Transform parent)
        {
            var go = new GameObject("FallbackCamera", typeof(Camera));
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(0f, 8f, -12f);
            go.transform.LookAt(Vector3.zero);
            var cam = go.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            return go.transform;
        }

        private static void AttachGrabbable(GameObject go)
        {
            var grabbableType = Type.GetType(GrabbableType);
            if (grabbableType != null)
            {
                go.AddComponent(grabbableType);
                var transformerType = Type.GetType(GrabTransformerType);
                if (transformerType != null) go.AddComponent(transformerType);
                var rb = go.GetComponent<Rigidbody>() ?? go.AddComponent<Rigidbody>();
                rb.useGravity = false;
                rb.isKinematic = true; // ISDK drives the transform while grabbed
                Debug.Log("[QuestParkTycoon] Meta XR Grabbable attached to cube.");
            }
            else
            {
                Debug.LogWarning("[QuestParkTycoon] com.meta.xr.sdk.interaction not installed — " +
                    "cube is NOT grabbable yet. Install it in Package Manager, then re-run. " +
                    "Spinning fallback active so you can see it.");
                go.AddComponent<SpinFallback>();
            }
        }

        /// <summary>Pre-SDK visual fallback: slow spin so the cube is visibly alive.</summary>
        private sealed class SpinFallback : MonoBehaviour
        {
            private void Update() => transform.Rotate(Vector3.up, 30f * Time.deltaTime, Space.World);
        }
    }
}
