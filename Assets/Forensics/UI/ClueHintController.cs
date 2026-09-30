using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Forensics
{
    /// <summary>Gives a spatial sound hint, then a screen-edge direction hint after inactivity.</summary>
    [DisallowMultipleComponent]
    public sealed class ClueHintController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HidingSpotGenerator hidingSpotGenerator;
        [SerializeField] private ClueInspectionController inspectionController;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private AudioClip hintSound;
        [SerializeField] private GameObject chevronPrefab;

        [Header("Timing")]
        [SerializeField, Min(0f)] private float secondsWithoutDiscovery = 90f;
        [SerializeField, Min(0f)] private float secondsAfterSound = 30f;

        [Header("Spatial Sound")]
        [SerializeField, Range(0f, 1f)] private float soundVolume = 0.8f;
        [SerializeField, Min(0.01f)] private float soundMinDistance = 0.4f;
        [SerializeField, Min(0.1f)] private float soundMaxDistance = 8f;
        [SerializeField] private bool useSpatializer;

        [Header("Chevron")]
        [SerializeField, Range(0.05f, 0.4f)] private float viewportEdgeInset = 0.13f;
        [SerializeField, Min(0.2f)] private float chevronDistance = 0.6f;
        [SerializeField, Min(0.01f)] private float chevronScale = 0.45f;
        [SerializeField] private Vector3 chevronEulerOffset;

        [Header("Events")]
        [SerializeField] private UnityEvent onSoundHint;
        [SerializeField] private UnityEvent onChevronShown;
        [SerializeField] private UnityEvent onHintDismissed;

        private enum HintStage { Waiting, SoundPlayed, ChevronVisible }

        private readonly HashSet<ClueBase> trackedClues = new HashSet<ClueBase>();
        private HintStage stage;
        private ClueBase targetClue;
        private GameObject chevron;
        private AudioSource soundSource;
        private float quietElapsed;
        private float soundElapsed;

        private void Awake()
        {
            if (hidingSpotGenerator == null)
                hidingSpotGenerator = GetComponent<HidingSpotGenerator>();
            if (inspectionController == null)
                inspectionController = FindFirstObjectByType<ClueInspectionController>();
            if (playerCamera == null)
                playerCamera = Camera.main;
        }

        private void OnEnable()
        {
            if (inspectionController != null)
                inspectionController.InspectionOpened += HandleInspectionOpened;
            if (hidingSpotGenerator == null)
                return;

            hidingSpotGenerator.ClueSpawned += TrackClue;
            hidingSpotGenerator.CluesCleared += ClearClues;
            foreach (Transform clueTransform in hidingSpotGenerator.SpawnedClues)
                if (clueTransform != null)
                    TrackClue(clueTransform.GetComponent<ClueBase>());
        }

        private void OnDisable()
        {
            if (inspectionController != null)
                inspectionController.InspectionOpened -= HandleInspectionOpened;
            if (hidingSpotGenerator != null)
            {
                hidingSpotGenerator.ClueSpawned -= TrackClue;
                hidingSpotGenerator.CluesCleared -= ClearClues;
            }
            ClearClues();
        }

        private void Update()
        {
            if (playerCamera == null)
                return;

            if (inspectionController != null && inspectionController.ActiveClue != null)
            {
                if (stage != HintStage.Waiting)
                    DismissHint();
                else
                    quietElapsed = 0f;
                return;
            }

            if (stage != HintStage.Waiting)
            {
                if (targetClue == null || targetClue.IsComplete || targetClue.HasBeenFound)
                {
                    DismissHint();
                    return;
                }

                if (stage == HintStage.SoundPlayed)
                {
                    soundElapsed += Time.deltaTime;
                    if (soundElapsed >= secondsAfterSound)
                        ShowChevron();
                }
                return;
            }

            if (!HasUnfoundClues())
            {
                quietElapsed = 0f;
                return;
            }

            quietElapsed += Time.deltaTime;
            if (quietElapsed >= secondsWithoutDiscovery)
                PlaySoundHint();
        }

        private void LateUpdate()
        {
            if (stage != HintStage.ChevronVisible || chevron == null || targetClue == null || playerCamera == null)
                return;

            Vector3 viewport = playerCamera.WorldToViewportPoint(targetClue.transform.position);
            Vector2 direction = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);
            if (viewport.z < 0f)
                direction = -direction;
            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector2.up;
            direction.Normalize();

            float radius = 0.5f - viewportEdgeInset;
            float edgeFactor = Mathf.Min(
                Mathf.Abs(direction.x) > 0.0001f ? radius / Mathf.Abs(direction.x) : float.PositiveInfinity,
                Mathf.Abs(direction.y) > 0.0001f ? radius / Mathf.Abs(direction.y) : float.PositiveInfinity);
            Vector2 arrowViewport = new Vector2(0.5f, 0.5f) + direction * edgeFactor;
            chevron.transform.position = playerCamera.ViewportToWorldPoint(
                new Vector3(arrowViewport.x, arrowViewport.y, chevronDistance));

            float angle = -Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
            chevron.transform.rotation = playerCamera.transform.rotation *
                                         Quaternion.AngleAxis(angle, Vector3.forward) *
                                         Quaternion.Euler(chevronEulerOffset);
        }

        private void TrackClue(ClueBase clue)
        {
            if (clue == null || !trackedClues.Add(clue))
                return;
            clue.Found += HandleFound;
            clue.Completed += HandleCompleted;
            if (stage == HintStage.Waiting)
                quietElapsed = 0f;
        }

        private void HandleFound(ClueBase clue) => DismissHint();
        private void HandleInspectionOpened(ClueBase clue) => DismissHint();

        private void HandleCompleted(ClueBase clue)
        {
            clue.Found -= HandleFound;
            clue.Completed -= HandleCompleted;
            trackedClues.Remove(clue);
            if (clue == targetClue || !HasUnfoundClues())
                DismissHint();
        }

        private void ClearClues()
        {
            foreach (ClueBase clue in trackedClues)
            {
                if (clue == null)
                    continue;
                clue.Found -= HandleFound;
                clue.Completed -= HandleCompleted;
            }
            trackedClues.Clear();
            DismissHint();
        }

        private bool HasUnfoundClues()
        {
            foreach (ClueBase clue in trackedClues)
                if (clue != null && !clue.IsComplete && !clue.HasBeenFound)
                    return true;
            return false;
        }

        private void PlaySoundHint()
        {
            var candidates = new List<ClueBase>();
            foreach (ClueBase clue in trackedClues)
                if (clue != null && !clue.IsComplete && !clue.HasBeenFound)
                    candidates.Add(clue);
            if (candidates.Count == 0)
                return;

            targetClue = candidates[Random.Range(0, candidates.Count)];
            stage = HintStage.SoundPlayed;
            soundElapsed = 0f;

            if (hintSound != null)
            {
                var soundObject = new GameObject("Clue Hint Sound");
                soundObject.transform.position = targetClue.transform.position;
                soundSource = soundObject.AddComponent<AudioSource>();
                soundSource.clip = hintSound;
                soundSource.playOnAwake = false;
                soundSource.loop = true;
                soundSource.spatialBlend = 1f;
                soundSource.spatialize = useSpatializer;
                soundSource.volume = soundVolume;
                soundSource.rolloffMode = AudioRolloffMode.Logarithmic;
                soundSource.minDistance = soundMinDistance;
                soundSource.maxDistance = Mathf.Max(soundMinDistance, soundMaxDistance);
                soundSource.dopplerLevel = 0f;
                soundSource.Play();
            }
            else
                Debug.LogWarning("Clue hint has no sound clip assigned.", this);

            onSoundHint?.Invoke();
        }

        private void ShowChevron()
        {
            if (targetClue == null || targetClue.IsComplete || targetClue.HasBeenFound)
            {
                DismissHint();
                return;
            }
            if (chevronPrefab == null)
            {
                Debug.LogWarning("Clue hint has no Chevron prefab assigned.", this);
                DismissHint();
                return;
            }

            chevron = Instantiate(chevronPrefab, playerCamera.transform);
            chevron.name = "Clue Hint Chevron";
            chevron.transform.localScale = chevronPrefab.transform.localScale * chevronScale;
            stage = HintStage.ChevronVisible;
            onChevronShown?.Invoke();
            LateUpdate();
        }

        private void DismissHint()
        {
            bool hadHint = stage != HintStage.Waiting;
            if (chevron != null)
                Destroy(chevron);
            chevron = null;
            if (soundSource != null)
            {
                soundSource.Stop();
                Destroy(soundSource.gameObject);
            }
            soundSource = null;
            targetClue = null;
            stage = HintStage.Waiting;
            quietElapsed = 0f;
            soundElapsed = 0f;
            if (hadHint)
                onHintDismissed?.Invoke();
        }
    }
}
