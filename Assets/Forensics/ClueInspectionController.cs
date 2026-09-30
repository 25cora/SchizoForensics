using System;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Utilities;
using UnityEngine;
using UnityEngine.Events;

namespace Forensics
{
    /// <summary>
    /// Presents one clue in the center of the player's field of view. It preserves
    /// the clue's original transform and Rigidbody state, supports button-driven
    /// rotation, and forwards inventory item identifiers to the active clue.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ClueInspectionController : MonoBehaviour
    {
        [Header("Presentation")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Transform inspectionAnchor;
        [SerializeField, Min(0.2f)] private float inspectionDistance = 0.55f;
        [SerializeField] private float verticalOffset = -0.055f;
        [SerializeField, Min(0f)] private float anchorFollowSharpness = 18f;
        [SerializeField, Min(0f)] private float clueMoveSharpness = 22f;
        [SerializeField, Min(0f)] private float clueRotationSharpness = 22f;
        [SerializeField, Min(1f)] private float rotationStepDegrees = 15f;

        [Header("Open hand swipe to close")]
        [SerializeField, Min(0.05f)] private float swipeDistance = 0.14f;
        [SerializeField, Min(0.1f)] private float maxSwipeSeconds = 0.8f;
        [SerializeField, Min(0.03f)] private float openFingerDistance = 0.06f;
        [SerializeField, Min(0.05f)] private float swipeHalfWidth = 0.24f;
        [SerializeField, Min(0.05f)] private float swipeHalfHeight = 0.16f;
        [SerializeField, Min(0.05f)] private float swipeDepthTolerance = 0.22f;
        [SerializeField, Min(0f)] private float swipeReadyDelay = 0.45f;

        [Header("Events")]
        [SerializeField] private UnityEvent onInspectionOpened;
        [SerializeField] private UnityEvent onInspectionClosed;

        public ClueBase ActiveClue { get; private set; }
        public bool IsInspecting => ActiveClue != null;
        public event Action<ClueBase> InspectionOpened;
        public event Action<ClueBase> InspectionClosed;

        private Transform originalParent;
        private Vector3 originalPosition;
        private Quaternion originalRotation;
        private Vector3 originalScale;
        private Vector3 inspectionBaseScale;
        private Quaternion userRotation = Quaternion.identity;
        private bool directManipulationActive;
        private Rigidbody activeRigidbody;
        private bool originalIsKinematic;
        private bool originalUseGravity;
        private float inspectionStartedAt;
        private SwipeSample leftSwipe;
        private SwipeSample rightSwipe;

        private static readonly TrackedHandJoint[] OpenHandFingertips =
        {
            TrackedHandJoint.IndexTip, TrackedHandJoint.MiddleTip,
            TrackedHandJoint.RingTip, TrackedHandJoint.PinkyTip
        };

        private struct SwipeSample
        {
            public bool Tracking;
            public float StartX;
            public float StartedAt;
        }

        private void Awake()
        {
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }

            if (inspectionAnchor == null)
            {
                GameObject anchorObject = new GameObject("Clue Inspection Anchor");
                inspectionAnchor = anchorObject.transform;
            }
        }

        private void Update()
        {
            if (ActiveClue == null || playerCamera == null ||
                Time.unscaledTime - inspectionStartedAt < swipeReadyDelay)
                return;

            CheckSwipe(Handedness.Left, ref leftSwipe);
            if (ActiveClue != null)
                CheckSwipe(Handedness.Right, ref rightSwipe);
        }

        private void CheckSwipe(Handedness hand, ref SwipeSample sample)
        {
            if (!HandJointUtils.TryGetJointPose(TrackedHandJoint.Palm, hand, out MixedRealityPose palm) ||
                !IsOpenHand(hand, palm.Position))
            {
                sample = default;
                return;
            }

            Vector3 delta = palm.Position - ActiveClue.transform.position;
            Transform cameraTransform = playerCamera.transform;
            float x = Vector3.Dot(delta, cameraTransform.right);
            float y = Vector3.Dot(delta, cameraTransform.up);
            float depth = Vector3.Dot(delta, cameraTransform.forward);
            if (Mathf.Abs(x) > swipeHalfWidth || Mathf.Abs(y) > swipeHalfHeight ||
                Mathf.Abs(depth) > swipeDepthTolerance)
            {
                sample = default;
                return;
            }

            if (!sample.Tracking)
            {
                if (Mathf.Abs(x) < swipeDistance * 0.5f) return;
                sample.Tracking = true;
                sample.StartX = x;
                sample.StartedAt = Time.unscaledTime;
                return;
            }

            if (Time.unscaledTime - sample.StartedAt > maxSwipeSeconds)
            {
                sample = default;
                return;
            }

            // Require an open palm to cross the clue from one side to the other.
            if (Mathf.Sign(x) != Mathf.Sign(sample.StartX) &&
                Mathf.Abs(x) >= swipeDistance * 0.5f &&
                Mathf.Abs(x - sample.StartX) >= swipeDistance)
                CloseInspection();
        }

        private bool IsOpenHand(Handedness hand, Vector3 palmPosition)
        {
            int extended = 0;
            foreach (TrackedHandJoint tip in OpenHandFingertips)
            {
                if (!HandJointUtils.TryGetJointPose(tip, hand, out MixedRealityPose pose)) return false;
                if (Vector3.Distance(pose.Position, palmPosition) >= openFingerDistance)
                    extended++;
            }
            return extended >= 3;
        }

        private void LateUpdate()
        {
            if (playerCamera == null || inspectionAnchor == null)
            {
                return;
            }

            Transform cameraTransform = playerCamera.transform;
            Vector3 targetPosition = cameraTransform.position +
                                     cameraTransform.forward * inspectionDistance +
                                     cameraTransform.up * verticalOffset;
            Quaternion targetRotation = cameraTransform.rotation;
            float followT = Damp(anchorFollowSharpness);

            inspectionAnchor.SetPositionAndRotation(
                Vector3.Lerp(inspectionAnchor.position, targetPosition, followT),
                Quaternion.Slerp(inspectionAnchor.rotation, targetRotation, followT));

            if (ActiveClue == null)
            {
                return;
            }

            float moveT = Damp(clueMoveSharpness);
            float rotationT = Damp(clueRotationSharpness);
            if (directManipulationActive)
            {
                ActiveClue.transform.localPosition = Vector3.zero;
                ActiveClue.transform.localScale = inspectionBaseScale * ActiveClue.InspectionScaleMultiplier;
                return;
            }
            Quaternion targetLocalRotation = Quaternion.Euler(ActiveClue.InspectionEulerOffset) * userRotation;

            ActiveClue.transform.localPosition = Vector3.Lerp(
                ActiveClue.transform.localPosition,
                Vector3.zero,
                moveT);
            ActiveClue.transform.localRotation = Quaternion.Slerp(
                ActiveClue.transform.localRotation,
                targetLocalRotation,
                rotationT);
            ActiveClue.transform.localScale = Vector3.Lerp(
                ActiveClue.transform.localScale,
                inspectionBaseScale * ActiveClue.InspectionScaleMultiplier,
                moveT);
        }

        public void Inspect(ClueBase clue)
        {
            if (clue == null || clue == ActiveClue)
            {
                return;
            }

            if (ActiveClue != null)
            {
                CloseInspection();
            }

            ActiveClue = clue;
            Transform clueTransform = clue.transform;
            originalParent = clueTransform.parent;
            originalPosition = clueTransform.position;
            originalRotation = clueTransform.rotation;
            originalScale = clueTransform.localScale;
            userRotation = Quaternion.identity;
            directManipulationActive = false;
            inspectionStartedAt = Time.unscaledTime;
            leftSwipe = default;
            rightSwipe = default;

            activeRigidbody = clue.GetComponent<Rigidbody>();
            if (activeRigidbody != null)
            {
                originalIsKinematic = activeRigidbody.isKinematic;
                originalUseGravity = activeRigidbody.useGravity;
                activeRigidbody.linearVelocity = Vector3.zero;
                activeRigidbody.angularVelocity = Vector3.zero;
                activeRigidbody.isKinematic = true;
                activeRigidbody.useGravity = false;
            }

            clueTransform.SetParent(inspectionAnchor, true);
            inspectionBaseScale = clueTransform.localScale;
            clue.NotifyInspectionStarted();
            if (ActiveClue == clue)
            {
                onInspectionOpened?.Invoke();
                InspectionOpened?.Invoke(clue);
            }
        }

        public void CloseInspection()
        {
            if (ActiveClue == null)
            {
                return;
            }

            ClueBase clue = ActiveClue;
            ActiveClue = null;
            directManipulationActive = false;
            leftSwipe = default;
            rightSwipe = default;

            if (clue.ReturnToHidingSpotWhenClosed)
            {
                clue.transform.SetParent(originalParent, true);
                clue.transform.SetPositionAndRotation(originalPosition, originalRotation);
                clue.transform.localScale = originalScale;
            }
            else
            {
                clue.transform.SetParent(originalParent, true);
            }

            if (activeRigidbody != null)
            {
                activeRigidbody.isKinematic = originalIsKinematic;
                activeRigidbody.useGravity = originalUseGravity;
            }

            activeRigidbody = null;
            clue.NotifyInspectionEnded();
            onInspectionClosed?.Invoke();
            InspectionClosed?.Invoke(clue);
        }

        public bool UseItemOnActiveClue(string itemId)
        {
            return ActiveClue != null && ActiveClue.TryUseItem(itemId);
        }

        public void BeginDirectManipulation(ClueBase clue)
        {
            if (ActiveClue == clue) directManipulationActive = true;
        }

        public void EndDirectManipulation(ClueBase clue)
        {
            if (ActiveClue != clue || !directManipulationActive) return;
            userRotation = Quaternion.Inverse(Quaternion.Euler(clue.InspectionEulerOffset)) *
                           clue.transform.localRotation;
            directManipulationActive = false;
        }
        public void RotateLeft() => RotateActiveClue(0f, rotationStepDegrees);
        public void RotateRight() => RotateActiveClue(0f, -rotationStepDegrees);
        public void RotateUp() => RotateActiveClue(rotationStepDegrees, 0f);
        public void RotateDown() => RotateActiveClue(-rotationStepDegrees, 0f);

        public void RotateActiveClue(float pitchDegrees, float yawDegrees)
        {
            if (ActiveClue != null)
            {
                userRotation = Quaternion.Euler(pitchDegrees, yawDegrees, 0f) * userRotation;
            }
        }

        private static float Damp(float sharpness)
        {
            return sharpness <= 0f ? 1f : 1f - Mathf.Exp(-sharpness * Time.unscaledDeltaTime);
        }
    }
}

