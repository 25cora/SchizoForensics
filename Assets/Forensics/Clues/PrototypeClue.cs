using Microsoft.MixedReality.Toolkit.UI;
using UnityEngine;

namespace Forensics
{
    /// <summary>
    /// Hair clue: pinch it directly and turn it over during inspection.
    /// The inspection controller keeps its position anchored in the view.
    /// </summary>
    [RequireComponent(typeof(ObjectManipulator))]
    public sealed class PrototypeClue : ClueBase
    {
        [SerializeField, Range(90f, 180f)] private float turnThresholdDegrees = 165f;

        private ObjectManipulator manipulator;
        private Quaternion inspectionStartRotation;
        private bool directGrabActive;

        protected override void Awake()
        {
            base.Awake();
            manipulator = GetComponent<ObjectManipulator>();
            manipulator.AllowFarManipulation = false;
            manipulator.UseForcesForNearManipulation = false;
            manipulator.SmoothingNear = false;
            manipulator.OneHandRotationModeNear = ObjectManipulator.RotateInOneHandType.RotateAboutObjectCenter;
        }

        private void OnEnable()
        {
            if (manipulator == null) manipulator = GetComponent<ObjectManipulator>();
            manipulator.OnManipulationStarted.AddListener(HandleGrabStarted);
            manipulator.OnManipulationEnded.AddListener(HandleGrabEnded);
        }

        private void OnDisable()
        {
            if (manipulator != null)
            {
                manipulator.OnManipulationStarted.RemoveListener(HandleGrabStarted);
                manipulator.OnManipulationEnded.RemoveListener(HandleGrabEnded);
            }
            directGrabActive = false;
        }

        protected override void OnInspectionBegan()
        {
            // Give every attempt a stable orientation, including a reopened clue.
            inspectionStartRotation = Quaternion.Euler(InspectionEulerOffset);
            transform.localRotation = inspectionStartRotation;
            transform.localPosition = Vector3.zero;
            directGrabActive = false;
        }

        protected override void OnInspectionFinished()
        {
            directGrabActive = false;
        }

        private void LateUpdate()
        {
            if (!directGrabActive || !IsBeingInspected || IsComplete) return;

            if (Quaternion.Angle(inspectionStartRotation, transform.localRotation) >= turnThresholdDegrees)
                CompleteClue();
        }

        private void HandleGrabStarted(ManipulationEventData eventData)
        {
            if (!eventData.IsNearInteraction || !IsBeingInspected || IsComplete) return;
            directGrabActive = true;
            InspectionController?.BeginDirectManipulation(this);
        }

        private void HandleGrabEnded(ManipulationEventData eventData)
        {
            if (!directGrabActive) return;
            directGrabActive = false;
            InspectionController?.EndDirectManipulation(this);
        }
    }
}

