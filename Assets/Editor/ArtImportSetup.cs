using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace SlotMachine.EditorTools
{
    /// <summary>
    /// Configures import settings for the supplied artwork and slices the button sprite sheets.
    /// Slice rectangles are measured from the image's alpha channel (not guessed): opaque bands
    /// separated by transparent gaps become individual sprites.
    /// </summary>
    public static class ArtImportSetup
    {
        public const string ArtFolder = "Assets/Art/Provided/Slot Machine";

        private const byte AlphaThreshold = 8;
        private const int MergeGap = 3;
        private const int Padding = 2;

        // Single sprites. Borders (L, B, R, T) enable 9-slicing for resizable panels.
        private static readonly Dictionary<string, Vector4> SingleSprites = new Dictionary<string, Vector4>
        {
            { "bg_gradient.png", Vector4.zero },
            { "slot-machine1.png", Vector4.zero },
            { "slot-machine2.png", Vector4.zero },
            { "slot-machine3.png", Vector4.zero },
            { "slot-machine4.png", Vector4.zero },
            { "slot-machine5.png", Vector4.zero },
            { "slot-symbol1.png", Vector4.zero },
            { "slot-symbol2.png", Vector4.zero },
            { "slot-symbol3.png", Vector4.zero },
            { "slot-symbol4.png", Vector4.zero },
            { "slot_machine_Middle_box.png", Vector4.zero },
            { "popup.png", new Vector4(150, 150, 150, 130) },
        };

        /// <summary>Vertical sheets with four states each, named top to bottom.</summary>
        private static readonly Dictionary<string, string> VerticalSheets = new Dictionary<string, string>
        {
            { "slot_machine_buttons-02.png", "Close_State" },
            { "slot_machine_buttons-03.png", "ArrowRight_State" },
            { "slot_machine_buttons-04.png", "ArrowLeft_State" },
        };

        private const string YesNoSheet = "Yes_No_Btn.png";

        [MenuItem("Tools/Slot Machine/1. Configure Art Imports", priority = 1)]
        public static void ConfigureAll()
        {
            foreach (KeyValuePair<string, Vector4> entry in SingleSprites)
                ConfigureSingle(entry.Key, entry.Value);

            foreach (KeyValuePair<string, string> entry in VerticalSheets)
                SliceVerticalSheet(entry.Key, entry.Value, 4);

            SliceYesNoSheet();
            AssetDatabase.SaveAssets();
            Debug.Log("[SlotSetup] Art import settings configured and sprite sheets sliced.");
        }

        private static TextureImporter GetImporter(string fileName)
        {
            string path = $"{ArtFolder}/{fileName}";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new FileNotFoundException($"Supplied asset not found or not a texture: {path}");
            return importer;
        }

        private static void ApplyCommon(TextureImporter importer, int maxSourceSize)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.spritePixelsPerUnit = 100;
            // Uncompressed keeps the small 96 px symbols and thin cabinet lines crisp.
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = Mathf.Max(2048, Mathf.NextPowerOfTwo(maxSourceSize));
        }

        private static void ConfigureSingle(string fileName, Vector4 border)
        {
            TextureImporter importer = GetImporter(fileName);
            Texture2D source = LoadSourcePixels(importer.assetPath);
            ApplyCommon(importer, Mathf.Max(source.width, source.height));
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = border;
            Object.DestroyImmediate(source);
            importer.SaveAndReimport();
        }

        private static void SliceVerticalSheet(string fileName, string prefix, int expectedCount)
        {
            TextureImporter importer = GetImporter(fileName);
            Texture2D tex = LoadSourcePixels(importer.assetPath);
            Color32[] pixels = tex.GetPixels32();

            List<Vector2Int> rows = FindBands(pixels, tex.width, tex.height, true, 0, tex.width - 1, 0, tex.height - 1);
            rows.Sort((a, b) => b.x.CompareTo(a.x)); // top of image first

            var rects = new List<SpriteRect>();
            for (int i = 0; i < rows.Count; i++)
            {
                List<Vector2Int> cols = FindBands(pixels, tex.width, tex.height, false, 0, tex.width - 1, rows[i].x, rows[i].y);
                int xMin = cols.Min(c => c.x), xMax = cols.Max(c => c.y);
                rects.Add(MakeRect($"{prefix}_{i + 1:00}", xMin, rows[i].x, xMax, rows[i].y, tex.width, tex.height));
            }

            if (rects.Count != expectedCount)
                Debug.LogError($"[SlotSetup] {fileName}: expected {expectedCount} button states but detected {rects.Count}. Check the slices in the Sprite Editor.");

            ApplySlices(importer, tex, rects);
        }

        private static void SliceYesNoSheet()
        {
            TextureImporter importer = GetImporter(YesNoSheet);
            Texture2D tex = LoadSourcePixels(importer.assetPath);
            Color32[] pixels = tex.GetPixels32();

            List<Vector2Int> rows = FindBands(pixels, tex.width, tex.height, true, 0, tex.width - 1, 0, tex.height - 1);
            rows.Sort((a, b) => b.x.CompareTo(a.x));

            var rects = new List<SpriteRect>();
            for (int r = 0; r < rows.Count; r++)
            {
                List<Vector2Int> cols = FindBands(pixels, tex.width, tex.height, false, 0, tex.width - 1, rows[r].x, rows[r].y);
                cols.Sort((a, b) => a.x.CompareTo(b.x));
                for (int c = 0; c < cols.Count; c++)
                {
                    string label = c == 0 ? "Yes" : c == 1 ? "No" : $"Extra{c}";
                    rects.Add(MakeRect($"{label}_State_{r + 1:00}", cols[c].x, rows[r].x, cols[c].y, rows[r].y, tex.width, tex.height));
                }
            }

            if (rects.Count != 6)
                Debug.LogError($"[SlotSetup] {YesNoSheet}: expected 6 button states but detected {rects.Count}.");

            ApplySlices(importer, tex, rects);
        }

        /// <summary>
        /// Finds runs of rows (horizontal=true) or columns containing any pixel above the alpha threshold,
        /// restricted to the given box. Runs separated by tiny gaps are merged. Returns (start, end) inclusive.
        /// </summary>
        private static List<Vector2Int> FindBands(Color32[] px, int width, int height, bool horizontal, int x0, int x1, int y0, int y1)
        {
            var bands = new List<Vector2Int>();
            int outerStart = horizontal ? y0 : x0;
            int outerEnd = horizontal ? y1 : x1;
            int runStart = -1;
            int lastHit = -100;

            for (int o = outerStart; o <= outerEnd; o++)
            {
                bool hit = false;
                int innerStart = horizontal ? x0 : y0;
                int innerEnd = horizontal ? x1 : y1;
                for (int i = innerStart; i <= innerEnd && !hit; i++)
                {
                    int x = horizontal ? i : o;
                    int y = horizontal ? o : i;
                    hit = px[y * width + x].a > AlphaThreshold;
                }

                if (!hit) continue;
                if (runStart >= 0 && o - lastHit > MergeGap + 1)
                {
                    bands.Add(new Vector2Int(runStart, lastHit));
                    runStart = -1;
                }

                if (runStart < 0) runStart = o;
                lastHit = o;
            }

            if (runStart >= 0)
                bands.Add(new Vector2Int(runStart, lastHit));
            return bands;
        }

        private static SpriteRect MakeRect(string spriteName, int xMin, int yMin, int xMax, int yMax, int texWidth, int texHeight)
        {
            xMin = Mathf.Max(0, xMin - Padding);
            yMin = Mathf.Max(0, yMin - Padding);
            xMax = Mathf.Min(texWidth - 1, xMax + Padding);
            yMax = Mathf.Min(texHeight - 1, yMax + Padding);
            return new SpriteRect
            {
                name = spriteName,
                spriteID = GUID.Generate(),
                rect = new Rect(xMin, yMin, xMax - xMin + 1, yMax - yMin + 1),
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
            };
        }

        private static void ApplySlices(TextureImporter importer, Texture2D source, List<SpriteRect> rects)
        {
            ApplyCommon(importer, Mathf.Max(source.width, source.height));
            importer.spriteImportMode = SpriteImportMode.Multiple;
            Object.DestroyImmediate(source);
            importer.SaveAndReimport();

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            // Re-use existing IDs for sprites with the same name so references survive re-running setup.
            var existing = provider.GetSpriteRects().ToDictionary(r => r.name, r => r.spriteID);
            foreach (SpriteRect rect in rects)
            {
                if (existing.TryGetValue(rect.name, out GUID id))
                    rect.spriteID = id;
            }

            provider.SetSpriteRects(rects.ToArray());
            var nameIds = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            nameIds?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();

            string summary = string.Join(", ", rects.Select(r => $"{r.name} {r.rect.x},{r.rect.y} {r.rect.width}x{r.rect.height}"));
            Debug.Log($"[SlotSetup] Sliced {Path.GetFileName(importer.assetPath)}: {summary}");
        }

        /// <summary>Reads the original PNG bytes, independent of the importer's read/write setting.</summary>
        private static Texture2D LoadSourcePixels(string assetPath)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(assetPath));
            return tex;
        }
    }
}
