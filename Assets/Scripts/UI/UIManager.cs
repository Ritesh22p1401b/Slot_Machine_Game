using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlotMachine.UI
{
    public enum StatusTone
    {
        Neutral,
        Win,
        Loss,
        Warning
    }

    /// <summary>
    /// Displays game information and toggles control interactability. Contains no game rules:
    /// the controller tells it what to show.
    /// </summary>
    public sealed class UIManager : MonoBehaviour
    {
        [Header("Read-outs")]
        [SerializeField] private TMP_Text balanceText;
        [SerializeField] private TMP_Text betText;
        [SerializeField] private TMP_Text winText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text detailText;

        [Header("Controls")]
        [SerializeField] private Button spinButton;
        [SerializeField] private Button leverButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private Button[] betButtons = new Button[0];
        [SerializeField] private TMP_Text[] betButtonLabels = new TMP_Text[0];
        [SerializeField] private TMP_Text soundLabel;

        [Header("Reset confirmation")]
        [SerializeField] private GameObject resetPopup;
        [SerializeField] private TMP_Text resetPopupMessage;

        [Header("Payline")]
        [SerializeField] private PaylineMarker[] paylineMarkers = new PaylineMarker[0];

        [Header("Status colours (text also changes, colour is never the only cue)")]
        [SerializeField] private Color neutralColor = new Color(1f, 0.93f, 0.75f);
        [SerializeField] private Color winColor = new Color(1f, 0.82f, 0.2f);
        [SerializeField] private Color lossColor = new Color(0.75f, 0.8f, 1f);
        [SerializeField] private Color warningColor = new Color(1f, 0.45f, 0.4f);

        private IReadOnlyList<int> betOptions;

        public bool ValidateSetup(List<string> errors)
        {
            int startCount = errors.Count;
            Require(balanceText, nameof(balanceText), errors);
            Require(betText, nameof(betText), errors);
            Require(statusText, nameof(statusText), errors);
            Require(spinButton, nameof(spinButton), errors);
            Require(resetButton, nameof(resetButton), errors);
            Require(resetPopup, nameof(resetPopup), errors);
            if (betButtons == null || betButtons.Length == 0)
                errors.Add($"{name}: no bet buttons assigned.");
            else
                for (int i = 0; i < betButtons.Length; i++)
                    Require(betButtons[i], $"betButtons[{i}]", errors);
            return errors.Count == startCount;
        }

        public void SetBetOptions(IReadOnlyList<int> options)
        {
            betOptions = options;
            if (betButtons.Length != options.Count)
                Debug.LogWarning($"{name}: {betButtons.Length} bet buttons for {options.Count} bet options.", this);
        }

        public void SetBalance(long balance)
        {
            if (balanceText != null)
                balanceText.text = balance.ToString("N0");
        }

        public void SetBet(int bet, int selectedIndex)
        {
            if (betText != null)
                betText.text = bet.ToString("N0");

            if (betOptions == null)
                return;

            for (int i = 0; i < betButtonLabels.Length && i < betOptions.Count; i++)
            {
                if (betButtonLabels[i] == null) continue;
                bool selected = i == selectedIndex;
                // Selected state is shown by text markers and weight, not only colour.
                betButtonLabels[i].text = selected ? $"> BET {betOptions[i]} <" : $"BET {betOptions[i]}";
                betButtonLabels[i].fontStyle = selected ? FontStyles.Bold | FontStyles.Underline : FontStyles.Normal;
            }
        }

        public void SetLastWin(long amount)
        {
            if (winText != null)
                winText.text = amount.ToString("N0");
        }

        public void SetStatus(string message, string detail, StatusTone tone)
        {
            Color color = tone == StatusTone.Win ? winColor
                : tone == StatusTone.Loss ? lossColor
                : tone == StatusTone.Warning ? warningColor
                : neutralColor;

            if (statusText != null)
            {
                statusText.text = message;
                statusText.color = color;
            }

            if (detailText != null)
                detailText.text = detail ?? string.Empty;
        }

        public void SetControlsInteractable(bool interactable)
        {
            SetInteractable(spinButton, interactable);
            SetInteractable(leverButton, interactable);
            SetInteractable(resetButton, interactable);
            foreach (Button button in betButtons)
                SetInteractable(button, interactable);
        }

        public void ShowResetPopup(bool visible, string message = null)
        {
            if (resetPopupMessage != null && message != null)
                resetPopupMessage.text = message;
            if (resetPopup != null)
                resetPopup.SetActive(visible);
        }

        public void SetSoundEnabled(bool enabled)
        {
            if (soundLabel != null)
                soundLabel.text = enabled ? "SOUND: ON" : "SOUND: OFF";
        }

        public void SetPaylineState(PaylineState state)
        {
            foreach (PaylineMarker marker in paylineMarkers)
            {
                if (marker != null)
                    marker.SetState(state);
            }
        }

        private static void SetInteractable(Selectable selectable, bool interactable)
        {
            if (selectable != null)
                selectable.interactable = interactable;
        }

        private void Require(Object reference, string field, List<string> errors)
        {
            if (reference == null)
                errors.Add($"{name}: '{field}' is not assigned.");
        }
    }
}
