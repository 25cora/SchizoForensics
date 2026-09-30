using System.Collections;
using Microsoft.MixedReality.Toolkit;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Forensics
{
    /// <summary>Moves the player from room scan to clue search and finally to the win screen.</summary>
    [DisallowMultipleComponent]
    public sealed class GameFlowController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RoomScanController roomScanController;
        [SerializeField] private HidingSpotGenerator hidingSpotGenerator;
        [SerializeField] private ClueProgressController clueProgress;
        [SerializeField] private ClueInspectionController clueInspectionController;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private GameFlowView viewPrefab;
        [SerializeField] private GameObject manualFinishScanButton;

        [Header("Messages")]
        [SerializeField, Min(0f)] private float searchMessageSeconds = 8f;
        [SerializeField, Min(0f)] private float rescanDelaySeconds = 2f;
        [SerializeField, Min(0f)] private float closeHintDelaySeconds = 10f;

        public enum GameMode { Starting, Scanning, Search, Won, Error }
        public GameMode CurrentMode { get; private set; } = GameMode.Starting;

        private GameFlowView view;
        private Coroutine retryRoutine;
        private Coroutine closeHintRoutine;
        private float hideSearchPromptAt;
        private int lastShownSecond = -1;
        private bool retryingScan;

        private void Awake()
        {
            if (roomScanController == null) roomScanController = GetComponent<RoomScanController>();
            if (hidingSpotGenerator == null) hidingSpotGenerator = GetComponent<HidingSpotGenerator>();
            if (clueProgress == null) clueProgress = GetComponent<ClueProgressController>();
            if (clueInspectionController == null)
                clueInspectionController = FindFirstObjectByType<ClueInspectionController>();
            if (playerCamera == null) playerCamera = Camera.main;

            if (manualFinishScanButton != null) manualFinishScanButton.SetActive(false);
            if (viewPrefab != null && playerCamera != null)
            {
                view = Instantiate(viewPrefab, playerCamera.transform, false);
                view.name = "Game Flow HUD";
                if (view.RestartButton != null) view.RestartButton.OnClick.AddListener(RestartGame);
                if (view.ExitButton != null) view.ExitButton.OnClick.AddListener(ExitGame);
            }
        }

        private void OnEnable()
        {
            if (roomScanController != null) roomScanController.StateChanged += HandleScanStateChanged;
            if (hidingSpotGenerator != null)
            {
                hidingSpotGenerator.GenerationSucceeded += HandleGenerationSucceeded;
                hidingSpotGenerator.GenerationFailed += HandleGenerationFailed;
            }
            if (clueProgress != null) clueProgress.AllCluesSolved += HandleAllCluesSolved;
            if (clueInspectionController != null)
            {
                clueInspectionController.InspectionOpened += HandleInspectionOpened;
                clueInspectionController.InspectionClosed += HandleInspectionClosed;
            }
        }

        private void Start()
        {
            if (roomScanController == null || hidingSpotGenerator == null || clueProgress == null || view == null)
            {
                Debug.LogError("GameFlowController needs scan, clue generator, progress, camera, and HUD references.", this);
                CurrentMode = GameMode.Error;
                enabled = false;
                return;
            }
            if (hidingSpotGenerator.SpawnedClues.Count > 0) EnterSearchMode();
            else HandleScanStateChanged(roomScanController.CurrentState);
        }

        private void OnDisable()
        {
            StopCloseHintTimer();
            if (roomScanController != null) roomScanController.StateChanged -= HandleScanStateChanged;
            if (hidingSpotGenerator != null)
            {
                hidingSpotGenerator.GenerationSucceeded -= HandleGenerationSucceeded;
                hidingSpotGenerator.GenerationFailed -= HandleGenerationFailed;
            }
            if (clueProgress != null) clueProgress.AllCluesSolved -= HandleAllCluesSolved;
            if (clueInspectionController != null)
            {
                clueInspectionController.InspectionOpened -= HandleInspectionOpened;
                clueInspectionController.InspectionClosed -= HandleInspectionClosed;
            }
        }

        private void Update()
        {
            if (CurrentMode == GameMode.Scanning && roomScanController.IsScanning)
            {
                int seconds = Mathf.CeilToInt(roomScanController.ScanTimeRemaining);
                if (seconds != lastShownSecond)
                {
                    lastShownSecond = seconds;
                    view.ShowPrompt((retryingScan ? "Look for more room surfaces" : "Look around to scan") +
                                    "\n" + seconds + "s remaining");
                }
            }
            else if (CurrentMode == GameMode.Search &&
                     (clueInspectionController == null || !clueInspectionController.IsInspecting) &&
                     Time.unscaledTime >= hideSearchPromptAt)
            {
                view.HidePrompt();
                hideSearchPromptAt = float.PositiveInfinity;
            }
        }

        private void HandleScanStateChanged(RoomScanController.ScanState state)
        {
            if (roomScanController.CurrentState != state || CurrentMode == GameMode.Won) return;
            switch (state)
            {
                case RoomScanController.ScanState.Initializing:
                    CurrentMode = GameMode.Starting;
                    view?.ShowPrompt("Preparing room scanner...");
                    break;
                case RoomScanController.ScanState.Ready:
                    CurrentMode = GameMode.Starting;
                    view?.ShowPrompt("Ready to scan the room");
                    break;
                case RoomScanController.ScanState.Scanning:
                    CurrentMode = GameMode.Scanning;
                    lastShownSecond = -1;
                    break;
                case RoomScanController.ScanState.Complete:
                    if (hidingSpotGenerator.SpawnedClues.Count > 0) EnterSearchMode();
                    else if (retryRoutine == null) view?.ShowPrompt("Finding places for clues...");
                    break;
                case RoomScanController.ScanState.Error:
                    CurrentMode = GameMode.Error;
                    view?.ShowPrompt("Room scanner unavailable. Check spatial scanning setup.");
                    break;
            }
        }

        private void HandleGenerationSucceeded(int count)
        {
            if (count > 0) EnterSearchMode();
        }

        private void HandleGenerationFailed()
        {
            if (CurrentMode == GameMode.Won || retryRoutine != null) return;
            CurrentMode = GameMode.Scanning;
            retryingScan = true;
            view?.ShowPrompt("Not enough hiding places yet. Look around more.");
            retryRoutine = StartCoroutine(RetryScan());
        }

        private IEnumerator RetryScan()
        {
            yield return new WaitForSecondsRealtime(rescanDelaySeconds);
            retryRoutine = null;
            if (roomScanController != null && roomScanController.IsComplete)
                roomScanController.ResumeScanKeepingMesh();
        }

        private void EnterSearchMode()
        {
            if (CurrentMode == GameMode.Won) return;
            if (retryRoutine != null)
            {
                StopCoroutine(retryRoutine);
                retryRoutine = null;
            }
            CurrentMode = GameMode.Search;
            retryingScan = false;
            hideSearchPromptAt = Time.unscaledTime + searchMessageSeconds;
            view?.ShowPrompt("Search the room\nSelect a clue to inspect it");
        }

        private void HandleInspectionOpened(ClueBase clue)
        {
            if (CurrentMode != GameMode.Search) return;
            StopCloseHintTimer();
            view?.HidePrompt();
            closeHintRoutine = StartCoroutine(ShowCloseHintAfterDelay(clue));
        }

        private IEnumerator ShowCloseHintAfterDelay(ClueBase clue)
        {
            yield return new WaitForSecondsRealtime(closeHintDelaySeconds);
            closeHintRoutine = null;
            if (CurrentMode == GameMode.Search && clue != null && !clue.IsComplete &&
                clueInspectionController != null && clueInspectionController.ActiveClue == clue)
                view?.ShowCloseHint("Swipe an open hand\nacross to close");
        }

        private void StopCloseHintTimer()
        {
            if (closeHintRoutine == null) return;
            StopCoroutine(closeHintRoutine);
            closeHintRoutine = null;
        }

        private void HandleInspectionClosed(ClueBase clue)
        {
            StopCloseHintTimer();
            if (CurrentMode == GameMode.Search)
                view?.HidePrompt();
        }

        private void HandleAllCluesSolved()
        {
            if (CurrentMode != GameMode.Search || clueProgress.TotalClues == 0 ||
                clueProgress.FoundClues != clueProgress.TotalClues ||
                clueProgress.SolvedClues != clueProgress.TotalClues) return;
            CurrentMode = GameMode.Won;
            StopCloseHintTimer();
            clueProgress.HideForWin();
            if (CoreServices.DiagnosticsSystem != null)
                CoreServices.DiagnosticsSystem.ShowProfiler = false;
            view.ShowWin();
        }

        public void RestartGame()
        {
            Time.timeScale = 1f;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
            else SceneManager.LoadScene(scene.name);
        }

        public void ExitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
