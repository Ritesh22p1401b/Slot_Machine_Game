using System.Collections.Generic;
using SlotMachine.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SlotMachine.UI
{
    /// <summary>
    /// Connects UI buttons (and the Space key) to the controller in code, so wiring is explicit,
    /// visible in one place and not dependent on hand-edited OnClick lists.
    /// </summary>
    public sealed class SlotButtonBindings : MonoBehaviour
    {
        [SerializeField] private SlotMachineController controller;
        [SerializeField] private Button spinButton;
        [SerializeField] private Button leverButton;
        [SerializeField] private Button[] betButtons = new Button[0];
        [SerializeField] private Button resetButton;
        [SerializeField] private Button resetConfirmButton;
        [SerializeField] private Button resetCancelButton;
        [SerializeField] private Button resetCloseButton;
        [SerializeField] private Button soundButton;
        [SerializeField] private KeyCode spinKey = KeyCode.Space;

        private readonly List<(Button button, UnityAction action)> bound = new List<(Button, UnityAction)>();

        private void OnEnable()
        {
            if (controller == null)
            {
                Debug.LogError($"{name}: SlotMachineController is not assigned; buttons will not work.", this);
                return;
            }

            Bind(spinButton, controller.Spin);
            Bind(leverButton, controller.PullLever);
            for (int i = 0; i < betButtons.Length; i++)
            {
                int index = i; // captured per button
                Bind(betButtons[i], () => controller.SelectBet(index));
            }

            Bind(resetButton, controller.RequestReset);
            Bind(resetConfirmButton, controller.ConfirmReset);
            Bind(resetCancelButton, controller.CancelReset);
            Bind(resetCloseButton, controller.CancelReset);
            Bind(soundButton, controller.ToggleSound);
        }

        private void OnDisable()
        {
            foreach ((Button button, UnityAction action) in bound)
            {
                if (button != null)
                    button.onClick.RemoveListener(action);
            }

            bound.Clear();
        }

        private void Update()
        {
            if (controller != null && Input.GetKeyDown(spinKey))
                controller.PullLever();
        }

        private void Bind(Button button, UnityAction action)
        {
            if (button == null)
            {
                Debug.LogWarning($"{name}: a button reference is missing; that control is disabled.", this);
                return;
            }

            button.onClick.AddListener(action);
            bound.Add((button, action));
        }
    }
}
