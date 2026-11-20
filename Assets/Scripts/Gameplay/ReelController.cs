using System;
using System.Collections;
using System.Collections.Generic;
using SlotMachine.Data;
using UnityEngine;
using UnityEngine.UI;

namespace SlotMachine.Gameplay
{
    /// <summary>Everything one reel needs to perform a spin. The outcome is fixed before the spin starts.</summary>
    public readonly struct ReelSpinPlan
    {
        public ReelSpinPlan(SymbolData top, SymbolData center, SymbolData bottom, ReelMotionProfile motion, float startDelay, Func<SymbolData> fillerSource)
        {
            Top = top;
            Center = center;
            Bottom = bottom;
            Motion = motion;
            StartDelay = startDelay;
            FillerSource = fillerSource;
        }

        public SymbolData Top { get; }
        public SymbolData Center { get; }
        public SymbolData Bottom { get; }
        public ReelMotionProfile Motion { get; }
        public float StartDelay { get; }

        /// <summary>Supplies the purely visual symbols that scroll past before the result.</summary>
        public Func<SymbolData> FillerSource { get; }
    }

    /// <summary>
    /// Animates one vertical reel inside a masked viewport.
    /// The reel uses a fixed pool of five cells (one hidden above, three visible, one hidden below).
    /// Spinning moves the strip down; each time it has moved one full cell, the strip snaps back up
    /// and every cell takes the symbol of the cell above it, read from a pre-built "tape" whose
    /// final entries are the predetermined result. No GameObjects are created during a spin.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ReelController : MonoBehaviour
    {
        public const int VisibleRows = 3;
        public const int CellCount = VisibleRows + 2;
        public const int TopRow = 0;
        public const int CenterRow = 1;
        public const int BottomRow = 2;

        [SerializeField, Tooltip("Masked area (RectMask2D) showing exactly three cells.")]
        private RectTransform viewport;

        [SerializeField, Tooltip("Moving container holding the cell images.")]
        private RectTransform strip;

        [SerializeField, Tooltip("Five cell images, top (hidden) to bottom (hidden).")]
        private Image[] cellImages = new Image[CellCount];

        [SerializeField, Min(1f)] private float cellHeight = 70f;

        [SerializeField, Tooltip("Scale reached by the center symbol while celebrating a win.")]
        private float winPulseScale = 1.18f;

        private readonly SymbolData[] cellSymbols = new SymbolData[CellCount];
        private readonly List<SymbolData> tape = new List<SymbolData>(64);
        private Coroutine spinRoutine;
        private Coroutine pulseRoutine;
        private Action<ReelController, bool> pendingCompletion;
        private int appliedShift = -1;

        public bool IsSpinning => spinRoutine != null;
        public float CellHeight => cellHeight;

        /// <summary>Checks that all serialized references needed for spinning are assigned.</summary>
        public bool ValidateSetup(List<string> errors)
        {
            int startCount = errors.Count;
            if (viewport == null)
                errors.Add($"{name}: viewport is not assigned.");
            else if (viewport.GetComponent<RectMask2D>() == null && viewport.GetComponent<Mask>() == null)
                errors.Add($"{name}: viewport needs a RectMask2D (or Mask) to clip symbols.");
            if (strip == null)
                errors.Add($"{name}: strip is not assigned.");
            if (cellImages == null || cellImages.Length != CellCount)
            {
                errors.Add($"{name}: exactly {CellCount} cell images are required.");
            }
            else
            {
                for (int i = 0; i < cellImages.Length; i++)
                {
                    if (cellImages[i] == null)
                        errors.Add($"{name}: cell image {i} is not assigned.");
                }
            }

            return errors.Count == startCount;
        }

        /// <summary>Immediately shows a column without animation (used at start-up).</summary>
        public void ShowColumn(SymbolData above, SymbolData top, SymbolData center, SymbolData bottom, SymbolData below)
        {
            SetCell(0, above);
            SetCell(1, top);
            SetCell(2, center);
            SetCell(3, bottom);
            SetCell(4, below);
            SetStripOffset(0f);
        }

        /// <summary>Visible symbol on a row: 0 = top, 1 = center (payline), 2 = bottom.</summary>
        public SymbolData GetVisibleSymbol(int row)
        {
            if (row < 0 || row >= VisibleRows) throw new ArgumentOutOfRangeException(nameof(row));
            return cellSymbols[row + 1];
        }

        /// <summary>
        /// Starts spinning towards the planned result. <paramref name="onComplete"/> is invoked exactly
        /// once with true when the reel has stopped on the plan, or false if the spin was aborted or
        /// the final symbols did not match the plan.
        /// </summary>
        public void StartSpin(ReelSpinPlan plan, Action<ReelController, bool> onComplete)
        {
            if (IsSpinning)
            {
                Debug.LogError($"{name}: StartSpin called while already spinning.", this);
                onComplete?.Invoke(this, false);
                return;
            }

            if (plan.Motion == null || plan.FillerSource == null || plan.Top == null || plan.Center == null || plan.Bottom == null)
            {
                Debug.LogError($"{name}: incomplete spin plan.", this);
                onComplete?.Invoke(this, false);
                return;
            }

            StopWinPulse();
            pendingCompletion = onComplete;
            spinRoutine = StartCoroutine(SpinRoutine(plan));
        }

        /// <summary>Stops any spin in progress and reports it as failed.</summary>
        public void AbortSpin()
        {
            if (spinRoutine != null)
            {
                StopCoroutine(spinRoutine);
                spinRoutine = null;
            }

            SetStripOffset(0f);
            Complete(false);
        }

        public void PlayWinPulse()
        {
            StopWinPulse();
            if (isActiveAndEnabled)
                pulseRoutine = StartCoroutine(WinPulseRoutine());
        }

        public void StopWinPulse()
        {
            if (pulseRoutine != null)
            {
                StopCoroutine(pulseRoutine);
                pulseRoutine = null;
            }

            if (cellImages != null && cellImages.Length == CellCount && cellImages[2] != null)
                cellImages[2].rectTransform.localScale = Vector3.one;
        }

        private void OnDisable()
        {
            // Coroutines die with the component; make sure the caller is never left waiting.
            spinRoutine = null;
            pulseRoutine = null;
            if (pendingCompletion != null)
            {
                Debug.LogWarning($"{name}: disabled during a spin; spin aborted.", this);
                Complete(false);
            }
        }

        private IEnumerator SpinRoutine(ReelSpinPlan plan)
        {
            float delay = 0f;
            while (delay < plan.StartDelay)
            {
                delay += Time.deltaTime;
                yield return null;
            }

            int totalCells = plan.Motion.TotalCells;
            BuildTape(plan, totalCells);

            appliedShift = -1;
            float elapsed = 0f;
            float duration = plan.Motion.Duration;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                ApplyPosition(plan.Motion.Evaluate(Mathf.Min(elapsed, duration)));
                yield return null;
            }

            // Final snap: exact whole-cell position, zero fractional offset.
            ApplyPosition(totalCells);
            SetStripOffset(0f);

            spinRoutine = null;
            bool matches = cellSymbols[1] == plan.Top && cellSymbols[2] == plan.Center && cellSymbols[3] == plan.Bottom;
            if (!matches)
                Debug.LogError($"{name}: reel stopped on symbols that differ from the planned outcome.", this);
            Complete(matches);
        }

