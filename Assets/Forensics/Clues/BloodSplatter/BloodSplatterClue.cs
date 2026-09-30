using UnityEngine;
using UnityEngine.Events;

namespace Forensics
{
    /// <summary>
    /// Validates two Inspector-authored direction strings against a configurable
    /// direction in the clue's local XY plane.
    /// </summary>
    public sealed class BloodSplatterClue : ClueBase
    {
        [Header("Inspector-authored clue parts")]
        [SerializeField, Tooltip("Parent containing the two strings and their handles.")]
        private GameObject stringInteractionRoot;
        [SerializeField] private BloodDirectionString firstString;
        [SerializeField] private BloodDirectionString secondString;
        [SerializeField, Tooltip("Local Z offset that keeps strings slightly in front of the splatter and prevents flickering.")]
        private float interactionPlaneOffset = 0.006f;
        private Collider selectionCollider;

        [Header("Correct answer")]
        [SerializeField, Tooltip("Correct undirected direction in this clue's local XY plane. (1, 0.4663) is about 25 degrees.")]
        private Vector2 correctDirection = new Vector2(1f, 0.4663f);
        [SerializeField, Range(1f, 45f)] private float allowedAngleError = 6f;
        [SerializeField] private bool completeAutomaticallyWhenCorrect = true;

        [Header("Feedback")]
        [SerializeField] private Color editingColor = new Color(1f, 0.72f, 0.12f, 1f);
        [SerializeField] private Color correctColor = new Color(0.15f, 1f, 0.35f, 1f);

        [Header("Blood string events")]
        [SerializeField] private UnityEvent onStringPlacementChanged;
        [SerializeField] private UnityEvent onBothStringsCorrect;
        [SerializeField] private UnityEvent onStringsReset;

        public float FirstStringError { get; private set; }
        public float SecondStringError { get; private set; }
        public bool AreBothStringsCorrect { get; private set; }

        protected override void Awake()
        {
            base.Awake();

            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            selectionCollider = GetComponent<Collider>();
            firstString.Initialize(this, 0);
            secondString.Initialize(this, 1);
            SetInteractionVisible(false);
            RefreshValidation(false);
        }

        private void Start()
        {
            // The room generator faces clues toward the player. This flat clue
            // should instead lie on the scanned floor until inspection begins.
            transform.rotation = Quaternion.FromToRotation(transform.forward, Vector3.up) *
                                 transform.rotation;
        }
        protected override void OnInspectionBegan()
        {
            if (selectionCollider != null) selectionCollider.enabled = false;
            SetInteractionVisible(true);
            RefreshValidation(false);
        }

        protected override void OnInspectionFinished()
        {
            SetInteractionVisible(false);
            if (selectionCollider != null) selectionCollider.enabled = true;
        }

        internal bool CanEditStrings => IsBeingInspected && !IsComplete;

        internal Plane GetInteractionPlane()
        {
            Vector3 planePoint = transform.TransformPoint(0f, 0f, interactionPlaneOffset);
            return new Plane(transform.forward, planePoint);
        }

        internal Vector3 ClampToEvidenceArea(Vector3 worldPosition, Vector2 halfExtents)
        {
            Vector3 local = transform.InverseTransformPoint(worldPosition);
            local.x = Mathf.Clamp(local.x, -halfExtents.x, halfExtents.x);
            local.y = Mathf.Clamp(local.y, -halfExtents.y, halfExtents.y);
            local.z = interactionPlaneOffset;
            return transform.TransformPoint(local);
        }

        internal void NotifyStringPlaced(int stringIndex)
        {
            if (stringIndex == 0)
            {
                firstString.MarkPlaced();
            }
            else if (stringIndex == 1)
            {
                secondString.MarkPlaced();
            }

            RefreshValidation(true);
        }

        public bool CheckStrings()
        {
            RefreshValidation(true);
            return AreBothStringsCorrect;
        }

        public void ResetStrings()
        {
            if (firstString == null || secondString == null)
            {
                return;
            }

            ResetCompletion();
            firstString.ResetToInspectorPositions();
            secondString.ResetToInspectorPositions();
            RefreshValidation(true);
            onStringsReset?.Invoke();
        }

        private void RefreshValidation(bool invokeEvents)
        {
            FirstStringError = GetAngularError(firstString);
            SecondStringError = GetAngularError(secondString);

            bool wasCorrect = AreBothStringsCorrect;
            AreBothStringsCorrect = firstString.HasBeenPlaced && secondString.HasBeenPlaced &&
                                    FirstStringError <= allowedAngleError &&
                                    SecondStringError <= allowedAngleError;

            firstString.SetColor(firstString.HasBeenPlaced && FirstStringError <= allowedAngleError
                ? correctColor
                : editingColor);
            secondString.SetColor(secondString.HasBeenPlaced && SecondStringError <= allowedAngleError
                ? correctColor
                : editingColor);

            if (invokeEvents)
            {
                onStringPlacementChanged?.Invoke();
            }

            if (AreBothStringsCorrect && !wasCorrect)
            {
                onBothStringsCorrect?.Invoke();
                if (completeAutomaticallyWhenCorrect)
                {
                    CompleteClue();
                }
            }
        }

        private float GetAngularError(BloodDirectionString directionString)
        {
            Vector2 actualDirection = directionString.GetDirectionInClueSpace(transform);
            if (actualDirection.sqrMagnitude < 0.000001f || correctDirection.sqrMagnitude < 0.000001f)
            {
                return 180f;
            }

            // Abs makes the string undirected: swapping its endpoints remains valid.
            float dot = Mathf.Abs(Vector2.Dot(actualDirection.normalized, correctDirection.normalized));
            return Mathf.Acos(Mathf.Clamp01(dot)) * Mathf.Rad2Deg;
        }

        private void SetInteractionVisible(bool visible)
        {
            if (stringInteractionRoot != null)
            {
                stringInteractionRoot.SetActive(visible);
            }
        }

        private bool ValidateReferences()
        {
            bool valid = stringInteractionRoot != null && firstString != null && secondString != null;
            if (!valid)
            {
                Debug.LogError(
                    "BloodSplatterClue needs its String Interaction Root and both BloodDirectionString references assigned in the Inspector.",
                    this);
            }

            return valid;
        }
    }
}

