using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace Forensics
{
    /// <summary>Tracks generated clues, updates the view counter, and replaces solved clues.</summary>
    [DisallowMultipleComponent]
    public sealed class ClueProgressController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HidingSpotGenerator hidingSpotGenerator;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private TMP_Text hudTextPrefab;
        [SerializeField] private GameObject solvedMarkerPrefab;
        [SerializeField] private Transform markerParent;

        [Header("HUD Position")]
        [SerializeField, Range(0f, 1f)] private float viewportX = 0.9f;
        [SerializeField, Range(0f, 1f)] private float viewportY = 0.78f;
        [SerializeField, Min(0.2f)] private float hudDistance = 0.6f;
        [SerializeField] private string labelFormat = "{0} of {1} clues found";

        [Header("Solved Marker")]
        [SerializeField] private Vector3 markerWorldOffset = new Vector3(0f, 0.06f, 0f);

        [Header("Events")]
        [SerializeField] private UnityEvent onProgressChanged;
        [SerializeField] private UnityEvent onAllCluesFound;
        [SerializeField] private UnityEvent onAllCluesSolved;

        public int FoundClues => found.Count;
        public int TotalClues => totalClues;
        public int SolvedClues => solvedClues;
        public event Action AllCluesSolved;

        public void HideForWin()
        {
            if (hudText != null) hudText.gameObject.SetActive(false);
        }

        private readonly HashSet<ClueBase> tracked = new HashSet<ClueBase>();
        private readonly HashSet<ClueBase> found = new HashSet<ClueBase>();
        private readonly List<GameObject> markers = new List<GameObject>();
        private readonly Dictionary<ClueBase, GameObject> previewMarkers = new Dictionary<ClueBase, GameObject>();
        private TMP_Text hudText;
        private int totalClues;
        private int solvedClues;

        private void Awake()
        {
            if (hidingSpotGenerator == null)
                hidingSpotGenerator = FindFirstObjectByType<HidingSpotGenerator>();
            if (playerCamera == null)
                playerCamera = Camera.main;
            if (hudTextPrefab != null && playerCamera != null)
                hudText = Instantiate(hudTextPrefab, playerCamera.transform, false);
            RefreshHud();
        }

        private void OnEnable()
        {
            if (hidingSpotGenerator == null)
                return;
            hidingSpotGenerator.ClueSpawned += TrackClue;
            hidingSpotGenerator.CluesCleared += ResetProgress;
            foreach (Transform clueTransform in hidingSpotGenerator.SpawnedClues)
            {
                if (clueTransform != null)
                    TrackClue(clueTransform.GetComponent<ClueBase>());
            }
        }

        private void OnDisable()
        {
            if (hidingSpotGenerator != null)
            {
                hidingSpotGenerator.ClueSpawned -= TrackClue;
                hidingSpotGenerator.CluesCleared -= ResetProgress;
            }
            foreach (ClueBase clue in tracked)
            {
                if (clue != null)
                {
                    clue.Found -= HandleFound;
                    clue.CompletionStarted -= HandleCompletionStarted;
                    clue.CompletionCancelled -= HandleCompletionCancelled;
                    clue.Completed -= HandleCompleted;
                }
            }
            tracked.Clear();
            found.Clear();
            totalClues = 0;
            solvedClues = 0;
            foreach (GameObject marker in markers)
                if (marker != null) Destroy(marker);
            markers.Clear();
            previewMarkers.Clear();
            RefreshHud();
        }

        private void LateUpdate()
        {
            if (hudText == null || playerCamera == null)
                return;
            hudText.transform.position = playerCamera.ViewportToWorldPoint(
                new Vector3(viewportX, viewportY, hudDistance));
            hudText.transform.rotation = playerCamera.transform.rotation;
        }

        private void TrackClue(ClueBase clue)
        {
            if (clue == null || !tracked.Add(clue))
                return;
            totalClues++;
            clue.Found += HandleFound;
            clue.CompletionStarted += HandleCompletionStarted;
            clue.CompletionCancelled += HandleCompletionCancelled;
            clue.Completed += HandleCompleted;
            if (clue.HasBeenFound)
                found.Add(clue);
            RefreshHud();
        }

        private void HandleFound(ClueBase clue)
        {
            if (!tracked.Contains(clue) || !found.Add(clue))
                return;
            RefreshHud();
            if (found.Count == totalClues && totalClues > 0)
                onAllCluesFound?.Invoke();
        }

        private void HandleCompletionStarted(ClueBase clue)
        {
            if (solvedMarkerPrefab == null || !tracked.Contains(clue)) return;

            Vector3 towardCamera = playerCamera != null
                ? (playerCamera.transform.position - clue.transform.position).normalized
                : -clue.transform.forward;
            GameObject marker = Instantiate(solvedMarkerPrefab,
                clue.transform.position + towardCamera * 0.025f,
                Quaternion.identity);
            marker.transform.SetParent(clue.transform, true);
            var billboard = marker.GetComponent<SolvedClueMarker>();
            if (billboard != null) billboard.SetCamera(playerCamera);
            previewMarkers[clue] = marker;
            markers.Add(marker);
        }

        private void HandleCompletionCancelled(ClueBase clue)
        {
            if (!previewMarkers.TryGetValue(clue, out GameObject marker)) return;
            previewMarkers.Remove(clue);
            markers.Remove(marker);
            if (marker != null) Destroy(marker);
        }

        private void HandleCompleted(ClueBase clue)
        {
            if (!tracked.Remove(clue))
                return;

            clue.Found -= HandleFound;
            clue.CompletionStarted -= HandleCompletionStarted;
            clue.CompletionCancelled -= HandleCompletionCancelled;
            clue.Completed -= HandleCompleted;
            found.Add(clue);
            solvedClues++;

            // Inspection has closed; move the same check from the viewport to the clue's room position.
            Vector3 markerPosition = clue.transform.position + markerWorldOffset;
            if (previewMarkers.TryGetValue(clue, out GameObject marker))
            {
                previewMarkers.Remove(clue);
                marker.transform.SetParent(markerParent, true);
                marker.transform.position = markerPosition;
            }
            else if (solvedMarkerPrefab != null)
            {
                marker = Instantiate(solvedMarkerPrefab, markerPosition,
                    Quaternion.identity, markerParent);
                var billboard = marker.GetComponent<SolvedClueMarker>();
                if (billboard != null) billboard.SetCamera(playerCamera);
                markers.Add(marker);
            }

            hidingSpotGenerator.RemoveCompletedClue(clue);
            RefreshHud();
            if (solvedClues == totalClues && totalClues > 0)
            {
                onAllCluesSolved?.Invoke();
                AllCluesSolved?.Invoke();
            }
        }

        private void ResetProgress()
        {
            foreach (ClueBase clue in tracked)
            {
                if (clue != null)
                {
                    clue.Found -= HandleFound;
                    clue.CompletionStarted -= HandleCompletionStarted;
                    clue.CompletionCancelled -= HandleCompletionCancelled;
                    clue.Completed -= HandleCompleted;
                }
            }
            tracked.Clear();
            found.Clear();
            totalClues = 0;
            solvedClues = 0;
            foreach (GameObject marker in markers)
                if (marker != null) Destroy(marker);
            markers.Clear();
            previewMarkers.Clear();
            RefreshHud();
        }

        private void RefreshHud()
        {
            if (hudText != null)
                hudText.text = string.Format(labelFormat, found.Count, totalClues) +
                    $"\n{solvedClues} out of {totalClues} clues Solved";
            onProgressChanged?.Invoke();
        }
    }
}
