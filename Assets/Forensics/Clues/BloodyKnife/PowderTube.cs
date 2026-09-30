using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.UI;
using Microsoft.MixedReality.Toolkit.Utilities;
using UnityEngine;

namespace Forensics
{
    /// <summary>
    /// A powder tube manipulated with a direct hand pinch. Put a Collider and
    /// NearInteractionGrabbable on the tube prefab, and assign Pour Point to its opening.
    /// The tube's local up axis should point toward that opening when upright.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ObjectManipulator))]
    public sealed class PowderTube : MonoBehaviour
    {
        [SerializeField, Tooltip("The mouth of the tube; defaults to this transform if unassigned.")]
        private Transform pourPoint;

        public bool IsHeld { get; private set; }
        public Vector3 PourPosition => pourPoint != null ? pourPoint.position : transform.position;

        private ObjectManipulator manipulator;

        private void Awake()
        {
            manipulator = GetComponent<ObjectManipulator>();
            manipulator.AllowFarManipulation = false;
            manipulator.ManipulationType = ManipulationHandFlags.OneHanded;
            manipulator.UseForcesForNearManipulation = false;
            manipulator.SmoothingNear = false;

            if (GetComponentInChildren<NearInteractionGrabbable>(true) == null)
                Debug.LogWarning($"{name} needs a Collider with NearInteractionGrabbable for hand pinching.", this);
        }

        private void OnEnable()
        {
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
            IsHeld = false;
        }

        private void HandleGrabStarted(ManipulationEventData eventData)
        {
            IsHeld = eventData.IsNearInteraction;
        }

        private void HandleGrabEnded(ManipulationEventData eventData)
        {
            IsHeld = false;
        }
    }
}
