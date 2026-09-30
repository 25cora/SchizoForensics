using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.UI;
using UnityEngine;

namespace Forensics
{
    /// <summary>
    /// MRTK tooltip that responds to gaze only while the clue is being inspected.
    /// </summary>
    public sealed class ClueToolTipSpawner : ToolTipSpawner
    {
        [SerializeField] private bool showOnlyOncePerClue = true;
        [SerializeField, Min(1f)] private float extraDistanceFromCamera = 1.25f;

        private bool hasShown;
        private bool cameraGazeFocus;
        private ClueBase clue;
        private ToolTip activeToolTip;

        public override bool HasFocus => FocusEnabled && cameraGazeFocus &&
                                         clue != null && clue.IsBeingInspected;

        private void Awake()
        {
            clue = GetComponent<ClueBase>();
        }

        private void Update()
        {
            Camera view = Camera.main;
            bool lookingAtClue = false;
            if (clue != null && clue.IsBeingInspected && !clue.IsComplete && view != null &&
                Physics.Raycast(view.transform.position, view.transform.forward,
                    out RaycastHit hit, 6f, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
            {
                lookingAtClue = hit.transform == transform ||
                                hit.transform.IsChildOf(transform);
            }

            if (lookingAtClue == cameraGazeFocus)
                return;

            cameraGazeFocus = lookingAtClue;
            if (cameraGazeFocus)
                OnFocusEnter(null);
            else
                base.OnFocusExit(null);
        }

        private void LateUpdate()
        {
            if (activeToolTip == null || !activeToolTip.gameObject.activeSelf ||
                clue == null || !clue.IsBeingInspected)
                return;

            Camera view = Camera.main;
            if (view == null)
                return;

            Transform cameraTransform = view.transform;
            float clueDepth = Vector3.Dot(transform.position - cameraTransform.position,
                                          cameraTransform.forward);
            float tooltipDepth = Mathf.Max(0.2f, clueDepth) +
                                 Mathf.Max(1f, extraDistanceFromCamera);
            activeToolTip.PivotPosition = cameraTransform.position +
                                          cameraTransform.forward * tooltipDepth +
                                          cameraTransform.up * 0.35f;
        }

        public override void OnFocusEnter(FocusEventData eventData)
        {
            if (!HasFocus)
                return;

            InputSourceType? sourceType = eventData?.Pointer?.InputSourceParent?.SourceType;
            if (sourceType == InputSourceType.Hand ||
                sourceType == InputSourceType.Controller)
                return;

            if (showOnlyOncePerClue && hasShown)
                return;

            base.OnFocusEnter(eventData);
        }

        protected override void SpawnableActivated(GameObject spawnable)
        {
            base.SpawnableActivated(spawnable);
            activeToolTip = spawnable.GetComponent<ToolTip>();
            ToolTipConnector connector = spawnable.GetComponent<ToolTipConnector>();
            if (connector != null)
                connector.ConnectorFollowingType = ConnectorFollowType.AnchorOnly;
            if (Camera.main != null)
                spawnable.transform.rotation = Camera.main.transform.rotation;
            LateUpdate();
            hasShown = true;
        }
    }
}
