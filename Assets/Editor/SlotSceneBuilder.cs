using System.Collections.Generic;
using System.IO;
using System.Linq;
using SlotMachine.Audio;
using SlotMachine.Core;
using SlotMachine.Data;
using SlotMachine.Gameplay;
using SlotMachine.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SlotMachine.EditorTools
{
    /// <summary>
    /// Creates the ScriptableObject configuration and assembles MainGame.unity from the supplied
    /// artwork. Everything is generated through Unity's own APIs, so the scene can be opened and
    /// adjusted by hand afterwards. Re-running overwrites the scene (not the symbol/settings assets).
    /// </summary>
    public static class SlotSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/MainGame.unity";
        private const string SettingsFolder = "Assets/Settings";
        private const string SymbolsFolder = "Assets/Settings/Symbols";
        private const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        // Machine artwork is 816 x 624. Measured from the alpha channel of slot-machine4.png:
        // reel windows are transparent at x 229-336 / 359-466 / 489-596, y 246-455 (from top-left).
        private static readonly Vector2 MachineSize = new Vector2(816, 624);
        private static readonly float[] ReelCenterX = { 282.5f, 412.5f, 542.5f };
        private const float WindowWidth = 108f;
        private const float WindowHeight = 210f;
        private const float WindowCenterY = 350.5f;
        private const float CellHeight = WindowHeight / 3f; // 70
        private const float SymbolSize = 64f;

        private static readonly Color Gold = new Color(1f, 0.84f, 0.35f);
        private static readonly Color Cream = new Color(1f, 0.95f, 0.85f);
        private static readonly Color Muted = new Color(0.78f, 0.78f, 0.95f);

        private static TMP_FontAsset font;

        // ------------------------------------------------------------------ data assets

        [MenuItem("Tools/Slot Machine/2. Create Data Assets", priority = 2)]
        public static void CreateDataAssets()
        {
            EnsureFolder(SettingsFolder);
            EnsureFolder(SymbolsFolder);

            // Demonstration weights and multipliers (spec section 3.2); edit the assets to retune.
            var symbols = new List<SymbolData>
            {
                CreateSymbol("SEVEN", "Seven", "slot-symbol1.png", 10, 20),
                CreateSymbol("CHERRIES", "Cherries", "slot-symbol2.png", 30, 2),
                CreateSymbol("BELL", "Bell", "slot-symbol3.png", 20, 5),
                CreateSymbol("BAR", "BAR", "slot-symbol4.png", 15, 10),
            };

            SymbolDatabase database = LoadOrCreate<SymbolDatabase>($"{SettingsFolder}/SymbolDatabase.asset");
            database.SetSymbols(symbols);
            EditorUtility.SetDirty(database);

            LoadOrCreate<SlotMachineSettings>($"{SettingsFolder}/SlotMachineSettings.asset");
            AssetDatabase.SaveAssets();
            Debug.Log("[SlotSetup] Symbol, database and settings assets ready.");
        }

        private static SymbolData CreateSymbol(string id, string displayName, string spriteFile, int weight, int multiplier)
        {
            string path = $"{SymbolsFolder}/Symbol_{displayName}.asset";
            bool existed = AssetDatabase.LoadAssetAtPath<SymbolData>(path) != null;
            SymbolData symbol = LoadOrCreate<SymbolData>(path);
            Sprite sprite = LoadSprite(spriteFile);
            if (existed)
                // Keep any tuning the developer has done; only (re)link the artwork.
                symbol.Configure(symbol.Id, symbol.DisplayName, sprite, symbol.Weight, symbol.PayoutMultiplier);
            else
                symbol.Configure(id, displayName, sprite, weight, multiplier);
            EditorUtility.SetDirty(symbol);
            return symbol;
        }

        // ------------------------------------------------------------------ scene

        [MenuItem("Tools/Slot Machine/3. Build Main Scene", priority = 3)]
        public static void BuildScene()
        {
            // Create the scene first: NewScene(Single) unloads unused assets, which would invalidate
            // any asset references loaded before it.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
                throw new FileNotFoundException($"TextMeshPro font not found at {FontPath}. Import TMP Essential Resources first.");

            var database = AssetDatabase.LoadAssetAtPath<SymbolDatabase>($"{SettingsFolder}/SymbolDatabase.asset");
            var settings = AssetDatabase.LoadAssetAtPath<SlotMachineSettings>($"{SettingsFolder}/SlotMachineSettings.asset");
            if (database == null || settings == null)
                throw new FileNotFoundException("Run 'Create Data Assets' before building the scene.");

            // Camera + EventSystem
            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.GetComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.07f, 0.05f, 0.17f);
            cameraGo.transform.position = new Vector3(0, 0, -10);

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            // Canvas
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand; // whole layout always visible
            var root = (RectTransform)canvasGo.transform;

            Image background = CreateImage("Background", root, LoadSprite("bg_gradient.png"));
            Stretch(background.rectTransform);
            background.raycastTarget = false;

            // ---------------- machine
            RectTransform machine = CreateRect("SlotMachine", root, Vector2.zero, MachineSize);
            // Anchored to the bottom so the cut-off base of the cabinet always meets the screen edge.
            machine.anchorMin = machine.anchorMax = new Vector2(0.5f, 0f);
            machine.anchoredPosition = new Vector2(-300, 478);
            // Bottom edge of the cabinet art is a cut-off, so the machine bleeds off the bottom of the screen.
            machine.localScale = Vector3.one * 1.56f;

            Image backing = CreateImage("ReelWindowBacking", machine, LoadSprite("slot-machine5.png"));
            Stretch(backing.rectTransform);
            backing.raycastTarget = false;

            RectTransform reelsRoot = CreateRect("Reels", machine, Vector2.zero, MachineSize);
            var reels = new ReelController[3];
            for (int i = 0; i < 3; i++)
                reels[i] = CreateReel(i, reelsRoot, database);

            Image cabinet = CreateImage("Cabinet", machine, LoadSprite("slot-machine4.png"));
            Stretch(cabinet.rectTransform);
            cabinet.raycastTarget = false;

            var markers = new List<PaylineMarker>
            {
                CreatePaylineMarker("PaylineMarker_Left", machine, "slot_machine_buttons-03.png", "ArrowRight_State", ArtToLocal(206f, WindowCenterY)),
                CreatePaylineMarker("PaylineMarker_Right", machine, "slot_machine_buttons-04.png", "ArrowLeft_State", ArtToLocal(620f, WindowCenterY)),
            };

            TMP_Text ruleText = CreateText("PaylineRule", machine, "MATCH 3 ON THE CENTER LINE", 19, Cream, ArtToLocal(415f, 597f), new Vector2(400, 40));
            ruleText.fontStyle = FontStyles.Bold;
            ruleText.characterSpacing = 4;

            // Lever: up and pulled sprites share the cabinet's 816 x 624 canvas, so they align exactly.
            Image leverImage = CreateImage("Lever", machine, LoadSprite("slot-machine2.png"));
            Stretch(leverImage.rectTransform);
            leverImage.raycastTarget = false;
            var lever = leverImage.gameObject.AddComponent<LeverController>();
            SetFields(lever, ("leverImage", leverImage), ("upSprite", LoadSprite("slot-machine2.png")), ("pulledSprite", LoadSprite("slot-machine3.png")));

            // Invisible hit area over the lever (measured bounds x 673-764, y 296-565).
            Image leverHit = CreateImage("LeverHitArea", machine, null);
            leverHit.color = new Color(1, 1, 1, 0);
            SetRect(leverHit.rectTransform, ArtToLocal(718.5f, 430f), new Vector2(110, 290));
            var leverButton = leverHit.gameObject.AddComponent<Button>();
            leverButton.transition = Selectable.Transition.None;
            leverButton.targetGraphic = leverHit;
            SetNoNavigation(leverButton);

            // ---------------- control panel
            Sprite boxSprite = LoadSprite("slot_machine_Middle_box.png");
            Image panel = CreateImage("ControlPanel", root, LoadSprite("popup.png"));
            panel.type = Image.Type.Sliced;
            panel.pixelsPerUnitMultiplier = 2f;
            panel.raycastTarget = false;
            SetRect(panel.rectTransform, new Vector2(560, -10), new Vector2(620, 1010));
            RectTransform p = panel.rectTransform;

            CreateText("Title", p, "JACKPOT", 72, Gold, new Vector2(0, 408), new Vector2(520, 90)).fontStyle = FontStyles.Bold;
            CreateText("Subtitle", p, "VINTAGE SLOT MACHINE", 22, Muted, new Vector2(0, 358), new Vector2(520, 30)).characterSpacing = 6;

            CreateText("CreditsLabel", p, "CREDITS", 22, Muted, new Vector2(0, 302), new Vector2(400, 30)).characterSpacing = 6;
            RectTransform creditsBox = CreateBox("CreditsBox", p, boxSprite, new Vector2(0, 246), new Vector2(500, 96));
            TMP_Text balanceText = CreateText("Value", creditsBox, "1,000", 48, Gold, new Vector2(0, -2), new Vector2(440, 60));
            balanceText.fontStyle = FontStyles.Bold;

            CreateText("BetLabel", p, "BET", 20, Muted, new Vector2(-127, 180), new Vector2(200, 28)).characterSpacing = 6;
            RectTransform betBox = CreateBox("BetBox", p, boxSprite, new Vector2(-127, 128), new Vector2(246, 84));
            TMP_Text betText = CreateText("Value", betBox, "10", 36, Cream, new Vector2(0, -2), new Vector2(200, 46));
            betText.fontStyle = FontStyles.Bold;

            CreateText("WinLabel", p, "LAST WIN", 20, Muted, new Vector2(127, 180), new Vector2(200, 28)).characterSpacing = 6;
            RectTransform winBox = CreateBox("WinBox", p, boxSprite, new Vector2(127, 128), new Vector2(246, 84));
            TMP_Text winText = CreateText("Value", winBox, "0", 36, Cream, new Vector2(0, -2), new Vector2(200, 46));
            winText.fontStyle = FontStyles.Bold;

            var betButtons = new Button[settings.BetOptions.Count];
            var betLabels = new TMP_Text[settings.BetOptions.Count];
            float betSpacing = 168f;
            float betStart = -betSpacing * (betButtons.Length - 1) / 2f;
            for (int i = 0; i < betButtons.Length; i++)
            {
                betButtons[i] = CreateBoxButton($"Bet{settings.BetOptions[i]}Button", p, boxSprite,
                    new Vector2(betStart + i * betSpacing, 26), new Vector2(164, 74), $"BET {settings.BetOptions[i]}", 22, out betLabels[i]);
            }

            Button spinButton = CreateBoxButton("SpinButton", p, boxSprite, new Vector2(0, -82), new Vector2(500, 112), "SPIN", 54, out TMP_Text spinLabel);
            spinLabel.color = Gold;
            spinLabel.fontStyle = FontStyles.Bold;
            spinLabel.characterSpacing = 12;

            TMP_Text statusText = CreateText("StatusText", p, "READY", 40, Cream, new Vector2(0, -176), new Vector2(540, 50));
            statusText.fontStyle = FontStyles.Bold;
            TMP_Text detailText = CreateText("DetailText", p, "", 19, Muted, new Vector2(0, -214), new Vector2(540, 30));

            CreatePaytable(p, database, new Vector2(0, -310));

            Button resetButton = CreateBoxButton("ResetButton", p, boxSprite, new Vector2(-127, -428), new Vector2(232, 70), "RESET", 22, out _);
            Button soundButton = CreateBoxButton("SoundButton", p, boxSprite, new Vector2(127, -428), new Vector2(232, 70), "SOUND: ON", 22, out TMP_Text soundLabel);

            // ---------------- reset confirmation popup (popup + YES/NO + close)
            Image blocker = CreateImage("ResetPopup", root, null);
            blocker.color = new Color(0, 0, 0, 0.65f);
            Stretch(blocker.rectTransform);
            Image popup = CreateImage("PopupPanel", blocker.rectTransform, LoadSprite("popup.png"));
            popup.preserveAspect = true;
            SetRect(popup.rectTransform, Vector2.zero, new Vector2(960, 484));
            TMP_Text popupMessage = CreateText("Message", popup.rectTransform, "RESET CREDITS?", 46, Cream, new Vector2(0, 70), new Vector2(720, 170));
            popupMessage.fontStyle = FontStyles.Bold;
            Button yesButton = CreateSheetButton("YesButton", popup.rectTransform, "Yes_No_Btn.png", "Yes_State", new Vector2(-180, -110), new Vector2(300, 112));
            Button noButton = CreateSheetButton("NoButton", popup.rectTransform, "Yes_No_Btn.png", "No_State", new Vector2(180, -110), new Vector2(300, 112));
            Button closeButton = CreateSheetButton("CloseButton", popup.rectTransform, "slot_machine_buttons-02.png", "Close_State", new Vector2(420, 172), new Vector2(84, 84));
            blocker.gameObject.SetActive(false);

            // ---------------- services
            var audioGo = new GameObject("Audio", typeof(AudioSource), typeof(AudioManager));
            SetFields(audioGo.GetComponent<AudioManager>(), ("source", audioGo.GetComponent<AudioSource>()));

            var uiGo = new GameObject("UIManager", typeof(UIManager));
            var ui = uiGo.GetComponent<UIManager>();
            SetFields(ui,
                ("balanceText", balanceText), ("betText", betText), ("winText", winText),
                ("statusText", statusText), ("detailText", detailText),
                ("spinButton", spinButton), ("leverButton", leverButton), ("resetButton", resetButton),
                ("soundLabel", soundLabel), ("resetPopup", blocker.gameObject), ("resetPopupMessage", popupMessage));
            SetArray(ui, "betButtons", betButtons);
            SetArray(ui, "betButtonLabels", betLabels);
            SetArray(ui, "paylineMarkers", markers.ToArray());

            var controllerGo = new GameObject("SlotMachineController", typeof(SlotMachineController), typeof(SlotButtonBindings));
            var controller = controllerGo.GetComponent<SlotMachineController>();
            SetFields(controller, ("symbolDatabase", database), ("settings", settings), ("ui", ui),
                ("audioManager", audioGo.GetComponent<AudioManager>()), ("lever", lever));
            SetArray(controller, "reels", reels);

            var bindings = controllerGo.GetComponent<SlotButtonBindings>();
            SetFields(bindings, ("controller", controller), ("spinButton", spinButton), ("leverButton", leverButton),
                ("resetButton", resetButton), ("resetConfirmButton", yesButton), ("resetCancelButton", noButton),
                ("resetCloseButton", closeButton), ("soundButton", soundButton));
            SetArray(bindings, "betButtons", betButtons);

            EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"[SlotSetup] Scene saved to {ScenePath} and added to Build Profiles scene list.");
        }

        private static ReelController CreateReel(int index, RectTransform parent, SymbolDatabase database)
        {
            IReadOnlyList<SymbolData> symbols = database.Symbols;
            Vector2 center = ArtToLocal(ReelCenterX[index], WindowCenterY);
            RectTransform viewport = CreateRect($"Reel{index + 1}", parent, center, new Vector2(WindowWidth, WindowHeight));
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform strip = CreateRect("Strip", viewport, Vector2.zero, new Vector2(WindowWidth, CellHeight * ReelController.CellCount));

            var cells = new Image[ReelController.CellCount];
            for (int j = 0; j < cells.Length; j++)
            {
                // Cell 0 is hidden above, 1-3 visible (top/center/bottom), 4 hidden below.
                // Editor preview only; the controller replaces these with RNG symbols at start-up.
                cells[j] = CreateImage($"Cell{j}", strip, symbols[(index * 2 + j) % symbols.Count].Sprite);
                cells[j].preserveAspect = true;
                cells[j].raycastTarget = false;
                SetRect(cells[j].rectTransform, new Vector2(0, (2 - j) * CellHeight), new Vector2(SymbolSize, SymbolSize));
            }

            var reel = viewport.gameObject.AddComponent<ReelController>();
            SetFields(reel, ("viewport", viewport), ("strip", strip));
            SetFloat(reel, "cellHeight", CellHeight);
            SetArray(reel, "cellImages", cells);
            return reel;
        }

        private static PaylineMarker CreatePaylineMarker(string objectName, RectTransform parent, string sheet, string prefix, Vector2 position)
        {
            Sprite[] states = LoadSlices(sheet, prefix);
            Image image = CreateImage(objectName, parent, states[0]);
            image.preserveAspect = true;
            image.raycastTarget = false;
            SetRect(image.rectTransform, position, new Vector2(34, 34));
            var marker = image.gameObject.AddComponent<PaylineMarker>();
            // States in the sheet: 1 normal, 2 highlighted, 3 pressed (dark), 4 disabled (grey).
            SetFields(marker, ("image", image), ("idleSprite", states[0]), ("spinningSprite", states[2]), ("winSprite", states[1]));
            return marker;
        }

        private static void CreatePaytable(RectTransform parent, SymbolDatabase database, Vector2 position)
        {
            RectTransform table = CreateRect("Paytable", parent, position, new Vector2(520, 150));
            CreateText("Header", table, "PAYTABLE - 3 MATCHING ON CENTER LINE", 17, Muted, new Vector2(0, 58), new Vector2(520, 26)).characterSpacing = 2;

            IReadOnlyList<SymbolData> symbols = database.Symbols;
            for (int i = 0; i < symbols.Count; i++)
            {
                SymbolData symbol = symbols[i];
                float x = i % 2 == 0 ? -128f : 128f;
                float y = 14f - (i / 2) * 54f;
                RectTransform row = CreateRect($"Pay_{symbol.Id}", table, new Vector2(x, y), new Vector2(240, 50));
                Image icon = CreateImage("Icon", row, symbol.Sprite);
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                SetRect(icon.rectTransform, new Vector2(-82, 0), new Vector2(46, 46));
                TMP_Text label = CreateText("Label", row, $"{symbol.DisplayName.ToUpperInvariant()}  x{symbol.PayoutMultiplier}", 21, Cream, new Vector2(30, 0), new Vector2(170, 46));
                label.alignment = TextAlignmentOptions.MidlineLeft;
            }
        }

        // ------------------------------------------------------------------ UI helpers

        private static Vector2 ArtToLocal(float x, float yFromTop) => new Vector2(x - MachineSize.x / 2f, MachineSize.y / 2f - yFromTop);

        private static RectTransform CreateRect(string objectName, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            SetRect(rect, position, size);
            return rect;
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static Image CreateImage(string objectName, Transform parent, Sprite sprite)
        {
            RectTransform rect = CreateRect(objectName, parent, Vector2.zero, new Vector2(100, 100));
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            return image;
        }

        private static TMP_Text CreateText(string objectName, Transform parent, string text, float size, Color color, Vector2 position, Vector2 box)
        {
            RectTransform rect = CreateRect(objectName, parent, position, box);
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static RectTransform CreateBox(string objectName, Transform parent, Sprite sprite, Vector2 position, Vector2 size)
        {
            Image image = CreateImage(objectName, parent, sprite);
            image.raycastTarget = false;
            SetRect(image.rectTransform, position, size);
            return image.rectTransform;
        }

        private static Button CreateBoxButton(string objectName, Transform parent, Sprite sprite, Vector2 position, Vector2 size, string label, float fontSize, out TMP_Text labelText)
        {
            Image image = CreateImage(objectName, parent, sprite);
            SetRect(image.rectTransform, position, size);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.92f, 0.7f);
            colors.pressedColor = new Color(0.75f, 0.65f, 0.55f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.45f, 0.45f, 0.5f, 0.85f);
            button.colors = colors;
            SetNoNavigation(button);
            labelText = CreateText("Label", image.rectTransform, label, fontSize, Cream, new Vector2(0, 1), new Vector2(size.x * 0.8f, size.y * 0.6f));
            return button;
        }

        private static Button CreateSheetButton(string objectName, Transform parent, string sheet, string prefix, Vector2 position, Vector2 size)
        {
            Sprite[] states = LoadSlices(sheet, prefix);
            Image image = CreateImage(objectName, parent, states[0]);
            image.preserveAspect = true;
            SetRect(image.rectTransform, position, size);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                highlightedSprite = states[1],
                pressedSprite = states[Mathf.Min(2, states.Length - 1)],
                selectedSprite = states[0],
                disabledSprite = states[states.Length - 1],
            };
            SetNoNavigation(button);
            return button;
        }

        private static void SetNoNavigation(Selectable selectable)
        {
            selectable.navigation = new Navigation { mode = Navigation.Mode.None };
        }

        // ------------------------------------------------------------------ asset helpers

        private static Sprite LoadSprite(string fileName)
        {
            string path = $"{ArtImportSetup.ArtFolder}/{fileName}";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                throw new FileNotFoundException($"Sprite not found at {path}. Run 'Configure Art Imports' first.");
            return sprite;
        }

        private static Sprite[] LoadSlices(string fileName, string prefix)
        {
            string path = $"{ArtImportSetup.ArtFolder}/{fileName}";
            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                .Where(s => s.name.StartsWith(prefix)).OrderBy(s => s.name).ToArray();
            if (sprites.Length < 2)
                throw new FileNotFoundException($"Expected sliced sprites '{prefix}_NN' in {path}. Run 'Configure Art Imports' first.");
            return sprites;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static void SetFields(Object target, params (string field, Object value)[] fields)
        {
            var so = new SerializedObject(target);
            foreach ((string field, Object value) in fields)
            {
                SerializedProperty prop = so.FindProperty(field);
                if (prop == null)
                    throw new MissingFieldException($"{target.GetType().Name}.{field}");
                prop.objectReferenceValue = value;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(field);
            if (prop == null || !prop.isArray)
                throw new MissingFieldException($"{target.GetType().Name}.{field} (array)");
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private sealed class MissingFieldException : System.Exception
        {
            public MissingFieldException(string field) : base($"[SlotSetup] Serialized field not found: {field}") { }
        }
    }
}