        /// <summary>
        /// Tape layout: entries 0..4 are the current cells (bottom-hidden first), then visual fillers.
        /// After s whole-cell shifts, cell j shows tape[s + 4 - j]. After the full spin of N cells the
        /// visible rows are tape[N+3] (top), tape[N+2] (center) and tape[N+1] (bottom).
        /// Two extra entries allow a small overshoot past the final position.
        /// </summary>
        private void BuildTape(ReelSpinPlan plan, int totalCells)
        {
            tape.Clear();
            for (int j = CellCount - 1; j >= 0; j--)
                tape.Add(cellSymbols[j] != null ? cellSymbols[j] : plan.FillerSource());

            int length = totalCells + CellCount + 1;
            while (tape.Count < length)
                tape.Add(plan.FillerSource());

            tape[totalCells + 1] = plan.Bottom;
            tape[totalCells + 2] = plan.Center;
            tape[totalCells + 3] = plan.Top;
        }

        private void ApplyPosition(float positionInCells)
        {
            int maxShift = tape.Count - CellCount;
            int shift = Mathf.Clamp(Mathf.FloorToInt(positionInCells), 0, maxShift);
            float fraction = Mathf.Clamp01(positionInCells - shift);

            if (shift != appliedShift)
            {
                for (int j = 0; j < CellCount; j++)
                    SetCell(j, tape[shift + CellCount - 1 - j]);
                appliedShift = shift;
            }

            SetStripOffset(fraction);
        }

        private void SetStripOffset(float fractionOfCell)
        {
            if (strip != null)
                strip.anchoredPosition = new Vector2(0f, -fractionOfCell * cellHeight);
        }

        private void SetCell(int index, SymbolData symbol)
        {
            cellSymbols[index] = symbol;
            Image image = cellImages[index];
            if (image == null) return;
            image.sprite = symbol != null ? symbol.Sprite : null;
            image.enabled = symbol != null && symbol.Sprite != null;
        }

        private void Complete(bool success)
        {
            Action<ReelController, bool> callback = pendingCompletion;
            pendingCompletion = null;
            callback?.Invoke(this, success);
        }

        private IEnumerator WinPulseRoutine()
        {
            RectTransform target = cellImages[2].rectTransform;
            const float period = 0.45f;
            float t = 0f;
            while (true)
            {
                t += Time.deltaTime;
                float wave = 0.5f - 0.5f * Mathf.Cos(t / period * Mathf.PI * 2f);
                target.localScale = Vector3.one * Mathf.Lerp(1f, winPulseScale, wave);
                yield return null;
            }
        }
    }
}
