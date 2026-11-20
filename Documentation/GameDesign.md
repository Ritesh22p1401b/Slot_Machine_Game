# Game Design & Architecture Notes

## Rules

| Item | Value |
|---|---|
| Reels | 3 reels × 3 visible rows |
| Payline | Middle row only |
| Win condition | `reel1.center == reel2.center == reel3.center` |
| Payout | `bet × symbol.payoutMultiplier` (bet already deducted at spin start) |
| Starting credits | 1,000 (virtual, no cash value) |
| Bets | 10 / 50 / 100 |

Top and bottom rows are drawn by the same RNG but are visual only. To change the rule, edit
`PayoutCalculator.Evaluate` (the only place the rule is implemented).

### Demonstration symbol tuning

| Symbol | Sprite | Weight | P(symbol) | P(3 on line) | Multiplier | RTP contribution |
|---|---|---:|---:|---:|---:|---:|
| SEVEN | `slot-symbol1.png` | 10 | 13.3 % | 0.237 % | 20× | 4.7 % |
| CHERRIES | `slot-symbol2.png` | 30 | 40.0 % | 6.400 % | 2× | 12.8 % |
| BELL | `slot-symbol3.png` | 20 | 26.7 % | 1.896 % | 5× | 9.5 % |
| BAR | `slot-symbol4.png` | 15 | 20.0 % | 0.800 % | 10× | 8.0 % |

Theoretical return ≈ **35 %** of credits wagered, with a hit rate of ≈ 9.3 % of spins. These are
starter values chosen for a demo, not values from the assignment. They live in
`Assets/Settings/Symbols/*.asset` and can be changed in the Inspector without code changes.

## State machine

```
Ready --TrySpin (valid bet)--> Spinning --all reels stopped--> Evaluating --win--> Winning --hold--> Ready
                                                                          \--loss--> Losing  --hold--> Ready
Ready --TrySpin (insufficient credits)--> Ready (message only)
Spinning --reel failure / timeout--> Ready (bet refunded, no payout)
```

Reset, bet changes and new spins are only accepted in `Ready`.

## Spin pipeline

1. `SlotMachineController.TrySpin` checks state and affordability, then debits the bet **once**.
2. All 9 symbols (3 reels × top/center/bottom) are drawn from `RNGService` **before** any animation.
3. Each `ReelController` receives a `ReelSpinPlan` (target column + `ReelMotionProfile`).
4. The reel builds a "tape": current symbols → visual filler symbols → planned result. It scrolls a
   fixed pool of 5 `Image` cells through the tape; nothing is instantiated during a spin.
5. `ReelMotionProfile` (ease-in → cruise → ease-out with a small bounce) travels an exact whole
   number of cells, so the final position is exactly cell-aligned. Reels stop 0.38 s apart.
6. Each reel verifies that its visible symbols equal the plan and reports back.
7. The controller verifies the center row again, evaluates it with `PayoutCalculator`, credits the
   payout **once**, then returns to `Ready`.

## Class responsibilities

| Class | Kind | Responsibility |
|---|---|---|
| `SlotMachineController` | MonoBehaviour | Game flow & state machine |
| `ReelController` | MonoBehaviour | Animates one reel to a predetermined result |
| `ReelMotionProfile` | plain C# | Position-over-time curve, exact cell alignment |
| `RNGService` | plain C# | Weighted symbol selection |
| `IRandomProvider` / `UnityRandomProvider` | plain C# | Injectable RNG source |
| `PayoutCalculator` | plain C# | Payline rule & payout |
| `BalanceManager` | plain C# | Credits: debit, credit, refund, reset, clamping |
| `BetSelector` | plain C# | Allowed bets and current selection |
| `SymbolData` / `SymbolDatabase` / `SlotMachineSettings` | ScriptableObject | Configuration |
| `UIManager` | MonoBehaviour | Displays state; toggles interactability |
| `SlotButtonBindings` | MonoBehaviour | Wires buttons and Space key to the controller |
| `LeverController` | MonoBehaviour | Lever sprite swap (visual only) |
| `PaylineMarker` | MonoBehaviour | Payline arrows idle / spinning / win blink |
| `AudioManager` | MonoBehaviour | Optional SFX (procedural fallback tones) |

## Artwork usage

| Asset | Used as |
|---|---|
| `bg_gradient.png` | Full-screen background |
| `slot-machine5.png` | Reel window backing (behind reels) |
| `slot-machine4.png` | Cabinet frame with transparent windows (in front of reels) |
| `slot-machine2.png` / `slot-machine3.png` | Lever up / lever pulled |
| `slot-symbol1-4.png` | Reel symbols and paytable icons |
| `popup.png` | Control panel (9-sliced) and reset confirmation popup |
| `slot_machine_Middle_box.png` | Read-out boxes and text buttons |
| `slot_machine_buttons-02.png` | Popup close button (4 states) |
| `slot_machine_buttons-03/04.png` | Payline marker arrows (normal / highlighted / pressed states) |
| `Yes_No_Btn.png` | YES / NO buttons in the reset popup (3 states each) |
| `slot-machine1.png` | Imported, unused (same cabinet with filled windows) |
| `4tXlXs.gif` | Reference only: `Documentation/Reference/` |

Reel window positions were measured from the alpha channel of `slot-machine4.png`
(windows at x 229–336, 359–466, 489–596; y 246–455 in the 816 × 624 image), giving three
108 × 70 px cells per reel.
