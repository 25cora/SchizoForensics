using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.SpatialAwareness;
using UnityEngine;
using UnityEngine.Events;

namespace Forensics
{
    /// <summary>
    /// Controls MRTK 2's spatial mesh observer for the room-scanning phase.
    /// While scanning the mesh is visible. Finishing the scan freezes the current
    /// mesh and switches it to depth-only occlusion while keeping its colliders.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomScanController : MonoBehaviour
    {
        public enum ScanState
        {
            Initializing,
            Ready,
            Scanning,
            Complete,
            Error
        }

        [Header("Startup")]
        [SerializeField] private bool beginScanningAutomatically = true;
        [SerializeField] private bool clearPreviousMeshWhenScanningStarts;
        [SerializeField, Min(1f)] private float initializationTimeoutSeconds = 10f;

        [Header("Automatic Scan")]
        [SerializeField, Min(1f)] private float scanDurationSeconds = 35f;

        [Header("Finished Scan")]
        [SerializeField]
        private SpatialAwarenessMeshDisplayOptions finishedDisplayOption =
            SpatialAwarenessMeshDisplayOptions.Occlusion;

        [Header("Diagnostics")]
        [SerializeField, Min(0.1f)] private float diagnosticsRefreshSeconds = 0.5f;
        [SerializeField] private ScanState currentState = ScanState.Initializing;
        [SerializeField] private int scannedMeshCount;
        [SerializeField] private int scannedTriangleCount;

        [Header("Events")]
        [SerializeField] private UnityEvent onReady;
        [SerializeField] private UnityEvent onScanStarted;
        [SerializeField] private UnityEvent onScanFinished;
        [SerializeField] private UnityEvent onScanReset;

        public event Action<ScanState> StateChanged;

        public ScanState CurrentState => currentState;
        public int ScannedMeshCount => scannedMeshCount;
        public int ScannedTriangleCount => scannedTriangleCount;
        public float ScanTimeRemaining => IsScanning ? Mathf.Max(0f, scanDurationSeconds - (Time.unscaledTime - scanStartedAt)) : 0f;
        public bool IsScanning => currentState == ScanState.Scanning;
        public bool IsComplete => currentState == ScanState.Complete;
        public int SpatialMeshLayerMask { get; private set; }

        public string StatusText => currentState switch
        {
            ScanState.Initializing => "Preparing room scanner...",
            ScanState.Ready => "Ready to scan",
            ScanState.Scanning => $"Scanning room: {scannedMeshCount} mesh sections",
            ScanState.Complete => $"Room scan complete: {scannedMeshCount} mesh sections",
            _ => "Room scanner unavailable"
        };

        private IMixedRealitySpatialAwarenessSystem spatialSystem;
        private IReadOnlyList<IMixedRealitySpatialAwarenessMeshObserver> meshObservers;
        private float nextDiagnosticsTime;
        private float scanStartedAt;

        private IEnumerator Start()
        {
            SetState(ScanState.Initializing);

            float deadline = Time.realtimeSinceStartup + initializationTimeoutSeconds;
            while (!TryResolveSpatialServices() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (spatialSystem == null || meshObservers == null || meshObservers.Count == 0)
            {
                Debug.LogError(
                    "RoomScanController could not find an MRTK spatial mesh observer. " +
                    "Confirm that the active MRTK profile enables Spatial Awareness and contains the OpenXR Spatial Mesh Observer.",
                    this);
                SetState(ScanState.Error);
                yield break;
            }

            SpatialMeshLayerMask = meshObservers[0].MeshPhysicsLayerMask;
            SetState(ScanState.Ready);
            onReady?.Invoke();

            if (beginScanningAutomatically)
            {
                BeginScan();
            }
            else
            {
                spatialSystem.SuspendObservers<IMixedRealitySpatialAwarenessMeshObserver>();
                SetMeshDisplay(SpatialAwarenessMeshDisplayOptions.None);
            }
        }

        private void Update()
        {
            if (IsScanning && Time.unscaledTime - scanStartedAt >= scanDurationSeconds)
            {
                FinishScan();
            }

            if (Time.unscaledTime < nextDiagnosticsTime)
            {
                return;
            }

            nextDiagnosticsTime = Time.unscaledTime + diagnosticsRefreshSeconds;
            RefreshDiagnostics();
        }

        /// <summary>
        /// Clears the previous scan if requested, shows the mesh, and resumes MRTK mesh updates.
        /// This method can be connected directly to an MRTK button's OnClick event.
        /// </summary>
        public void BeginScan()
        {
            BeginScanInternal(clearPreviousMeshWhenScanningStarts);
        }

        /// <summary>Continues scanning after clue placement fails without discarding the mesh already found.</summary>
        public void ResumeScanKeepingMesh()
        {
            BeginScanInternal(false);
        }

        private void BeginScanInternal(bool clearExistingMesh)
        {
            if (!EnsureServicesReady())
            {
                return;
            }

            if (IsScanning)
            {
                return;
            }

            if (clearExistingMesh)
            {
                ClearMeshObservations();
            }

            SetMeshDisplay(SpatialAwarenessMeshDisplayOptions.Visible);
            spatialSystem.ResumeObservers<IMixedRealitySpatialAwarenessMeshObserver>();
            scanStartedAt = Time.unscaledTime;
            SetState(ScanState.Scanning);
            onScanStarted?.Invoke();
        }

        /// <summary>
        /// Stops requesting mesh updates but preserves existing meshes and colliders.
        /// The default finished display option is Occlusion, so real surfaces hide clues.
        /// </summary>
        public void FinishScan()
        {
            if (!IsScanning)
            {
                return;
            }

            if (!EnsureServicesReady())
            {
                SetState(ScanState.Error);
                return;
            }

            spatialSystem.SuspendObservers<IMixedRealitySpatialAwarenessMeshObserver>();
            SetMeshDisplay(finishedDisplayOption);
            RefreshDiagnostics();
            SetState(ScanState.Complete);
            onScanFinished?.Invoke();
        }

        /// <summary>
        /// Stops scanning and deletes generated mesh objects. Call BeginScan afterward
        /// to create a completely new room scan.
        /// </summary>
        public void ResetScan()
        {
            if (!EnsureServicesReady())
            {
                return;
            }

            spatialSystem.SuspendObservers<IMixedRealitySpatialAwarenessMeshObserver>();
            ClearMeshObservations();
            SetMeshDisplay(SpatialAwarenessMeshDisplayOptions.None);
            RefreshDiagnostics();
            SetState(ScanState.Ready);
            onScanReset?.Invoke();
        }

        public void ShowMesh()
        {
            if (EnsureServicesReady())
            {
                SetMeshDisplay(SpatialAwarenessMeshDisplayOptions.Visible);
            }
        }

        public void HideMesh()
        {
            if (EnsureServicesReady())
            {
                SetMeshDisplay(SpatialAwarenessMeshDisplayOptions.None);
            }
        }

        public void UseMeshAsOccluder()
        {
            if (EnsureServicesReady())
            {
                SetMeshDisplay(SpatialAwarenessMeshDisplayOptions.Occlusion);
            }
        }

        private bool EnsureServicesReady()
        {
            if (TryResolveSpatialServices())
            {
                return true;
            }

            Debug.LogWarning("The MRTK spatial mesh observer is not ready yet.", this);
            return false;
        }

        private bool TryResolveSpatialServices()
        {
            spatialSystem = CoreServices.SpatialAwarenessSystem;
            if (!(spatialSystem is IMixedRealityDataProviderAccess providerAccess))
            {
                return false;
            }

            meshObservers = providerAccess.GetDataProviders<IMixedRealitySpatialAwarenessMeshObserver>();
            return meshObservers != null && meshObservers.Count > 0;
        }

        private void SetMeshDisplay(SpatialAwarenessMeshDisplayOptions option)
        {
            foreach (IMixedRealitySpatialAwarenessMeshObserver observer in meshObservers)
            {
                observer.DisplayOption = option;
            }
        }

        private void ClearMeshObservations()
        {
            foreach (IMixedRealitySpatialAwarenessMeshObserver observer in meshObservers)
            {
                observer.ClearObservations();
            }

            scannedMeshCount = 0;
            scannedTriangleCount = 0;
        }

        private void RefreshDiagnostics()
        {
            if (meshObservers == null)
            {
                return;
            }

            int meshCount = 0;
            int triangleCount = 0;

            foreach (IMixedRealitySpatialAwarenessMeshObserver observer in meshObservers)
            {
                meshCount += observer.Meshes.Count;
                foreach (SpatialAwarenessMeshObject meshObject in observer.Meshes.Values)
                {
                    Mesh mesh = meshObject.Filter != null ? meshObject.Filter.sharedMesh : null;
                    if (mesh != null)
                    {
                        triangleCount += (int)mesh.GetIndexCount(0) / 3;
                    }
                }
            }

            scannedMeshCount = meshCount;
            scannedTriangleCount = triangleCount;
        }

        private void SetState(ScanState nextState)
        {
            if (currentState == nextState)
            {
                return;
            }

            currentState = nextState;
            StateChanged?.Invoke(currentState);
        }
    }
}
