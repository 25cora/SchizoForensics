using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Forensics
{
    /// <summary>
    /// Generates geometry-only clue positions after a room scan. A valid position
    /// has physical clearance, rests on an upward-facing scanned surface, is hidden
    /// from the player's scan position, and can be revealed from a nearby viewpoint.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HidingSpotGenerator : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private RoomScanController roomScanController;
        [SerializeField] private ClueInspectionController inspectionController;
        [SerializeField] private Camera playerCamera;

        [Header("Clues")]
        [SerializeField] private List<GameObject> cluePrefabs = new List<GameObject>();
        [SerializeField, Min(1)] private int cluesToSpawn = 3;
        [SerializeField] private Transform spawnedClueParent;
        [SerializeField] private bool createDebugCluesWhenNoPrefabs = true;
        [SerializeField] private bool replacePreviouslySpawnedClues = true;

        [Header("Search Area")]
        [SerializeField, Min(0.2f)] private float minimumDistance = 0.8f;
        [SerializeField, Min(0.5f)] private float maximumDistance = 3.5f;
        [SerializeField, Min(1)] private int attemptsPerClue = 100;
        [SerializeField, Tooltip("Downward probe starts this far below eye height, helping it find floors behind furniture instead of only tabletops.")]
        private float probeStartBelowEye = 0.35f;
        [SerializeField, Min(0.5f)] private float downwardProbeDistance = 2.2f;

        [Header("Candidate Validation")]
        [SerializeField, Range(0f, 1f)] private float minimumUpwardNormal = 0.55f;
        [SerializeField, Min(0.01f)] private float clueClearanceRadius = 0.07f;
        [SerializeField, Min(0f)] private float surfaceGap = 0.015f;
        [SerializeField, Min(0.1f)] private float minimumSpotSeparation = 0.65f;
        [SerializeField, Min(0.1f)] private float revealViewpointOffset = 0.8f;
        [SerializeField, Min(0.05f)] private float occlusionMargin = 0.12f;
        [SerializeField] private bool alignClueUpToSurface = true;

        [Header("Events")]
        [SerializeField] private UnityEvent onGenerationStarted;
        [SerializeField] private UnityEvent onGenerationCompleted;
        [SerializeField] private UnityEvent onGenerationFailed;

        public IReadOnlyList<Transform> SpawnedClues => spawnedClues;
        public IReadOnlyList<Vector3> GeneratedPositions => generatedPositions;
        public event Action<ClueBase> ClueSpawned;
        public event Action CluesCleared;
        public event Action<int> GenerationSucceeded;
        public event Action GenerationFailed;

        private readonly List<Transform> spawnedClues = new List<Transform>();
        private readonly List<Vector3> generatedPositions = new List<Vector3>();

        private void Awake()
        {
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }

            if (roomScanController == null)
            {
                roomScanController = FindFirstObjectByType<RoomScanController>();
            }

            if (inspectionController == null)
            {
                inspectionController = FindFirstObjectByType<ClueInspectionController>();
            }
        }

        private void OnEnable()
        {
            if (roomScanController != null)
            {
                roomScanController.StateChanged += HandleScanStateChanged;
            }
        }

        private void Start()
        {
            if (roomScanController == null)
            {
                Debug.LogError("HidingSpotGenerator needs a RoomScanController.", this);
                enabled = false;
                return;
            }

            if (playerCamera == null)
            {
                Debug.LogError("HidingSpotGenerator could not find the player's camera.", this);
                enabled = false;
                return;
            }

            if (roomScanController.IsComplete)
            {
                GenerateAndSpawnClues();
            }
        }

        private void OnDisable()
        {
            if (roomScanController != null)
            {
                roomScanController.StateChanged -= HandleScanStateChanged;
            }
        }

        private void HandleScanStateChanged(RoomScanController.ScanState state)
        {
            if (state == RoomScanController.ScanState.Complete)
            {
                GenerateAndSpawnClues();
            }
        }

        /// <summary>
        /// Can also be called directly from an MRTK button after FinishScan.
        /// </summary>
        public void GenerateAndSpawnClues()
        {
            if (roomScanController == null || !roomScanController.IsComplete)
            {
                Debug.LogWarning("Finish the room scan before generating hiding spots.", this);
                return;
            }

            int spatialMeshMask = roomScanController.SpatialMeshLayerMask;
            if (spatialMeshMask == 0)
            {
                Debug.LogError("The spatial mesh physics-layer mask is unavailable.", this);
                onGenerationFailed?.Invoke();
                GenerationFailed?.Invoke();
                return;
            }

            if (replacePreviouslySpawnedClues)
            {
                ClearSpawnedClues();
            }

            onGenerationStarted?.Invoke();
            Vector3 scanViewPosition = playerCamera.transform.position;
            Quaternion scanViewRotation = playerCamera.transform.rotation;

            for (int clueIndex = 0; clueIndex < cluesToSpawn; clueIndex++)
            {
                if (!TryFindHidingSpot(scanViewPosition, scanViewRotation, spatialMeshMask, out Pose spot))
                {
                    Debug.LogWarning($"Could only find {generatedPositions.Count} of {cluesToSpawn} requested hiding spots. Scan more of the room or loosen the generator settings.", this);
                    break;
                }

                generatedPositions.Add(spot.position);
                SpawnClue(clueIndex, spot);
            }

            if (generatedPositions.Count > 0)
            {
                onGenerationCompleted?.Invoke();
                GenerationSucceeded?.Invoke(generatedPositions.Count);
            }
            else
            {
                onGenerationFailed?.Invoke();
                GenerationFailed?.Invoke();
            }
        }

        public void ClearSpawnedClues()
        {
            foreach (Transform clue in spawnedClues)
            {
                if (clue != null)
                {
                    Destroy(clue.gameObject);
                }
            }

            spawnedClues.Clear();
            generatedPositions.Clear();
            CluesCleared?.Invoke();
        }

        public void RemoveCompletedClue(ClueBase clue)
        {
            if (clue == null || !clue.IsComplete || !spawnedClues.Remove(clue.transform))
            {
                return;
            }

            clue.gameObject.SetActive(false);
            Destroy(clue.gameObject);
        }

        private bool TryFindHidingSpot(
            Vector3 scanViewPosition,
            Quaternion scanViewRotation,
            int spatialMeshMask,
            out Pose result)
        {
            Vector3 planarForward = Vector3.ProjectOnPlane(scanViewRotation * Vector3.forward, Vector3.up).normalized;
            Vector3 planarRight = Vector3.Cross(Vector3.up, planarForward).normalized;

            if (planarForward.sqrMagnitude < 0.1f)
            {
                planarForward = Vector3.forward;
                planarRight = Vector3.right;
            }

            for (int attempt = 0; attempt < attemptsPerClue; attempt++)
            {
                float angle = UnityEngine.Random.Range(-Mathf.PI, Mathf.PI);
                float distance = UnityEngine.Random.Range(minimumDistance, maximumDistance);
                Vector3 planarOffset = (planarRight * Mathf.Sin(angle) + planarForward * Mathf.Cos(angle)) * distance;
                Vector3 probeOrigin = scanViewPosition + planarOffset - Vector3.up * probeStartBelowEye;

                if (!Physics.Raycast(
                        probeOrigin,
                        Vector3.down,
                        out RaycastHit supportHit,
                        downwardProbeDistance,
                        spatialMeshMask,
                        QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                if (Vector3.Dot(supportHit.normal, Vector3.up) < minimumUpwardNormal)
                {
                    continue;
                }

                Vector3 candidate = supportHit.point + supportHit.normal * (clueClearanceRadius + surfaceGap);
                if (Physics.CheckSphere(
                        candidate,
                        clueClearanceRadius * 0.85f,
                        spatialMeshMask,
                        QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                if (!IsFarEnoughFromOtherSpots(candidate))
                {
                    continue;
                }

                if (!IsOccludedFrom(scanViewPosition, candidate, spatialMeshMask))
                {
                    continue;
                }

                if (!CanBeRevealedFromNearbyViewpoint(
                        scanViewPosition,
                        scanViewRotation,
                        candidate,
                        spatialMeshMask))
                {
                    continue;
                }

                Vector3 towardPlayer = Vector3.ProjectOnPlane(scanViewPosition - candidate, supportHit.normal);
                Quaternion rotation = towardPlayer.sqrMagnitude > 0.001f
                    ? Quaternion.LookRotation(towardPlayer.normalized, alignClueUpToSurface ? supportHit.normal : Vector3.up)
                    : Quaternion.identity;

                result = new Pose(candidate, rotation);
                return true;
            }

            result = default;
            return false;
        }

        private bool IsOccludedFrom(Vector3 viewpoint, Vector3 candidate, int spatialMeshMask)
        {
            Vector3 direction = candidate - viewpoint;
            float distance = direction.magnitude;
            return distance > occlusionMargin && Physics.Raycast(
                viewpoint,
                direction.normalized,
                distance - occlusionMargin,
                spatialMeshMask,
                QueryTriggerInteraction.Ignore);
        }

        private bool CanBeRevealedFromNearbyViewpoint(
            Vector3 scanViewPosition,
            Quaternion scanViewRotation,
            Vector3 candidate,
            int spatialMeshMask)
        {
            Vector3 right = scanViewRotation * Vector3.right;
            Vector3[] revealPositions =
            {
                scanViewPosition + right * revealViewpointOffset,
                scanViewPosition - right * revealViewpointOffset,
                scanViewPosition - Vector3.up * revealViewpointOffset
            };

            foreach (Vector3 revealPosition in revealPositions)
            {
                if (!IsOccludedFrom(revealPosition, candidate, spatialMeshMask))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsFarEnoughFromOtherSpots(Vector3 candidate)
        {
            float minimumSqrDistance = minimumSpotSeparation * minimumSpotSeparation;
            foreach (Vector3 position in generatedPositions)
            {
                if ((candidate - position).sqrMagnitude < minimumSqrDistance)
                {
                    return false;
                }
            }

            return true;
        }

        private void SpawnClue(int clueIndex, Pose spot)
        {
            GameObject clueObject;
            if (cluePrefabs.Count > 0 && cluePrefabs[clueIndex % cluePrefabs.Count] != null)
            {
                clueObject = Instantiate(
                    cluePrefabs[clueIndex % cluePrefabs.Count],
                    spot.position,
                    spot.rotation,
                    spawnedClueParent);
            }
            else if (createDebugCluesWhenNoPrefabs)
            {
                clueObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                clueObject.name = $"Debug Clue {clueIndex + 1}";
                clueObject.transform.SetParent(spawnedClueParent, true);
                clueObject.transform.SetPositionAndRotation(spot.position, spot.rotation);
                clueObject.transform.localScale = Vector3.one * 0.09f;
            }
            else
            {
                return;
            }

            ClueBase clue = clueObject.GetComponent<ClueBase>();
            if (clue == null)
            {
                clue = clueObject.AddComponent<PrototypeClue>();
            }

            clue.SetInspectionController(inspectionController);
            spawnedClues.Add(clueObject.transform);
            ClueSpawned?.Invoke(clue);
        }
    }
}
