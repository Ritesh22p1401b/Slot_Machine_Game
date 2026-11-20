# Unity Editor Setup & Checklist

The scene, ScriptableObjects, sprite slicing and import settings are produced by editor scripts in
`Assets/Editor/` using Unity's own APIs. Unity writes all `.unity`, `.asset` and `.meta` files.
You can rebuild everything from the menu, or check and adjust it by hand using the reference below.

## Rebuild from the menu (Unity Editor)

`Tools ▸ Slot Machine`:

| Step | Menu item | What it does |
|---|---|---|
| 0 | Import TMP Essentials | Imports TextMeshPro fonts/shaders (skipped if already present) |
| 1 | Configure Art Imports | Sprite import settings; slices button sheets by alpha bands |
| 2 | Create Data Assets | Creates `Symbol_*.asset`, `SymbolDatabase.asset`, `SlotMachineSettings.asset` (existing tuning is kept) |
| 3 | Build Main Scene | Regenerates `Assets/Scenes/MainGame.unity` and adds it to the build scene list |
| 4 | Build Web (Production) | Writes the Web build to `Build/WebGL/` |
| – | Run Steps 1-3 | Steps 1–3 in one go |
| – | Capture Layout Screenshots | Renders the scene to `Documentation/Screenshots/` |

> Step 3 **overwrites** `MainGame.unity`. If you hand-edit the scene afterwards, don't re-run it
> (or re-apply your edits).

## Rebuild from the command line

```powershell
$unity = "C:\Program Files\Unity\Hub\Editor\6000.6.10f1\Editor\Unity.exe"
$proj  = "<path>\UnitySlotGame"
# TMP import is asynchronous: run WITHOUT -quit, the method exits by itself.
& $unity -batchmode -nographics -projectPath $proj -executeMethod SlotMachine.EditorTools.SlotProjectSetup.ImportTmpEssentials -logFile tmp.log
& $unity -batchmode -quit -nographics -projectPath $proj -executeMethod SlotMachine.EditorTools.SlotProjectSetup.SetupProject -logFile setup.log
& $unity -batchmode -quit -nographics -buildTarget WebGL -projectPath $proj -executeMethod SlotMachine.EditorTools.SlotProjectSetup.BuildWeb -logFile build.log
# Tests
& $unity -batchmode -nographics -projectPath $proj -runTests -testPlatform EditMode -testResults editmode.xml
& $unity -batchmode -projectPath $proj -runTests -testPlatform PlayMode -testResults playmode.xml
```

## Manual verification checklist

- [ ] Project opens with no Console errors.
- [ ] `Assets/Art/Provided/Slot Machine/*.png` are `Sprite (2D and UI)` with alpha transparency.
- [ ] Sprite Editor shows `Close_State_01–04`, `ArrowRight_State_01–04`, `ArrowLeft_State_01–04`,
      `Yes_State_01–03`, `No_State_01–03`. Each slice should frame one button completely.
- [ ] `Assets/Settings/SymbolDatabase.asset` lists the 4 symbols; each has a sprite, a weight and a multiplier.
- [ ] Open `Assets/Scenes/MainGame.unity`, press Play, then spin, change bets, win/lose, reset, and toggle sound.
- [ ] Game view at 1920×1080 and at 4:3: nothing is clipped.

## Scene reference (what step 3 creates)

```
Main Camera            (orthographic, solid colour)
EventSystem            (EventSystem + StandaloneInputModule; project uses the legacy Input Manager)
Canvas                 (Screen Space Overlay, CanvasScaler 1920×1080, Expand)
├─ Background                 Image bg_gradient (stretch)
├─ SlotMachine                816×624, scale 1.56, anchored bottom-centre (-300, 478)
│  ├─ ReelWindowBacking       Image slot-machine5 (stretch)
│  ├─ Reels
│  │  ├─ Reel1  (-125.5,-38.5) 108×210  RectMask2D + ReelController
│  │  │  └─ Strip  108×350
│  │  │     └─ Cell0..Cell4  64×64 at y = +140, +70, 0, -70, -140
│  │  ├─ Reel2  (4.5,-38.5)
│  │  └─ Reel3  (134.5,-38.5)
│  ├─ Cabinet                 Image slot-machine4 (stretch; transparent windows)
│  ├─ PaylineMarker_Left/Right  Image + PaylineMarker (arrow sheets)
│  ├─ PaylineRule             TMP "MATCH 3 ON THE CENTER LINE"
│  ├─ Lever                   Image slot-machine2 + LeverController
│  └─ LeverHitArea            transparent Image + Button
├─ ControlPanel               Image popup (sliced), 620×1010 at (560,-10)
│  ├─ Title, Subtitle, CreditsLabel, CreditsBox/Value, BetLabel, BetBox/Value, WinLabel, WinBox/Value
│  ├─ Bet10Button, Bet50Button, Bet100Button   (Middle_box + Button + Label)
│  ├─ SpinButton
│  ├─ StatusText, DetailText
│  ├─ Paytable (4 rows: icon + "NAME xN")
│  └─ ResetButton, SoundButton
└─ ResetPopup (inactive)      dimmer Image
   └─ PopupPanel              popup.png: Message, YesButton, NoButton, CloseButton (sprite-swap)
Audio                  AudioSource + AudioManager
UIManager              UIManager
SlotMachineController  SlotMachineController + SlotButtonBindings
```

### Serialized references

| Component | Field | Assigned to |
|---|---|---|
| SlotMachineController | symbolDatabase / settings | `Assets/Settings/SymbolDatabase.asset` / `SlotMachineSettings.asset` |
| | reels[0..2] | Reel1, Reel2, Reel3 |
| | ui / audioManager / lever | UIManager / Audio / Lever |
| ReelController | viewport / strip / cellImages[0..4] / cellHeight | self / Strip / Cell0..4 / 70 |
| UIManager | balanceText, betText, winText, statusText, detailText | the matching TMP texts |
| | spinButton, leverButton, resetButton, betButtons[3], betButtonLabels[3], soundLabel | the matching buttons/labels |
| | resetPopup, resetPopupMessage, paylineMarkers[2] | ResetPopup, Message, both markers |
| SlotButtonBindings | controller + every button incl. Yes/No/Close | as named |
| LeverController | leverImage / upSprite / pulledSprite | Lever / slot-machine2 / slot-machine3 |
| AudioManager | source | AudioSource on the same object |

Button clicks are wired in code by `SlotButtonBindings` (no OnClick lists to maintain).
