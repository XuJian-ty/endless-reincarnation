using System;
using System.Collections.Generic;
using System.IO;
using Game.Saving;
using UnityEngine;

namespace Game.UI
{
    internal static class PlayerPortraitUtility
    {
        private const string CustomPortraitPrefix = "custom:";
        private const string CustomPortraitFolder = "CustomPortraits";
        private const string SourcePortraitPrefix = "UI图片/";
        private const string ResourcePortraitPrefix = "SocialPortraits/";

        private static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>();

        public static Sprite LoadPortraitSprite(string portraitId)
        {
            string normalizedId = NormalizePortraitId(portraitId);
            if (SpriteCache.TryGetValue(normalizedId, out Sprite cached))
                return cached;

            Sprite sprite = normalizedId.StartsWith(CustomPortraitPrefix, StringComparison.Ordinal)
                ? LoadCustomPortrait(normalizedId)
                : LoadResourcePortrait(normalizedId);

            if (sprite == null && normalizedId != SaveSystem.DefaultPortraitId)
                sprite = LoadResourcePortrait(SaveSystem.DefaultPortraitId);

            if (sprite != null)
                SpriteCache[normalizedId] = sprite;

            return sprite;
        }

        public static bool TryImportPortrait(string sourcePath, out string portraitId, out string error)
        {
            portraitId = null;
            error = null;

            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                error = "未找到选择的头像文件。";
                return false;
            }

            string ext = Path.GetExtension(sourcePath).ToLowerInvariant();
            if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
            {
                error = "头像文件只支持 png、jpg、jpeg。";
                return false;
            }

            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(sourcePath);
            }
            catch (Exception ex)
            {
                error = $"读取头像文件失败：{ex.Message}";
                return false;
            }

            Texture2D probe = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            bool valid = probe.LoadImage(bytes);
            UnityEngine.Object.Destroy(probe);
            if (!valid)
            {
                error = "无法识别选择的图片文件。";
                return false;
            }

            string directory = GetCustomPortraitDirectory();
            Directory.CreateDirectory(directory);

            string fileName = $"{Guid.NewGuid():N}{ext}";
            string destination = Path.Combine(directory, fileName);
            try
            {
                File.WriteAllBytes(destination, bytes);
            }
            catch (Exception ex)
            {
                error = $"保存头像文件失败：{ex.Message}";
                return false;
            }

            portraitId = CustomPortraitPrefix + fileName;
            SpriteCache.Remove(portraitId);
            return true;
        }

        public static bool TryLoadExternalPortraitTexture(string sourcePath, out Texture2D texture, out string error)
        {
            texture = null;
            error = null;

            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                error = "未找到选择的头像文件。";
                return false;
            }

            string ext = Path.GetExtension(sourcePath).ToLowerInvariant();
            if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
            {
                error = "头像文件只支持 png、jpg、jpeg。";
                return false;
            }

            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(sourcePath);
            }
            catch (Exception ex)
            {
                error = $"读取头像文件失败：{ex.Message}";
                return false;
            }

            Texture2D loaded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!loaded.LoadImage(bytes))
            {
                UnityEngine.Object.Destroy(loaded);
                error = "无法识别选择的图片文件。";
                return false;
            }

            loaded.name = Path.GetFileNameWithoutExtension(sourcePath);
            loaded.wrapMode = TextureWrapMode.Clamp;
            loaded.filterMode = FilterMode.Bilinear;
            texture = loaded;
            return true;
        }

        public static bool TrySaveCroppedPortrait(Texture2D sourceTexture, float zoom, Vector2 offset, int outputSize, out string portraitId, out string error)
        {
            portraitId = null;
            error = null;

            if (sourceTexture == null || sourceTexture.width <= 0 || sourceTexture.height <= 0)
            {
                error = "当前没有可保存的头像图片。";
                return false;
            }

            int size = Mathf.Max(64, outputSize);
            float safeZoom = Mathf.Max(0.01f, zoom);
            float scale = Mathf.Max(size / (float)sourceTexture.width, size / (float)sourceTexture.height) * safeZoom;
            float displayWidth = sourceTexture.width * scale;
            float displayHeight = sourceTexture.height * scale;
            float maxOffsetX = Mathf.Max(0f, (displayWidth - size) * 0.5f);
            float maxOffsetY = Mathf.Max(0f, (displayHeight - size) * 0.5f);
            Vector2 clampedOffset = new Vector2(
                Mathf.Clamp(offset.x, -maxOffsetX, maxOffsetX),
                Mathf.Clamp(offset.y, -maxOffsetY, maxOffsetY));

            Texture2D cropped = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            float halfSize = size * 0.5f;
            float widthMinusOne = Mathf.Max(1f, sourceTexture.width - 1f);
            float heightMinusOne = Mathf.Max(1f, sourceTexture.height - 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float viewX = x + 0.5f - halfSize;
                    float viewY = y + 0.5f - halfSize;
                    float sourceX = (viewX - clampedOffset.x) / scale + sourceTexture.width * 0.5f;
                    float sourceY = (viewY - clampedOffset.y) / scale + sourceTexture.height * 0.5f;
                    float u = Mathf.Clamp01(sourceX / widthMinusOne);
                    float v = Mathf.Clamp01(sourceY / heightMinusOne);
                    pixels[y * size + x] = sourceTexture.GetPixelBilinear(u, v);
                }
            }

            cropped.SetPixels(pixels);
            cropped.Apply(false, false);
            byte[] pngBytes = cropped.EncodeToPNG();
            UnityEngine.Object.Destroy(cropped);

            if (pngBytes == null || pngBytes.Length == 0)
            {
                error = "生成头像文件失败。";
                return false;
            }

            string directory = GetCustomPortraitDirectory();
            Directory.CreateDirectory(directory);

            string fileName = $"{Guid.NewGuid():N}.png";
            string destination = Path.Combine(directory, fileName);
            try
            {
                File.WriteAllBytes(destination, pngBytes);
            }
            catch (Exception ex)
            {
                error = $"保存头像文件失败：{ex.Message}";
                return false;
            }

            portraitId = CustomPortraitPrefix + fileName;
            SpriteCache.Remove(portraitId);
            return true;
        }

        private static string NormalizePortraitId(string portraitId)
        {
            return string.IsNullOrWhiteSpace(portraitId)
                ? SaveSystem.DefaultPortraitId
                : portraitId.Trim();
        }

        private static Sprite LoadResourcePortrait(string portraitId)
        {
            string resourcePath = NormalizePortraitId(portraitId).Replace(SourcePortraitPrefix, ResourcePortraitPrefix);
            Sprite sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite != null)
                return sprite;

            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
                return null;

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }

        private static Sprite LoadCustomPortrait(string portraitId)
        {
            string fileName = portraitId.Substring(CustomPortraitPrefix.Length);
            if (string.IsNullOrWhiteSpace(fileName) || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                return null;

            string path = Path.Combine(GetCustomPortraitDirectory(), fileName);
            if (!File.Exists(path))
                return null;

            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(path);
            }
            catch
            {
                return null;
            }

            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes))
            {
                UnityEngine.Object.Destroy(texture);
                return null;
            }

            texture.name = Path.GetFileNameWithoutExtension(fileName);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }

        private static string GetCustomPortraitDirectory()
        {
            return Path.Combine(Application.persistentDataPath, CustomPortraitFolder);
        }
    }
}
