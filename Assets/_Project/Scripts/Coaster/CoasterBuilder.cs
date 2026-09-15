using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuestParkTycoon.Coaster
{
    /// <summary>
    /// Placement input abstraction. META-SDK NOTE: a MetaXRBuilderInput class will
    /// implement this against com.meta.xr.sdk.interaction — controller ray for
    /// AimPoint, trigger edge for CommitDown, B/Y for UndoDown, thumbstick X for
    /// BankDelta. Until then, KeyboardDebugInput below.
    /// </summary>
    public interface IBuilderInput
    {
        Vector3 AimPoint { get; }
        bool CommitDown { get; } // edge-triggered by the implementation
        bool UndoDown { get; }
        bool ClearDown { get; }
        float BankDelta { get; } // rad/sec of ghost yaw trim (aiming piece 1)
    }
    /// <summary>
    /// Hand-driven coaster placement state machine. The ghost preview snaps to the
    /// chain end (or the aim point for piece 1); committing instantiates a
    /// permanent mesh. Frame-driven; Tick is a no-op sim hook.
    /// </summary>
    public sealed class CoasterBuilder : MonoBehaviour, ISimSystem
    {
        public CoasterLayout Layout { get; } = new CoasterLayout();
        [Header("Build")]
        public PieceType selectedPiece = PieceType.Straight;
        public Transform layoutRoot; // auto-created under this GameObject
        public float gridSnap = 1f;
        public event Action OnLayoutChanged;
        public IBuilderInput Input { get; set; }
        private readonly List<GameObject> _pieceObjects = new List<GameObject>();
        private static readonly PieceType[] PieceOrder = (PieceType[])Enum.GetValues(typeof(PieceType));
        private GameObject _ghost;
        private Material _trackMaterial;
        private float _aimYaw, _time;
        private void Awake()
        {
            if (layoutRoot == null)
            {
                layoutRoot = new GameObject("CoasterLayout").transform;
                layoutRoot.SetParent(transform, false);
            }
            _trackMaterial = new Material(Shader.Find("QuestParkTycoon/VertexColorUnlit"));
            if (_trackMaterial.shader == null || !_trackMaterial.shader.isSupported)
            {
                Debug.LogWarning("[Coaster] VertexColorUnlit shader missing; Standard fallback.");
                _trackMaterial = new Material(Shader.Find("Standard"));
            }
            var kb = GetComponent<KeyboardDebugInput>();
            if (Input == null)
            {
                if (kb == null) kb = gameObject.AddComponent<KeyboardDebugInput>();
                Input = kb;
            }
            if (kb != null) kb.builder = this;
            _ghost = new GameObject("GhostPiece", typeof(MeshFilter), typeof(MeshRenderer));
            _ghost.GetComponent<MeshRenderer>().sharedMaterial = _trackMaterial;
            RefreshGhostMesh();
        }
        private void Update()
        {
            _time += Time.deltaTime;
            if (Input == null) return;
            _aimYaw += Input.BankDelta * Time.deltaTime;
            UpdateGhost();
            if (Input.CommitDown) CommitPiece();
            else if (Input.UndoDown) UndoPiece();
            else if (Input.ClearDown) Clear();
        }
        public void Tick(float dt) { } // placement is frame-driven; sim hook reserved
        public void SelectPiece(PieceType type) { selectedPiece = type; RefreshGhostMesh(); }
        public void SelectNextPiece(int direction)
        {
            int i = Array.IndexOf(PieceOrder, selectedPiece);
            SelectPiece(PieceOrder[(i + direction + PieceOrder.Length) % PieceOrder.Length]);
        }
        public void CommitPiece()
        {
            Layout.AddPiece(selectedPiece);
            var entry = Layout.GetEntryPose(Layout.PieceCount - 1);
            var go = new GameObject($"Piece_{Layout.PieceCount - 1}_{selectedPiece}");
            go.transform.SetParent(layoutRoot, false);
            go.transform.SetPositionAndRotation(entry.position, entry.Rotation);
            go.AddComponent<MeshFilter>().sharedMesh = TrackPiece.BuildMesh(selectedPiece, Layout.PieceCount);
            go.AddComponent<MeshRenderer>().sharedMaterial = _trackMaterial;
            _pieceObjects.Add(go);
            RefreshGhostMesh();
            OnLayoutChanged?.Invoke();
        }
        public bool UndoPiece()
        {
            if (!Layout.RemoveLast()) return false;
            int last = _pieceObjects.Count - 1;
            Destroy(_pieceObjects[last]);
            _pieceObjects.RemoveAt(last);
            RefreshGhostMesh();
            OnLayoutChanged?.Invoke();
            return true;
        }
        public void Clear()
        {
            Layout.Clear();
            foreach (var go in _pieceObjects) Destroy(go);
            _pieceObjects.Clear();
            RefreshGhostMesh();
            OnLayoutChanged?.Invoke();
        }
        private PiecePose GhostPose()
        {
            if (Layout.PieceCount == 0)
            {
                Vector3 a = Input.AimPoint;
                a.x = Mathf.Round(a.x / gridSnap) * gridSnap;
                a.z = Mathf.Round(a.z / gridSnap) * gridSnap;
                a.y = 0f;
                return new PiecePose { position = a, yaw = _aimYaw };
            }
            return Layout.GetEntryPose(Layout.PieceCount); // snap to chain end
        }
        private void UpdateGhost()
        {
            var pose = GhostPose();
            Vector3 p = pose.position;
            p.y += Mathf.Sin(_time * 1.5f * Mathf.PI * 2f) * 0.05f; // hover bob
            _ghost.transform.SetPositionAndRotation(p, pose.Rotation);
        }
        private void RefreshGhostMesh()
        {
            _ghost.GetComponent<MeshFilter>().sharedMesh = TrackPiece.BuildMesh(selectedPiece, 999);
            _ghost.name = "GhostPiece_" + selectedPiece;
        }
    }
    /// <summary>
    /// Desktop testing input: mouse ray onto the ground plane aims, Space commits,
    /// Backspace undoes, C clears, Q/E trims aim yaw, 1-9/0 and -/= pick pieces.
    /// Replaced by MetaXRBuilderInput on device.
    /// </summary>
    public sealed class KeyboardDebugInput : MonoBehaviour, IBuilderInput
    {
        public CoasterBuilder builder;
        private static readonly PieceType[] Order = (PieceType[])Enum.GetValues(typeof(PieceType));
        public Vector3 AimPoint
        {
            get
            {
                var cam = Camera.main;
                if (cam == null) return Vector3.zero;
                Ray ray = cam.ScreenPointToRay(UnityEngine.Input.mousePosition);
                return new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float d)
                    ? ray.GetPoint(d) : Vector3.zero;
            }
        }
        public bool CommitDown => UnityEngine.Input.GetKeyDown(KeyCode.Space);
        public bool UndoDown => UnityEngine.Input.GetKeyDown(KeyCode.Backspace);
        public bool ClearDown => UnityEngine.Input.GetKeyDown(KeyCode.C);
        public float BankDelta =>
            (UnityEngine.Input.GetKey(KeyCode.Q) ? 1.5f : 0f) +
            (UnityEngine.Input.GetKey(KeyCode.E) ? -1.5f : 0f);
        private void Update()
        {
            if (builder == null) return;
            for (int i = 0; i < 9 && i < Order.Length; i++)
                if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1 + i)) builder.SelectPiece(Order[i]);
            if (Order.Length > 9 && UnityEngine.Input.GetKeyDown(KeyCode.Alpha0)) builder.SelectPiece(Order[9]);
            if (UnityEngine.Input.GetKeyDown(KeyCode.Minus)) builder.SelectNextPiece(-1);
            if (UnityEngine.Input.GetKeyDown(KeyCode.Equals)) builder.SelectNextPiece(1);
        }
    }
}
