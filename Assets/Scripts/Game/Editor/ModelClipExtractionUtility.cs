using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    internal static class ModelClipExtractionUtility
    {
        private const string ExtractedFolderName = "ExtractedClips";

        [MenuItem("Assets/提取选中 FBX 的独立动画片段", false, 2100)]
        private static void ExtractSelectedFbxClips()
        {
            List<string> sourceAssetPaths = CollectSelectedFbxAssetPaths();
            if (sourceAssetPaths.Count == 0)
            {
                Debug.LogWarning("[ModelClipExtractionUtility] 未选中可提取动画的 FBX 资源或文件夹。");
                return;
            }

            List<UnityEngine.Object> extractedClips = new List<UnityEngine.Object>();
            int totalExtractedClipCount = 0;
            int processedAssetCount = 0;

            for (int i = 0; i < sourceAssetPaths.Count; i++)
            {
                string assetPath = sourceAssetPaths[i];
                if (!TryExtractClipsFromAsset(assetPath, extractedClips, out int extractedClipCount))
                    continue;

                totalExtractedClipCount += extractedClipCount;
                processedAssetCount++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (extractedClips.Count > 0)
                Selection.objects = extractedClips.ToArray();

            if (totalExtractedClipCount > 0)
            {
                Debug.Log(
                    $"[ModelClipExtractionUtility] 已从 {processedAssetCount} 个 FBX 中提取 {totalExtractedClipCount} 个独立动画片段，输出到各自目录下的 {ExtractedFolderName} 文件夹。");
            }
            else
            {
                Debug.LogWarning("[ModelClipExtractionUtility] 选中的 FBX 中没有找到可提取的动画片段。");
            }
        }

        [MenuItem("Assets/提取选中 FBX 的独立动画片段", true)]
        private static bool ValidateExtractSelectedFbxClips()
        {
            return CollectSelectedFbxAssetPaths().Count > 0;
        }

        private static List<string> CollectSelectedFbxAssetPaths()
        {
            HashSet<string> uniqueAssetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            UnityEngine.Object[] selectedObjects = Selection.objects;
            if (selectedObjects == null || selectedObjects.Length == 0)
                return new List<string>();

            for (int i = 0; i < selectedObjects.Length; i++)
            {
                UnityEngine.Object selectedObject = selectedObjects[i];
                if (selectedObject == null)
                    continue;

                string assetPath = AssetDatabase.GetAssetPath(selectedObject);
                if (string.IsNullOrEmpty(assetPath))
                    continue;

                assetPath = NormalizeAssetPath(assetPath);
                if (AssetDatabase.IsValidFolder(assetPath))
                {
                    CollectFbxAssetPathsInFolder(assetPath, uniqueAssetPaths);
                    continue;
                }

                if (string.Equals(Path.GetExtension(assetPath), ".fbx", StringComparison.OrdinalIgnoreCase))
                    uniqueAssetPaths.Add(assetPath);
            }

            return new List<string>(uniqueAssetPaths);
        }

        private static void CollectFbxAssetPathsInFolder(string folderPath, HashSet<string> uniqueAssetPaths)
        {
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { folderPath });
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (string.IsNullOrEmpty(assetPath))
                    continue;

                if (!string.Equals(Path.GetExtension(assetPath), ".fbx", StringComparison.OrdinalIgnoreCase))
                    continue;

                uniqueAssetPaths.Add(assetPath);
            }
        }

        private static bool TryExtractClipsFromAsset(
            string assetPath,
            List<UnityEngine.Object> extractedClips,
            out int extractedClipCount)
        {
            extractedClipCount = 0;

            List<AnimationClip> sourceClips = CollectExtractableClips(assetPath);
            if (sourceClips.Count == 0)
            {
                Debug.LogWarning($"[ModelClipExtractionUtility] 未在 FBX 中找到可提取的 AnimationClip：{assetPath}");
                return false;
            }

            string extractedFolderPath = EnsureExtractedFolder(assetPath);
            for (int i = 0; i < sourceClips.Count; i++)
            {
                AnimationClip sourceClip = sourceClips[i];
                string targetAssetPath = BuildClipAssetPath(extractedFolderPath, sourceClip.name);
                AnimationClip targetClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(targetAssetPath);
                if (targetClip == null)
                {
                    targetClip = UnityEngine.Object.Instantiate(sourceClip);
                    targetClip.name = sourceClip.name;
                    targetClip.hideFlags = HideFlags.None;
                    AssetDatabase.CreateAsset(targetClip, targetAssetPath);
                }
                else
                {
                    EditorUtility.CopySerialized(sourceClip, targetClip);
                    targetClip.name = sourceClip.name;
                    targetClip.hideFlags = HideFlags.None;
                    EditorUtility.SetDirty(targetClip);
                }

                AnimationUtility.SetAnimationEvents(targetClip, AnimationUtility.GetAnimationEvents(sourceClip));
                extractedClips.Add(targetClip);
                extractedClipCount++;
            }

            return extractedClipCount > 0;
        }

        private static List<AnimationClip> CollectExtractableClips(string assetPath)
        {
            List<AnimationClip> clips = new List<AnimationClip>();
            HashSet<int> uniqueClipIds = new HashSet<int>();

            AppendExtractableClips(AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath), clips, uniqueClipIds);
            AppendExtractableClips(AssetDatabase.LoadAllAssetsAtPath(assetPath), clips, uniqueClipIds);

            return clips;
        }

        private static void AppendExtractableClips(
            UnityEngine.Object[] assets,
            List<AnimationClip> clips,
            HashSet<int> uniqueClipIds)
        {
            if (assets == null || assets.Length == 0)
                return;

            for (int i = 0; i < assets.Length; i++)
            {
                AnimationClip clip = assets[i] as AnimationClip;
                if (!IsExtractableClip(clip))
                    continue;

                int clipId = clip.GetInstanceID();
                if (!uniqueClipIds.Add(clipId))
                    continue;

                clips.Add(clip);
            }
        }

        private static bool IsExtractableClip(AnimationClip clip)
        {
            if (clip == null)
                return false;

            return !clip.name.StartsWith("__preview__", StringComparison.Ordinal);
        }

        private static string EnsureExtractedFolder(string assetPath)
        {
            string sourceFolderPath = NormalizeAssetPath(Path.GetDirectoryName(assetPath));
            string extractedFolderPath = $"{sourceFolderPath}/{ExtractedFolderName}";
            if (AssetDatabase.IsValidFolder(extractedFolderPath))
                return extractedFolderPath;

            string createdFolderGuid = AssetDatabase.CreateFolder(sourceFolderPath, ExtractedFolderName);
            return NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(createdFolderGuid));
        }

        private static string BuildClipAssetPath(string folderPath, string clipName)
        {
            string safeFileName = clipName;
            char[] invalidChars = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalidChars.Length; i++)
                safeFileName = safeFileName.Replace(invalidChars[i], '_');

            return $"{folderPath}/{safeFileName}.anim";
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace("\\", "/");
        }
    }
}
