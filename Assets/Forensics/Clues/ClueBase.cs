using System;
using System.Collections;
using Microsoft.MixedReality.Toolkit.Input;
using UnityEngine;
using UnityEngine.Events;

namespace Forensics
{
    /// <summary>
    /// Base component for every discoverable clue. Derived clues can override the
    /// protected lifecycle hooks and item validation without reimplementing MRTK
    /// pointer handling or inspection presentation.
    /// </summary>
    public abstract class ClueBase : MonoBehaviour, IMixedRealityPointerHandler
    {
        [Header("Clue Identity")]
        [SerializeField] private string clueId;
        [SerializeField, TextArea] private string description;

        [Header("Inspection")]
        [SerializeField] private Vector3 inspectionEulerOffset = new Vector3(0f, 180f, 0f);
        [SerializeField, Min(0.1f)] private float inspectionScaleMultiplier = 1.5f;
        [SerializeField] private bool closeWhenClickedDuringInspection = true;
        [SerializeField] private bool returnToHidingSpotWhenClosed = true;

        [Header("Item Task")]
        [SerializeField] private string[] acceptedItemIds = Array.Empty<string>();
        [SerializeField] private bool completeWhenCorrectItemIsUsed = true;
        [SerializeField] private bool completeWhenFirstInspected;
        [SerializeField, Min(0f)] private float completionPreviewSeconds = 1.5f;

        [Header("Events")]
        [SerializeField] private UnityEvent onFound;
        [SerializeField] private UnityEvent onInspectionStarted;
        [SerializeField] private UnityEvent onInspectionEnded;
        [SerializeField] private UnityEvent onCorrectItemUsed;
        [SerializeField] private UnityEvent onWrongItemUsed;
        [SerializeField] private UnityEvent onCompleted;
        [SerializeField] private UnityEvent onReset;

        public string ClueId => clueId;
        public event Action<ClueBase> Found;
        public event Action<ClueBase> CompletionStarted;
        public event Action<ClueBase> CompletionCancelled;
        public event Action<ClueBase> Completed;
        public string Description => description;
        public bool HasBeenFound { get; private set; }
        public bool IsBeingInspected { get; private set; }
        public bool IsComplete { get; private set; }
        public Vector3 InspectionEulerOffset => inspectionEulerOffset;
        public float InspectionScaleMultiplier => inspectionScaleMultiplier;
        public bool ReturnToHidingSpotWhenClosed => returnToHidingSpotWhenClosed;

        protected ClueInspectionController InspectionController { get; private set; }
        private Coroutine completionRoutine;

        protected virtual void Awake()
        {
            if (GetComponentInChildren<Collider>() == null)
            {
                Debug.LogWarning($"Clue '{name}' needs a Collider for MRTK pointer selection.", this);
            }
        }

        public void SetInspectionController(ClueInspectionController controller)
        {
            InspectionController = controller;
        }

        public void FindAndInspect()
        {
            if (IsComplete)
            {
                return;
            }

            if (InspectionController == null)
            {
                InspectionController = FindFirstObjectByType<ClueInspectionController>();
            }

            if (InspectionController == null)
            {
                Debug.LogError("No ClueInspectionController exists in the scene.", this);
                return;
            }

            if (!HasBeenFound)
            {
                HasBeenFound = true;
                onFound?.Invoke();
                OnDiscovered();
                Found?.Invoke(this);
            }

            InspectionController.Inspect(this);
        }

        /// <summary>
        /// Inventory code calls this with its stable item identifier.
        /// Override IsItemAccepted for clue-specific rules.
        /// </summary>
        public bool TryUseItem(string itemId)
        {
            if (!IsBeingInspected || IsComplete || string.IsNullOrWhiteSpace(itemId))
            {
                return false;
            }

            if (IsItemAccepted(itemId))
            {
                onCorrectItemUsed?.Invoke();
                OnItemAccepted(itemId);

                if (completeWhenCorrectItemIsUsed)
                {
                    CompleteClue();
                }

                return true;
            }

            onWrongItemUsed?.Invoke();
            OnItemRejected(itemId);
            return false;
        }

        public void CompleteClue()
        {
            if (IsComplete)
            {
                return;
            }

            IsComplete = true;
            if (IsBeingInspected && InspectionController != null && InspectionController.ActiveClue == this)
            {
                CompletionStarted?.Invoke(this);
                completionRoutine = StartCoroutine(FinishCompletionAfterPreview());
            }
            else
            {
                FinishCompletion();
            }
        }

        private IEnumerator FinishCompletionAfterPreview()
        {
            yield return new WaitForSecondsRealtime(completionPreviewSeconds);
            completionRoutine = null;
            if (InspectionController != null && InspectionController.ActiveClue == this)
                InspectionController.CloseInspection();
            FinishCompletion();
        }

        private void FinishCompletion()
        {
            onCompleted?.Invoke();
            OnClueCompleted();
            Completed?.Invoke(this);
        }

        /// <summary>Reopen a completed clue for another attempt without changing its discovery state.</summary>
        protected void ResetCompletion()
        {
            if (completionRoutine != null)
            {
                StopCoroutine(completionRoutine);
                completionRoutine = null;
                CompletionCancelled?.Invoke(this);
            }
            IsComplete = false;
            onReset?.Invoke();
        }

        internal void NotifyInspectionStarted()
        {
            IsBeingInspected = true;
            onInspectionStarted?.Invoke();
            OnInspectionBegan();

            if (completeWhenFirstInspected)
            {
                CompleteClue();
            }
        }

        internal void NotifyInspectionEnded()
        {
            IsBeingInspected = false;
            onInspectionEnded?.Invoke();
            OnInspectionFinished();
        }

        protected virtual bool IsItemAccepted(string itemId)
        {
            return Array.Exists(
                acceptedItemIds,
                acceptedId => string.Equals(acceptedId, itemId, StringComparison.OrdinalIgnoreCase));
        }

        protected virtual void OnDiscovered() { }
        protected virtual void OnInspectionBegan() { }
        protected virtual void OnInspectionFinished() { }
        protected virtual void OnItemAccepted(string itemId) { }
        protected virtual void OnItemRejected(string itemId) { }
        protected virtual void OnClueCompleted() { }

        public virtual void OnPointerClicked(MixedRealityPointerEventData eventData)
        {
            if (IsBeingInspected && closeWhenClickedDuringInspection)
            {
                InspectionController?.CloseInspection();
            }
            else
            {
                FindAndInspect();
            }

            eventData.Use();
        }

        public virtual void OnPointerDown(MixedRealityPointerEventData eventData) { }
        public virtual void OnPointerDragged(MixedRealityPointerEventData eventData) { }
        public virtual void OnPointerUp(MixedRealityPointerEventData eventData) { }
    }
}
