using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using UnityEngine;

namespace TheLongingDecrypted.UI
{
    /// <summary>
    /// Loads custom PNG sprites and text files from BepInEx/plugins/TheLongingDecrypted/CustomAssets/
    /// (with procedural pixel-art fallbacks if an external PNG is not present).
    /// </summary>
    internal static class CustomAssetLoader
    {
        private static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Texture2D> TextureCache = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

        public static string PrimaryAssetsDir
        {
            get
            {
                string dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? Paths.PluginPath;
                return Path.Combine(dllDir, "CustomAssets");
            }
        }

        public static void ClearCache()
        {
            SpriteCache.Clear();
            TextureCache.Clear();
        }

        public static string ResolveAssetPath(string relativePath)
        {
            string normalized = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            string p1 = Path.Combine(PrimaryAssetsDir, normalized);
            if (File.Exists(p1))
            {
                return p1;
            }

            string p2 = Path.Combine(Paths.PluginPath, "TheLongingDecrypted", "CustomAssets", normalized);
            if (File.Exists(p2))
            {
                return p2;
            }

            string p3 = Path.Combine(Paths.PluginPath, "CustomAssets", normalized);
            if (File.Exists(p3))
            {
                return p3;
            }

            if (string.Equals(relativePath, "hats/hat_miffy.png", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveAssetPath("hats/hat_glasses.png");
            }

            return null;
        }

        private static byte[] LoadEmbeddedAssetBytes(string relativePath)
        {
            string resSuffix = "CustomAssets." + relativePath.Replace('/', '.').Replace('\\', '.');
            var asm = Assembly.GetExecutingAssembly();
            foreach (var name in asm.GetManifestResourceNames())
            {
                if (name.EndsWith(resSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    using (var stream = asm.GetManifestResourceStream(name))
                    {
                        if (stream != null)
                        {
                            byte[] buf = new byte[stream.Length];
                            stream.Read(buf, 0, buf.Length);
                            return buf;
                        }
                    }
                }
            }
            return null;
        }

        public static Texture2D LoadTextureOrNull(string relativePath)
        {
            if (TextureCache.TryGetValue(relativePath, out var cached) && cached != null)
            {
                return cached;
            }

            try
            {
                string fullPath = ResolveAssetPath(relativePath);
                byte[] bytes = !string.IsNullOrEmpty(fullPath)
                    ? File.ReadAllBytes(fullPath)
                    : LoadEmbeddedAssetBytes(relativePath);

                if (bytes != null && bytes.Length > 0)
                {
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (ImageConversion.LoadImage(tex, bytes))
                    {
                        tex.name = Path.GetFileNameWithoutExtension(relativePath);
                        tex.filterMode = FilterMode.Bilinear;
                        tex.wrapMode = TextureWrapMode.Clamp;
                        TextureCache[relativePath] = tex;
                        return tex;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Failed to load custom texture {relativePath}: {ex.Message}");
            }

            return null;
        }

        public static Sprite LoadSprite(
            string relativePath,
            int fallbackWidth,
            int fallbackHeight,
            Vector2 pivot,
            float pixelsPerUnit,
            Action<Texture2D> proceduralFallback)
        {
            if (SpriteCache.TryGetValue(relativePath, out var cached) && cached != null)
            {
                return cached;
            }

            Texture2D tex = LoadTextureOrNull(relativePath);
            if (tex == null)
            {
                tex = new Texture2D(fallbackWidth, fallbackHeight, TextureFormat.RGBA32, false)
                {
                    name = "Fallback_" + Path.GetFileNameWithoutExtension(relativePath),
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                ClearTexture(tex, new Color(0f, 0f, 0f, 0f));
                proceduralFallback?.Invoke(tex);
                tex.Apply();
                TextureCache[relativePath] = tex;
            }

            var sprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                pivot,
                pixelsPerUnit
            );
            sprite.name = tex.name;
            SpriteCache[relativePath] = sprite;
            return sprite;
        }

        public static void ClearTexture(Texture2D tex, Color color)
        {
            var pixels = new Color[tex.width * tex.height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
            tex.SetPixels(pixels);
        }

        public static void FillRect(Texture2D tex, int x, int y, int w, int h, Color col)
        {
            int x0 = Mathf.Clamp(x, 0, tex.width);
            int y0 = Mathf.Clamp(y, 0, tex.height);
            int x1 = Mathf.Clamp(x + w, 0, tex.width);
            int y1 = Mathf.Clamp(y + h, 0, tex.height);
            for (int py = y0; py < y1; py++)
            {
                for (int px = x0; px < x1; px++)
                {
                    tex.SetPixel(px, py, col);
                }
            }
        }

        public static void FillEllipse(Texture2D tex, int cx, int cy, int rx, int ry, Color col)
        {
            if (rx <= 0 || ry <= 0)
            {
                return;
            }
            int x0 = Mathf.Clamp(cx - rx, 0, tex.width - 1);
            int x1 = Mathf.Clamp(cx + rx, 0, tex.width - 1);
            int y0 = Mathf.Clamp(cy - ry, 0, tex.height - 1);
            int y1 = Mathf.Clamp(cy + ry, 0, tex.height - 1);
            float rx2 = rx * rx;
            float ry2 = ry * ry;
            for (int py = y0; py <= y1; py++)
            {
                float dy = py - cy;
                for (int px = x0; px <= x1; px++)
                {
                    float dx = px - cx;
                    if ((dx * dx) / rx2 + (dy * dy) / ry2 <= 1f)
                    {
                        tex.SetPixel(px, py, col);
                    }
                }
            }
        }
    }
}
