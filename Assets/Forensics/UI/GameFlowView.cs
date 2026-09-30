using Microsoft.MixedReality.Toolkit.UI;
using TMPro;
using UnityEngine;

namespace Forensics
{
    /// <summary>Camera-facing prompts and the two actions shown after solving the case.</summary>
    public sealed class GameFlowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text promptText;
        [SerializeField] private GameObject promptPanel;
        [SerializeField] private GameObject winPanel;
        [SerializeField] private Interactable restartButton;
        [SerializeField] private Interactable exitButton;

        private Vector3 promptDefaultPosition;
        private Vector3 promptDefaultScale;
        private static readonly Vector3 CloseHintPosition = new Vector3(0f, -0.14f, 0.43f);

        public Interactable RestartButton => restartButton;
        public Interactable ExitButton => exitButton;

        private void Awake()
        {
            if (promptPanel != null)
            {
                promptDefaultPosition = promptPanel.transform.localPosition;
                promptDefaultScale = promptPanel.transform.localScale;
            }
            HidePrompt();
            if (winPanel != null)
                winPanel.SetActive(false);
        }

        public void ShowPrompt(string message)
        {
            if (promptPanel != null)
            {
                promptPanel.transform.localPosition = promptDefaultPosition;
                promptPanel.transform.localScale = promptDefaultScale;
            }
            SetPrompt(message);
        }

        public void ShowCloseHint(string message)
        {
            if (promptPanel != null)
            {
                // Keep the close instruction below and in front of the inspected clue.
                promptPanel.transform.localPosition = CloseHintPosition;
                promptPanel.transform.localScale = Vector3.one * 0.7f;
            }
            SetPrompt(message);
        }

        private void SetPrompt(string message)
        {
            if (promptText == null) return;
            promptText.text = message;
            if (promptPanel != null) promptPanel.SetActive(true);
            else promptText.gameObject.SetActive(true);
        }

        public void HidePrompt()
        {
            if (promptPanel != null) promptPanel.SetActive(false);
            else if (promptText != null) promptText.gameObject.SetActive(false);
        }

        public void ShowWin()
        {
            HidePrompt();
            if (winPanel != null)
                winPanel.SetActive(true);
        }
    }
}
