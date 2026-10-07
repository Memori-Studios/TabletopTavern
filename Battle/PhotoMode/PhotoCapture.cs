using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using Memori.Steamworks;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace TJ
{
    /// <summary>
    /// Takes the picture: renders the whole camera stack into a texture of its own, so no UI is ever in it, then
    /// writes a PNG off the main thread and hands it to the Steam screenshot library.
    /// </summary>
    public static class PhotoCapture
    {
        public struct Result
        {
            public bool Saved;
            public string Path;
            public int Width, Height;
            // The picture was taken at screen size because the larger render could not be made.
            public bool FellBack;
        }

        private const string FolderName = "Tabletop Tavern";
        private const int MaxLongSide = 7680;
        private const int MaxLongSideSteamDeck = 3840;

        public static bool Busy { get; private set; }
        // A capture cut short by a scene change never reaches its end, so the next battle clears the flag.
        public static void ResetBusy() => Busy = false;

        public static string Folder
        {
            get
            {
                string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                return string.IsNullOrEmpty(pictures)
                    ? Path.Combine(Application.persistentDataPath, "Photos")
                    : Path.Combine(pictures, FolderName);
            }
        }

        /// <summary>The largest capture scale this machine can take at the current screen size.</summary>
        public static int MaxScale()
        {
            int longSide = Mathf.Max(Screen.width, Screen.height);
            int cap = Mathf.Min(UIScaler.IsSteamDeck ? MaxLongSideSteamDeck : MaxLongSide, SystemInfo.maxTextureSize);
            return longSide * 2 <= cap ? 2 : 1;
        }

        /// <param name="stackBaseCamera">The base camera of the stack to photograph.</param>
        /// <param name="hideForFallback">Shown again after a screen capture; the fallback path would otherwise include it.</param>
        public static IEnumerator Capture(Camera stackBaseCamera, int scale, Canvas hideForFallback, Action<Result> done)
        {
            if (Busy) yield break;
            Busy = true;
            Result result = default;
            RenderTexture target = null;
            byte[] pixels = null;
            int width = Screen.width, height = Screen.height;

            yield return new WaitForEndOfFrame();
            scale = Mathf.Clamp(scale, 1, MaxScale());
            bool guideWasVisible = PhotoFocusGuide.Visible;
            PhotoFocusGuide.Visible = false;
            try
            {
                width = Screen.width * scale;
                height = Screen.height * scale;
                target = new RenderTexture(width, height, 32, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                var request = new RenderPipeline.StandardRequest { destination = target };
                if (target.Create() && RenderPipeline.SupportsRenderRequest(stackBaseCamera, request))
                    RenderPipeline.SubmitRenderRequest(stackBaseCamera, request);
                else
                    ReleaseTarget(ref target);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PhotoCapture] Render request failed, using the screen instead: {e.Message}");
                ReleaseTarget(ref target);
            }

            if (target != null)
            {
                PhotoFocusGuide.Visible = guideWasVisible;
                AsyncGPUReadbackRequest readback = AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32);
                while (!readback.done) yield return null;
                if (!readback.hasError) pixels = readback.GetData<byte>().ToArray();
                ReleaseTarget(ref target);
            }

            if (pixels == null)
            {
                // The screen capture includes overlay UI, so the photo canvas steps out for one frame.
                bool canvasWasOn = hideForFallback != null && hideForFallback.enabled;
                if (canvasWasOn) hideForFallback.enabled = false;
                yield return new WaitForEndOfFrame();
                Texture2D screen = ScreenCapture.CaptureScreenshotAsTexture();
                PhotoFocusGuide.Visible = guideWasVisible;
                if (canvasWasOn && hideForFallback != null) hideForFallback.enabled = true;
                width = screen.width;
                height = screen.height;
                Texture2D readable = screen;
                if (screen.format != TextureFormat.RGBA32)
                {
                    readable = new Texture2D(width, height, TextureFormat.RGBA32, false);
                    readable.SetPixels32(screen.GetPixels32());
                    UnityEngine.Object.Destroy(screen);
                }
                pixels = readable.GetRawTextureData<byte>().ToArray();
                UnityEngine.Object.Destroy(readable);
                result.FellBack = scale > 1;
            }

            string folder = Folder;
            string path = Path.Combine(folder, $"TabletopTavern_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png");
            int w = width, h = height;
            Task<bool> write = Task.Run(() => WritePng(pixels, w, h, folder, path));
            while (!write.IsCompleted) yield return null;

            result.Saved = write.Status == TaskStatus.RanToCompletion && write.Result;
            result.Path = path;
            result.Width = width;
            result.Height = height;
            if (result.Saved) SteamStatic.AddScreenshotToLibrary(path, width, height);
            Busy = false;
            done?.Invoke(result);
        }

        // Scene alpha would make the PNG look washed out in a viewer, so every pixel is made opaque.
        private static bool WritePng(byte[] rgba, int width, int height, string folder, string path)
        {
            try
            {
                for (int i = 3; i < rgba.Length; i += 4) rgba[i] = 255;
                byte[] png = ImageConversion.EncodeArrayToPNG(rgba, GraphicsFormat.R8G8B8A8_SRGB, (uint)width, (uint)height);
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(path, png);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PhotoCapture] Could not save the photo: {e.Message}");
                return false;
            }
        }

        private static void ReleaseTarget(ref RenderTexture target)
        {
            if (target == null) return;
            target.Release();
            UnityEngine.Object.Destroy(target);
            target = null;
        }
    }
}
