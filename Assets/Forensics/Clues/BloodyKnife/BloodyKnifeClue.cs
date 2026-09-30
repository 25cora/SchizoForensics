using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Forensics
{
    /// <summary>
    /// Reveal a print by holding an inverted powder tube above the knife while
    /// inspecting it. ClueBase displays the solved check before closing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BloodyKnifeClue : ClueBase
    {
        [Header("Prefab parts")]
        [SerializeField] private PowderTube powderTubePrefab;
        [SerializeField, Tooltip("Optional child transform beside the knife where the tube appears.")]
        private Transform tubeSpawnPoint;
        [SerializeField, Tooltip("Fallback spawn position in knife-local metres when Tube Spawn Point is unassigned.")]
        private Vector3 tubeSpawnLocalOffset = new Vector3(0.18f, 0f, -0.04f);
        [SerializeField, Tooltip("Point on the knife where the powder should fall; defaults to the clue origin.")]
        private Transform fingerprintTarget;
        [SerializeField, Tooltip("Fingerprint artwork on the knife, initially hidden and revealed when powdering succeeds.")]
        private GameObject fingerprintVisual;
        [SerializeField] private Camera playerCamera;

        [Header("Powdering gesture")]
        [SerializeField, Min(0f)] private float minimumHeightAbovePrint = 0.015f;
        [SerializeField, Min(0f)] private float maximumHeightAbovePrint = 0.16f;
        [SerializeField, Min(0f)] private float horizontalTolerance = 0.075f;
        [SerializeField, Min(0f)] private float depthTolerance = 0.08f;
        [SerializeField, Range(0f, 89f), Tooltip("Maximum tilt away from fully upside down, relative to the player's view.")]
        private float upsideDownToleranceDegrees = 50f;
        [SerializeField, Min(0.05f)] private float powderHoldSeconds = 0.5f;

        [Header("Feedback")]
        [SerializeField] private UnityEvent onPrintRevealed;

        private PowderTube activeTube;
        private float validPowderTime;
        private Coroutine spawnTubeRoutine;

        protected override void Awake()
        {
            base.Awake();
            if (playerCamera == null) playerCamera = Camera.main;
            if (fingerprintVisual != null) fingerprintVisual.SetActive(false);
        }

        protected override void OnInspectionBegan()
        {
            validPowderTime = 0f;
            if (fingerprintVisual != null) fingerprintVisual.SetActive(false);
            spawnTubeRoutine = StartCoroutine(SpawnTubeAtInspectionPose());
        }

        protected override void OnInspectionFinished()
        {
            validPowderTime = 0f;
            if (spawnTubeRoutine != null)
            {
                StopCoroutine(spawnTubeRoutine);
                spawnTubeRoutine = null;
            }
            if (activeTube != null)
            {
                activeTube.gameObject.SetActive(false);
                Destroy(activeTube.gameObject);
                activeTube = null;
            }
        }

        private void Start()
        {
            // The room generator orients clues toward the player. This flat
            // knife should lie face-up on the scanned surface until picked up.
            if (!IsBeingInspected)
                transform.rotation = Quaternion.FromToRotation(transform.forward, Vector3.up) *
                                     transform.rotation;
        }

        private IEnumerator SpawnTubeAtInspectionPose()
        {
            // Inspection initially reparents the floor clue without moving it.
            // Wait for its animated move into the view before placing the tube.
            while (IsBeingInspected &&
                   (transform.localPosition.sqrMagnitude > 0.0004f ||
                    Quaternion.Angle(transform.localRotation,
                        Quaternion.Euler(InspectionEulerOffset)) > 8f))
                yield return null;

            if (IsBeingInspected) SpawnTube();
            spawnTubeRoutine = null;
        }

        private void Update()
        {
            if (!IsBeingInspected || IsComplete || activeTube == null || !activeTube.IsHeld)
            {
                validPowderTime = 0f;
                return;
            }

            if (!IsTubePouringOverPrint())
            {
                validPowderTime = 0f;
                return;
            }

            validPowderTime += Time.unscaledDeltaTime;
            if (validPowderTime < powderHoldSeconds) return;

            if (fingerprintVisual != null) fingerprintVisual.SetActive(true);
            onPrintRevealed?.Invoke();
            CompleteClue();
        }

        private void SpawnTube()
        {
            if (powderTubePrefab == null)
            {
                Debug.LogError($"{name} needs a Powder Tube Prefab assigned.", this);
                return;
            }

            if (activeTube != null) Destroy(activeTube.gameObject);
            // Keep the tube's authored world scale. Parenting it under the imported
            // knife mesh would multiply its size by the knife's model scale.
            if (tubeSpawnPoint != null)
            {
                activeTube = Instantiate(powderTubePrefab,
                    tubeSpawnPoint.position, tubeSpawnPoint.rotation);
            }
            else
            {
                activeTube = Instantiate(powderTubePrefab,
                    transform.TransformPoint(tubeSpawnLocalOffset), transform.rotation);
            }
        }

        private bool IsTubePouringOverPrint()
        {
            if (playerCamera == null) playerCamera = Camera.main;
            if (playerCamera == null) return false;

            Transform view = playerCamera.transform;
            Vector3 target = fingerprintTarget != null ? fingerprintTarget.position : transform.position;
            Vector3 delta = activeTube.PourPosition - target;
            float height = Vector3.Dot(delta, view.up);
            float side = Mathf.Abs(Vector3.Dot(delta, view.right));
            float depth = Mathf.Abs(Vector3.Dot(delta, view.forward));
            float inverted = Vector3.Dot(activeTube.transform.up, view.up);

            return height >= minimumHeightAbovePrint &&
                   height <= maximumHeightAbovePrint &&
                   side <= horizontalTolerance &&
                   depth <= depthTolerance &&
                   inverted <= -Mathf.Cos(upsideDownToleranceDegrees * Mathf.Deg2Rad);
        }
    }
}

