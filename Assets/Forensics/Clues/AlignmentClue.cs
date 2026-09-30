using UnityEngine;

namespace Forensics
{
    /// <summary>Complete an inspection by directly moving one piece onto its target pose.</summary>
    [DisallowMultipleComponent]
    public sealed class AlignmentClue : ClueBase
    {
        [SerializeField] private NearCluePiece movablePiece;
        [SerializeField] private Transform targetPose;
        [SerializeField] private GameObject[] hideWhenAligned;
        [SerializeField] private GameObject showWhenAligned;
        [SerializeField, Min(0.005f)] private float positionTolerance = 0.018f;
        [SerializeField, Range(1f, 45f)] private float rotationToleranceDegrees = 12f;
        [SerializeField] private bool matchScale;
        [SerializeField] private bool randomizeScaleOnInspection;
        [SerializeField, Range(0.01f, 0.4f)] private float scaleTolerance = 0.1f;
        [SerializeField] private Vector2 initialScaleFactorRange = new Vector2(0.68f, 1.32f);
        [SerializeField, Range(0f, 0.4f)] private float minimumInitialScaleDifference = 0.18f;

        private Vector3 startingPosition;
        private Quaternion startingRotation;
        private Vector3 startingScale;

        protected override void Awake()
        {
            base.Awake();
            if (movablePiece != null)
            {
                startingPosition = movablePiece.transform.localPosition;
                startingRotation = movablePiece.transform.localRotation;
                startingScale = movablePiece.transform.localScale;
            }
            if (showWhenAligned != null) showWhenAligned.SetActive(false);
        }

        protected override void OnInspectionBegan()
        {
            if (movablePiece != null)
            {
                Transform piece = movablePiece.transform;
                piece.localPosition = startingPosition;
                piece.localRotation = startingRotation;
                float scaleFactor = 1f;
                if (randomizeScaleOnInspection)
                {
                    float min = Mathf.Min(initialScaleFactorRange.x, initialScaleFactorRange.y);
                    float max = Mathf.Max(initialScaleFactorRange.x, initialScaleFactorRange.y);
                    float smallMax = Mathf.Min(max, 1f - minimumInitialScaleDifference);
                    float largeMin = Mathf.Max(min, 1f + minimumInitialScaleDifference);
                    bool chooseSmall = Random.value < 0.5f;
                    scaleFactor = chooseSmall && min < smallMax
                        ? Random.Range(min, smallMax)
                        : Random.Range(largeMin, Mathf.Max(largeMin, max));
                }
                piece.localScale = startingScale * scaleFactor;
            }
            if (showWhenAligned != null) showWhenAligned.SetActive(false);
            foreach (var part in hideWhenAligned)
                if (part != null) part.SetActive(true);
        }

        private void Update()
        {
            if (!IsBeingInspected || IsComplete || movablePiece == null ||
                targetPose == null || !movablePiece.IsHeld)
                return;

            Transform piece = movablePiece.transform;
            if (Vector3.Distance(piece.position, targetPose.position) > positionTolerance ||
                Quaternion.Angle(piece.rotation, targetPose.rotation) > rotationToleranceDegrees)
                return;

            if (matchScale && Mathf.Abs(piece.localScale.x / targetPose.localScale.x - 1f) > scaleTolerance)
                return;

            piece.SetPositionAndRotation(targetPose.position, targetPose.rotation);
            if (matchScale) piece.localScale = targetPose.localScale;
            foreach (var part in hideWhenAligned)
                if (part != null) part.SetActive(false);
            if (showWhenAligned != null) showWhenAligned.SetActive(true);
            CompleteClue();
        }
    }
}
