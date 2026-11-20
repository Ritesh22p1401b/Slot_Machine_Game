using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SlotMachine.EditorTools
{
    /// <summary>
    /// Renders MainGame.unity to PNG files at several resolutions (layout check / README screenshots).
    /// The overlay canvas is temporarily switched to camera space for the capture and the scene is
    /// not saved, so nothing in the project changes.
    /// </summary>
    public static class SlotScreenshot
    {
        private const string OutputFolder = "Documentation/Screenshots";

        [MenuItem("Tools/Slot Machine/Capture Layout Screenshots", priority = 40)]
        public static void CaptureAll()
        {
            Capture(1920, 1080);
            Capture(1280, 960);
            Capture(1366, 768);
        }

        private static void Capture(int width, int height)
        {
            EditorSceneManager.OpenScene(SlotSceneBuilder.ScenePath, OpenSceneMode.Single);
            Camera camera = Camera.main;
            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 5f;

            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = rt;
            camera.aspect = (float)width / height;
            Canvas.ForceUpdateCanvases();
            camera.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            camera.targetTexture = null;

            Directory.CreateDirectory(OutputFolder);
            string path = Path.Combine(OutputFolder, $"layout_{width}x{height}.png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);

            // Discard the temporary changes.
            EditorSceneManager.OpenScene(SlotSceneBuilder.ScenePath, OpenSceneMode.Single);
            Debug.Log($"[SlotSetup] Screenshot written to {path}");
        }
    }
}
