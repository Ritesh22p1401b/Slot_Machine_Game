using System;
using System.Collections;
using System.Collections.Generic;
using SlotMachine.Audio;
using SlotMachine.Data;
using SlotMachine.Gameplay;
using SlotMachine.UI;
using UnityEngine;

namespace SlotMachine.Core
{
    /// <summary>
    /// Coordinates one game round and owns the state machine:
    /// Ready -> Spinning -> Evaluating -> Winning/Losing -> Ready.
    /// Rules live in the plain C# services (RNGService, PayoutCalculator, BalanceManager, BetSelector);
    /// animation lives in ReelController; presentation lives in UIManager.
    /// </summary>
    public sealed class SlotMachineController : MonoBehaviour
    {
        public const int ReelCount = 3;

        [Header("Configuration")]
        [SerializeField] private SymbolDatabase symbolDatabase;
        [SerializeField] private SlotMachineSettings settings;

        [Header("Scene references")]
        [SerializeField] private ReelController[] reels = new ReelController[ReelCount];
        [SerializeField] private UIManager ui;
        [SerializeField, Tooltip("Optional.")] private AudioManager audioManager;
        [SerializeField, Tooltip("Optional.")] private LeverController lever;

        private readonly SymbolData[,] plannedGrid = new SymbolData[ReelCount, ReelController.VisibleRows];
        private readonly SymbolData[] centerRow = new SymbolData[ReelCount];

        private IRandomProvider randomProvider;
        private RNGService rng;
        private PayoutCalculator payoutCalculator;
        private BalanceManager balance;
        private BetSelector betSelector;
        private Func<SymbolData> fillerSource;
        private Action<ReelController, bool> reelStoppedHandler;

        private Coroutine spinFlow;
        private int activeBet;
        private bool betAwaitingSettlement;
        private int reelsRemaining;
        private bool reelFailed;
        private bool resetPromptOpen;

        public event Action<GameState> StateChanged;

        public GameState State { get; private set; } = GameState.Ready;
        public bool IsInitialized { get; private set; }
        public long Balance => balance?.Balance ?? 0;
        public int SelectedBet => betSelector?.SelectedBet ?? 0;
        public SpinEvaluation LastEvaluation { get; private set; }

        /// <summary>Centre-row symbols chosen by the RNG for the most recent spin (before animation).</summary>
        public SymbolData GetPlannedCenter(int reel) => plannedGrid[reel, ReelController.CenterRow];

        public IReadOnlyList<ReelController> Reels => reels;

        private void Awake()
        {
            Initialize(new UnityRandomProvider());
        }

        /// <summary>Replaces the random source (deterministic tests). Only allowed while Ready.</summary>
        internal void SetRandomProvider(IRandomProvider provider)
        {
            if (State != GameState.Ready) throw new InvalidOperationException("Cannot change RNG during a spin.");
            randomProvider = provider ?? throw new ArgumentNullException(nameof(provider));
            rng = new RNGService(symbolDatabase.Symbols, randomProvider);
        }

        private void Initialize(IRandomProvider provider)
        {
            var errors = new List<string>();
            if (symbolDatabase == null) errors.Add($"{name}: SymbolDatabase is not assigned.");
            else symbolDatabase.Validate(errors);
            if (settings == null) errors.Add($"{name}: SlotMachineSettings is not assigned.");
            else settings.Validate(errors);

            if (reels == null || reels.Length != ReelCount)
            {
                errors.Add($"{name}: exactly {ReelCount} ReelControllers must be assigned.");
            }
            else
            {
                for (int i = 0; i < reels.Length; i++)
                {
                    if (reels[i] == null) errors.Add($"{name}: reel {i} is not assigned.");
                    else reels[i].ValidateSetup(errors);
                }
            }

            if (ui == null) errors.Add($"{name}: UIManager is not assigned.");
            else ui.ValidateSetup(errors);

            if (audioManager == null) Debug.LogWarning($"{name}: no AudioManager assigned; the game will be silent.", this);
            if (lever == null) Debug.LogWarning($"{name}: no LeverController assigned; lever animation disabled.", this);

            if (errors.Count > 0)
            {
                foreach (string error in errors)
                    Debug.LogError(error, this);
                IsInitialized = false;
                if (ui != null)
                {
                    ui.SetControlsInteractable(false);
                    ui.SetStatus("SETUP ERROR", "See the Console for details.", StatusTone.Warning);
                }

                return;
            }

            randomProvider = provider;
            rng = new RNGService(symbolDatabase.Symbols, randomProvider);
            if (rng.ExcludedCount > 0)
                Debug.LogWarning($"{name}: {rng.ExcludedCount} invalid symbol entries were excluded.", this);

            payoutCalculator = new PayoutCalculator(ReelCount);
            balance = new BalanceManager(settings.StartingBalance);
            betSelector = new BetSelector(settings.BetOptions, settings.DefaultBetIndex);
            fillerSource = () => rng.SelectSymbol();
            reelStoppedHandler = OnReelStopped;

            balance.BalanceChanged += ui.SetBalance;

            foreach (ReelController reel in reels)
                reel.ShowColumn(rng.SelectSymbol(), rng.SelectSymbol(), rng.SelectSymbol(), rng.SelectSymbol(), rng.SelectSymbol());

            ui.SetBetOptions(betSelector.Options);
            ui.SetBalance(balance.Balance);
            ui.SetBet(betSelector.SelectedBet, betSelector.SelectedIndex);
            ui.SetLastWin(0);
            ui.SetSoundEnabled(audioManager == null || audioManager.IsEnabled);
            ui.ShowResetPopup(false);
            ui.SetPaylineState(PaylineState.Idle);
            ui.SetStatus("READY", "Choose a bet and press SPIN or pull the lever.", StatusTone.Neutral);
            ui.SetControlsInteractable(true);

            IsInitialized = true;
        }

