using Microsoft.MixedReality.Toolkit.Input;
using UnityEngine;

namespace Forensics
{
    /// <summary>One blood-direction string, grabbed directly with a near-hand pinch.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LineRenderer))]
    public sealed class BloodDirectionString : MonoBehaviour, IMixedRealityPointerHandler
    {
        [Header("Inspector references")]
        [SerializeField] private LineRenderer lineRenderer;
        [SerializeField] private BloodStringHandle startHandle;
        [SerializeField] private BloodStringHandle endHandle;

        [Header("Movement bounds")]
        [SerializeField, Tooltip("Half-width and half-height of the editable clue area in local metres.")]
        private Vector2 movementHalfExtents = new Vector2(0.15f, 0.11f);
        [SerializeField, Min(0.01f), Tooltip("A very short line cannot establish a reliable direction.")]
        private float minimumStringLength = 0.06f;

        public bool HasBeenPlaced { get; private set; }
        internal bool CanEdit => owner != null && owner.CanEditStrings;

        private BloodSplatterClue owner;
        private int stringIndex;
        private Vector3 initialStartLocalPosition;
        private Vector3 initialEndLocalPosition;
        private Transform grabSurface;
        private BoxCollider grabCollider;
        private uint activePointerId;
        private int movingEndpointIndex;
        private Vector3 grabOffsetInClueSpace;
        private float grabFraction;
        private Vector3 initialMovingWorldPosition;
        private bool isDragging;
        private bool didMove;

        internal void Initialize(BloodSplatterClue clue, int ownerStringIndex)
        {
            owner = clue;
            stringIndex = ownerStringIndex;

            if (lineRenderer == null)
                lineRenderer = GetComponent<LineRenderer>();

            if (startHandle == null || endHandle == null)
            {
                Debug.LogError($"{name} needs both BloodStringHandle references assigned.", this);
                enabled = false;
                return;
            }

            initialStartLocalPosition = startHandle.transform.localPosition;
            initialEndLocalPosition = endHandle.transform.localPosition;
            EnsureGrabSurface();
            UpdateLine();
        }

        public void OnPointerDown(MixedRealityPointerEventData eventData)
        {
            if (!CanEdit || !(eventData.Pointer is IMixedRealityNearPointer nearPointer) ||
                !nearPointer.TryGetNearGraspPoint(out Vector3 graspPosition))
                return;

            Vector3 start = startHandle.transform.position;
            Vector3 end = endHandle.transform.position;
            movingEndpointIndex = (graspPosition - start).sqrMagnitude <= (graspPosition - end).sqrMagnitude ? 0 : 1;
            initialMovingWorldPosition = movingEndpointIndex == 0 ? start : end;
            Vector3 anchor = movingEndpointIndex == 0 ? end : start;
            Vector3 segment = initialMovingWorldPosition - anchor;
            grabFraction = Mathf.Clamp(Vector3.Dot(graspPosition - anchor, segment) / Mathf.Max(segment.sqrMagnitude, 0.000001f), 0.5f, 1f);
            Vector3 pointOnString = anchor + segment * grabFraction;
            grabOffsetInClueSpace = owner.transform.InverseTransformVector(pointOnString - graspPosition);
            activePointerId = eventData.Pointer.PointerId;
            didMove = false;
            isDragging = true;
            eventData.Use();
        }

        public void OnPointerDragged(MixedRealityPointerEventData eventData)
        {
            if (!isDragging || eventData.Pointer.PointerId != activePointerId ||
                !(eventData.Pointer is IMixedRealityNearPointer nearPointer))
                return;

            if (CanEdit && nearPointer.TryGetNearGraspPoint(out Vector3 graspPosition))
            {
                Vector3 anchor = movingEndpointIndex == 0 ? endHandle.transform.position : startHandle.transform.position;
                Vector3 desiredGrabPoint = graspPosition + owner.transform.TransformVector(grabOffsetInClueSpace);
                Vector3 desiredPosition = anchor + (desiredGrabPoint - anchor) / grabFraction;
                MoveEndpoint(movingEndpointIndex, desiredPosition);
                Vector3 actualPosition = movingEndpointIndex == 0 ? startHandle.transform.position : endHandle.transform.position;
                didMove |= (actualPosition - initialMovingWorldPosition).sqrMagnitude > 0.000004f;
            }
            eventData.Use();
        }

        public void OnPointerUp(MixedRealityPointerEventData eventData)
        {
            if (!isDragging || eventData.Pointer.PointerId != activePointerId)
                return;

            OnPointerDragged(eventData);
            isDragging = false;
            if (didMove && CanEdit)
                owner.NotifyStringPlaced(stringIndex);
            eventData.Use();
        }

        public void OnPointerClicked(MixedRealityPointerEventData eventData)
        {
            if (eventData.Pointer is IMixedRealityNearPointer)
                eventData.Use();
        }

        private void OnDisable()
        {
            isDragging = false;
        }

        internal void MoveEndpoint(int endpointIndex, Vector3 worldPosition)
        {
            if (!CanEdit)
                return;

            Vector3 clamped = owner.ClampToEvidenceArea(worldPosition, movementHalfExtents);
            Transform handle = endpointIndex == 0 ? startHandle.transform : endHandle.transform;
            handle.position = clamped;
            UpdateLine();
        }

        internal void MarkPlaced()
        {
            HasBeenPlaced = true;
        }

        internal Vector2 GetDirectionInClueSpace(Transform clueTransform)
        {
            Vector3 start = clueTransform.InverseTransformPoint(startHandle.transform.position);
            Vector3 end = clueTransform.InverseTransformPoint(endHandle.transform.position);
            Vector2 direction = new Vector2(end.x - start.x, end.y - start.y);
            return direction.magnitude >= minimumStringLength ? direction : Vector2.zero;
        }

        internal void SetColor(Color color)
        {
            if (lineRenderer == null)
                return;
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
        }

        internal void ResetToInspectorPositions()
        {
            startHandle.transform.localPosition = initialStartLocalPosition;
            endHandle.transform.localPosition = initialEndLocalPosition;
            HasBeenPlaced = false;
            isDragging = false;
            UpdateLine();
        }

        private void EnsureGrabSurface()
        {
            // MRTK's sphere pointer only targets colliders with NearInteractionGrabbable.
            // A separate collider follows the rendered segment, including after it rotates.
            var surface = new GameObject("NearHandGrabSurface");
            surface.layer = gameObject.layer;
            grabSurface = surface.transform;
            grabSurface.SetParent(transform, false);
            grabCollider = surface.AddComponent<BoxCollider>();
            surface.AddComponent<NearInteractionGrabbable>();
        }

        private void UpdateLine()
        {
            if (lineRenderer == null || startHandle == null || endHandle == null)
                return;

            Vector3 start = transform.InverseTransformPoint(startHandle.transform.position);
            Vector3 end = transform.InverseTransformPoint(endHandle.transform.position);
            lineRenderer.positionCount = 2;
            if (lineRenderer.useWorldSpace)
            {
                lineRenderer.SetPosition(0, startHandle.transform.position);
                lineRenderer.SetPosition(1, endHandle.transform.position);
            }
            else
            {
                lineRenderer.SetPosition(0, start);
                lineRenderer.SetPosition(1, end);
            }

            if (grabSurface != null)
            {
                Vector3 segment = end - start;
                grabSurface.localPosition = (start + end) * 0.5f;
                grabSurface.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(segment.y, segment.x) * Mathf.Rad2Deg);
                grabCollider.size = new Vector3(segment.magnitude + 0.01f, 0.02f, 0.02f);
            }
        }
    }
}
