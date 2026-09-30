using Microsoft.MixedReality.Toolkit.UI;
using UnityEngine;

namespace Forensics
{
    /// <summary>A clue piece that can only be moved by a near hand pinch.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ObjectManipulator))]
    public sealed class NearCluePiece : MonoBehaviour
    {
        public bool IsHeld { get; private set; }
        private ObjectManipulator manipulator;

        private void Awake()
        {
            manipulator = GetComponent<ObjectManipulator>();
            manipulator.AllowFarManipulation = false;
            manipulator.UseForcesForNearManipulation = false;
            manipulator.SmoothingNear = false;
        }

        private void OnEnable()
        {
            if (manipulator == null) manipulator = GetComponent<ObjectManipulator>();
            manipulator.OnManipulationStarted.AddListener(GrabStarted);
            manipulator.OnManipulationEnded.AddListener(GrabEnded);
        }

        private void OnDisable()
        {
            if (manipulator != null)
            {
                manipulator.OnManipulationStarted.RemoveListener(GrabStarted);
                manipulator.OnManipulationEnded.RemoveListener(GrabEnded);
            }
            IsHeld = false;
        }

        private void GrabStarted(ManipulationEventData data)
        {
            IsHeld = data.IsNearInteraction;
        }

        private void GrabEnded(ManipulationEventData data)
        {
            IsHeld = false;
        }
    }
}