        private void OnDestroy()
        {
            if (balance != null && ui != null)
                balance.BalanceChanged -= ui.SetBalance;
        }

        private void OnDisable()
        {
            // Unity stops our coroutines on disable. Never leave a debited bet unsettled.
            if (!IsInitialized || State == GameState.Ready)
                return;

            spinFlow = null;
            foreach (ReelController reel in reels)
            {
                if (reel != null && reel.IsSpinning)
                    reel.AbortSpin();
            }

            if (betAwaitingSettlement)
            {
                balance.Refund(activeBet);
                betAwaitingSettlement = false;
            }

            State = GameState.Ready;
        }

        // ---------------------------------------------------------------- input entry points

        /// <summary>Spin button callback.</summary>
        public void Spin() => TrySpin();

        /// <summary>Lever callback. Uses exactly the same validated path as the Spin button.</summary>
        public void PullLever()
        {
            if (TrySpin() && lever != null)
                lever.PlayPull();
        }

        /// <summary>
        /// Starts a spin if the machine is Ready and the selected bet is affordable.
        /// The bet is debited once here, and the full outcome is generated before any animation.
        /// </summary>
        public bool TrySpin()
        {
            if (!IsInitialized || State != GameState.Ready || resetPromptOpen || spinFlow != null)
                return false;

            int bet = betSelector.SelectedBet;
            if (!balance.TryPlaceBet(bet))
            {
                bool broke = balance.Balance < MinimumBet();
                ui.SetStatus("INSUFFICIENT CREDITS",
                    broke ? "Out of credits - press RESET to start again." : "Choose a lower bet.",
                    StatusTone.Warning);
                audioManager?.PlayError();
                return false; // stays in Ready
            }

            activeBet = bet;
            betAwaitingSettlement = true;

            // Outcome is decided now, before the reels move. Animation only displays it.
            for (int reel = 0; reel < ReelCount; reel++)
                for (int row = 0; row < ReelController.VisibleRows; row++)
                    plannedGrid[reel, row] = rng.SelectSymbol();

            spinFlow = StartCoroutine(SpinFlow());
            return true;
        }

        public void SelectBet(int index)
        {
            if (!IsInitialized || State != GameState.Ready || resetPromptOpen)
                return;
            if (!betSelector.Select(index))
            {
                Debug.LogWarning($"{name}: bet index {index} is out of range.", this);
                return;
            }

            ui.SetBet(betSelector.SelectedBet, betSelector.SelectedIndex);
            ui.SetStatus("READY", $"Bet set to {betSelector.SelectedBet}.", StatusTone.Neutral);
            audioManager?.PlayClick();
        }

        public void RequestReset()
        {
            if (!IsInitialized || State != GameState.Ready)
                return;
            resetPromptOpen = true;
            ui.ShowResetPopup(true, $"RESET CREDITS?\n<size=70%>Balance returns to {balance.StartingBalance:N0} and bet to {settings.BetOptions[settings.DefaultBetIndex]}.</size>");
            audioManager?.PlayClick();
        }

        public void ConfirmReset()
        {
            if (!IsInitialized || State != GameState.Ready)
                return;
            resetPromptOpen = false;
            ui.ShowResetPopup(false);
            balance.Reset();
            betSelector.Reset();
            ui.SetBet(betSelector.SelectedBet, betSelector.SelectedIndex);
            ui.SetLastWin(0);
            ui.SetPaylineState(PaylineState.Idle);
            foreach (ReelController reel in reels)
                reel.StopWinPulse();
            ui.SetStatus("READY", "Credits reset.", StatusTone.Neutral);
            audioManager?.PlayClick();
        }

        public void CancelReset()
        {
            resetPromptOpen = false;
            ui?.ShowResetPopup(false);
            audioManager?.PlayClick();
        }

        public void ToggleSound()
        {
            if (audioManager == null)
                return;
            audioManager.SetEnabled(!audioManager.IsEnabled);
            ui.SetSoundEnabled(audioManager.IsEnabled);
            audioManager.PlayClick();
        }

