using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using UnityEngine;

namespace TheLongingDecrypted.UI
{
    /// <summary>
    /// Loads the mod's own pictures and stories. Files in BepInEx/plugins/TheLongingDecrypted/CustomAssets/ replace the
    /// ones embedded in the plugin.
    /// </summary>
    internal static class CustomAssetLoader
    {
        private static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Texture2D> TextureCache = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

        public static void ClearCache()
        {
            SpriteCache.Clear();
            TextureCache.Clear();
        }

        public static string ResolveAssetPath(string relativePath)
        {
            string normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
            string pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? Paths.PluginPath;
            foreach (var root in new[] { pluginDir, Path.Combine(Paths.PluginPath, "TheLongingDecrypted"), Paths.PluginPath })
            {
                string path = Path.Combine(root, "CustomAssets", normalized);
                if (File.Exists(path))
                {
                    return path;
                }
            }
            return null;
        }

        private static byte[] LoadEmbeddedAssetBytes(string relativePath)
        {
            string suffix = "CustomAssets." + relativePath.Replace('/', '.');
            var assembly = Assembly.GetExecutingAssembly();
            foreach (var name in assembly.GetManifestResourceNames())
            {
                if (!name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                using (var stream = assembly.GetManifestResourceStream(name))
                using (var memory = new MemoryStream())
                {
                    stream?.CopyTo(memory);
                    return memory.ToArray();
                }
            }
            return null;
        }

        public static Texture2D LoadTextureOrNull(string relativePath)
        {
            if (TextureCache.TryGetValue(relativePath, out var cached))
            {
                return cached;
            }

            Texture2D texture = null;
            try
            {
                string fullPath = ResolveAssetPath(relativePath);
                byte[] bytes = fullPath != null ? File.ReadAllBytes(fullPath) : LoadEmbeddedAssetBytes(relativePath);
                if (bytes != null && bytes.Length > 0)
                {
                    texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (ImageConversion.LoadImage(texture, bytes))
                    {
                        texture.name = Path.GetFileNameWithoutExtension(relativePath);
                        texture.filterMode = FilterMode.Bilinear;
                        texture.wrapMode = TextureWrapMode.Clamp;
                        // Scene changes unload assets nothing in the new scene refers to yet.
                        texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
                    }
                    else
                    {
                        texture = null;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Failed to load {relativePath}: {ex.Message}");
            }
            TextureCache[relativePath] = texture;
            return texture;
        }

        /// <summary>Returns null when neither a file nor an embedded picture exists.</summary>
        public static Sprite LoadSprite(string relativePath, Vector2 pivot)
        {
            string key = relativePath + "|" + pivot.x + "|" + pivot.y;
            if (SpriteCache.TryGetValue(key, out var cached))
            {
                return cached;
            }
            var texture = LoadTextureOrNull(relativePath);
            Sprite sprite = null;
            if (texture != null)
            {
                sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), pivot, 100f, 0, SpriteMeshType.FullRect);
                sprite.name = texture.name;
                sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            }
            SpriteCache[key] = sprite;
            return sprite;
        }
    }
}