        // ---------------------------------------------------------------- spin flow

        private IEnumerator SpinFlow()
        {
            SetState(GameState.Spinning);
            ui.SetControlsInteractable(false);
            ui.SetPaylineState(PaylineState.Spinning);
            ui.SetStatus("SPINNING...", $"Bet {activeBet} placed.", StatusTone.Neutral);
            audioManager?.PlaySpinStart();

            reelsRemaining = ReelCount;
            reelFailed = false;
            float longest = 0f;
            for (int i = 0; i < ReelCount; i++)
            {
                float startDelay = i * settings.ReelStartStagger;
                // Stop times are staggered by ReelStopStagger regardless of the start stagger.
                float duration = Mathf.Max(0.3f, settings.FirstReelDuration + i * settings.ReelStopStagger - startDelay);
                var motion = new ReelMotionProfile(duration, settings.SpinSpeed, settings.AccelerationTime, settings.DecelerationTime, settings.StopBounce);
                longest = Mathf.Max(longest, startDelay + motion.Duration);

                var plan = new ReelSpinPlan(
                    plannedGrid[i, ReelController.TopRow],
                    plannedGrid[i, ReelController.CenterRow],
                    plannedGrid[i, ReelController.BottomRow],
                    motion, startDelay, fillerSource);
                reels[i].StartSpin(plan, reelStoppedHandler);
            }

            // Wait for every reel, with a watchdog in case one never reports back.
            float timeout = longest + 3f;
            float waited = 0f;
            while (reelsRemaining > 0 && waited < timeout)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            if (reelsRemaining > 0 || reelFailed)
            {
                FailSpin(reelsRemaining > 0 ? "Reel timed out." : "A reel failed to stop on its result.");
                yield break;
            }

            SetState(GameState.Evaluating);
            for (int i = 0; i < ReelCount; i++)
            {
                centerRow[i] = reels[i].GetVisibleSymbol(ReelController.CenterRow);
                if (centerRow[i] != plannedGrid[i, ReelController.CenterRow])
                {
                    FailSpin($"Reel {i} shows a different center symbol than the generated outcome.");
                    yield break;
                }
            }

            SpinEvaluation evaluation = payoutCalculator.Evaluate(centerRow, activeBet);
            LastEvaluation = evaluation;
            betAwaitingSettlement = false; // the bet is now settled; nothing else may debit or credit it

            if (evaluation.IsWin)
            {
                long credited = balance.AddPayout(evaluation.Payout); // the only place a payout is credited
                SetState(GameState.Winning);
                ui.SetLastWin(credited);
                ui.SetPaylineState(PaylineState.Win);
                ui.SetStatus($"YOU WIN {credited:N0}!",
                    $"3 x {evaluation.WinningSymbol.DisplayName.ToUpperInvariant()} pays {evaluation.WinningSymbol.PayoutMultiplier}x bet",
                    StatusTone.Win);
                foreach (ReelController reel in reels)
                    reel.PlayWinPulse();
                audioManager?.PlayWin();
            }
            else
            {
                SetState(GameState.Losing);
                ui.SetLastWin(0);
                ui.SetPaylineState(PaylineState.Idle);
                ui.SetStatus("TRY AGAIN", "Match all three symbols on the center line to win.", StatusTone.Loss);
                audioManager?.PlayLose();
            }

            float held = 0f;
            while (held < settings.ResultHoldTime)
            {
                held += Time.deltaTime;
                yield return null;
            }

            spinFlow = null;
            SetState(GameState.Ready);
            ui.SetControlsInteractable(true);
        }

        private void OnReelStopped(ReelController reel, bool success)
        {
            if (!success)
                reelFailed = true;
            else
                audioManager?.PlayReelStop();
            reelsRemaining--;
        }

        /// <summary>Recovers from an animation failure: no payout, the bet is refunded, back to Ready.</summary>
        private void FailSpin(string reason)
        {
            Debug.LogError($"{name}: spin failed - {reason} The bet has been refunded.", this);
            foreach (ReelController reel in reels)
            {
                if (reel.IsSpinning)
                    reel.AbortSpin();
            }

            if (betAwaitingSettlement)
            {
                balance.Refund(activeBet);
                betAwaitingSettlement = false;
            }

            LastEvaluation = SpinEvaluation.Loss;
            spinFlow = null;
            SetState(GameState.Ready);
            ui.SetPaylineState(PaylineState.Idle);
            ui.SetStatus("SPIN ERROR", "Bet refunded. Please spin again.", StatusTone.Warning);
            ui.SetControlsInteractable(true);
        }

        private int MinimumBet()
        {
            int min = int.MaxValue;
            foreach (int option in betSelector.Options)
                min = Mathf.Min(min, option);
            return min;
        }

        private void SetState(GameState newState)
        {
            if (State == newState)
                return;
            State = newState;
            StateChanged?.Invoke(newState);
        }
    }
}
