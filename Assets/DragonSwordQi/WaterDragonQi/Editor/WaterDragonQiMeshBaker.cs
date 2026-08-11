using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DragonSwordQiEffect.WaterDragonQi.Editor
{
    internal static class WaterDragonQiMeshBaker
    {
        public static Mesh BakeModelMesh(GameObject sourcePrefab, string assetPath)
        {
            if (sourcePrefab == null)
                throw new ArgumentNullException(nameof(sourcePrefab));

            GameObject instance = PrefabUtility.InstantiatePrefab(sourcePrefab) as GameObject;
            if (instance == null)
                throw new InvalidOperationException($"Failed to instantiate source prefab: {sourcePrefab.name}");

            try
            {
                Mesh combinedMesh = CombineSourceMeshes(instance);
                if (combinedMesh == null)
                    throw new InvalidOperationException($"No mesh data found in source prefab: {sourcePrefab.name}");

                Vector3[] vertices = combinedMesh.vertices;
                Vector3[] normals = combinedMesh.normals;
                Vector2[] uv = combinedMesh.uv;

                Vector3 axis = ComputePrincipalAxis(vertices);
                if (axis.sqrMagnitude < 0.000001f)
                    axis = Vector3.right;

                if (LooksLikeTailAtPositiveAxis(vertices, axis))
                    axis = -axis;

                Quaternion alignRotation = Quaternion.FromToRotation(axis.normalized, Vector3.right);

                for (int i = 0; i < vertices.Length; i++)
                {
                    vertices[i] = alignRotation * vertices[i];
                    if (normals != null && normals.Length == vertices.Length)
                        normals[i] = (alignRotation * normals[i]).normalized;
                }

                float minX = float.PositiveInfinity;
                float maxX = float.NegativeInfinity;
                Vector2 yzSum = Vector2.zero;
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 v = vertices[i];
                    minX = Mathf.Min(minX, v.x);
                    maxX = Mathf.Max(maxX, v.x);
                    yzSum += new Vector2(v.y, v.z);
                }

                float length = Mathf.Max(0.0001f, maxX - minX);
                Vector2 yzCenter = yzSum / Mathf.Max(1, vertices.Length);
                float invLength = 1f / length;

                float maxRadial = 0.0001f;
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 v = vertices[i];
                    v.x = (v.x - minX) * invLength;
                    v.y = (v.y - yzCenter.x) * invLength;
                    v.z = (v.z - yzCenter.y) * invLength;
                    vertices[i] = v;
                    maxRadial = Mathf.Max(maxRadial, new Vector2(v.y, v.z).magnitude);
                }

                Color[] colors = new Color[vertices.Length];
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 v = vertices[i];
                    float t = Mathf.Clamp01(v.x);
                    float radial = Mathf.Clamp01(new Vector2(v.y, v.z).magnitude / maxRadial);
                    float shell = Mathf.Clamp01(1f - radial);
                    colors[i] = new Color(t, radial, shell, 1f);
                }

                combinedMesh.vertices = vertices;
                if (normals != null && normals.Length == vertices.Length)
                    combinedMesh.normals = normals;
                if (uv != null && uv.Length == vertices.Length)
                    combinedMesh.uv = uv;
                combinedMesh.colors = colors;
                combinedMesh.RecalculateBounds();

                Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
                if (existing != null)
                    AssetDatabase.DeleteAsset(assetPath);

                AssetDatabase.CreateAsset(combinedMesh, assetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

                return AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static Mesh CombineSourceMeshes(GameObject instance)
        {
            var combineInstances = new List<CombineInstance>(32);
            int estimatedVertexCount = 0;

            MeshFilter[] meshFilters = instance.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < meshFilters.Length; i++)
            {
                MeshFilter filter = meshFilters[i];
                if (filter == null || filter.sharedMesh == null)
                    continue;

                Mesh mesh = filter.sharedMesh;
                estimatedVertexCount += mesh.vertexCount;
                for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                {
                    combineInstances.Add(new CombineInstance
                    {
                        mesh = mesh,
                        subMeshIndex = subMesh,
                        transform = filter.transform.localToWorldMatrix
                    });
                }
            }

            SkinnedMeshRenderer[] skinnedMeshRenderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinnedMeshRenderers.Length; i++)
            {
                SkinnedMeshRenderer renderer = skinnedMeshRenderers[i];
                if (renderer == null || renderer.sharedMesh == null)
                    continue;

                Mesh mesh = renderer.sharedMesh;
                estimatedVertexCount += mesh.vertexCount;
                for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                {
                    combineInstances.Add(new CombineInstance
                    {
                        mesh = mesh,
                        subMeshIndex = subMesh,
                        transform = renderer.transform.localToWorldMatrix
                    });
                }
            }

            if (combineInstances.Count == 0)
                return null;

            Mesh combinedMesh = new Mesh
            {
                name = "WaterDragonQi_BakedMesh",
                indexFormat = estimatedVertexCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            combinedMesh.CombineMeshes(combineInstances.ToArray(), true, true, false);
            combinedMesh.RecalculateNormals();
            return combinedMesh;
        }

        private static Vector3 ComputePrincipalAxis(IReadOnlyList<Vector3> vertices)
        {
            if (vertices == null || vertices.Count == 0)
                return Vector3.right;

            Vector3 mean = Vector3.zero;
            for (int i = 0; i < vertices.Count; i++)
                mean += vertices[i];
            mean /= vertices.Count;

            float xx = 0f, xy = 0f, xz = 0f, yy = 0f, yz = 0f, zz = 0f;
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 d = vertices[i] - mean;
                xx += d.x * d.x;
                xy += d.x * d.y;
                xz += d.x * d.z;
                yy += d.y * d.y;
                yz += d.y * d.z;
                zz += d.z * d.z;
            }

            Vector3 vector = new Vector3(1f, 0.35f, 0.18f);
            for (int i = 0; i < 12; i++)
            {
                Vector3 next = new Vector3(
                    xx * vector.x + xy * vector.y + xz * vector.z,
                    xy * vector.x + yy * vector.y + yz * vector.z,
                    xz * vector.x + yz * vector.y + zz * vector.z);

                float magnitude = next.magnitude;
                if (magnitude < 0.000001f)
                    break;

                vector = next / magnitude;
            }

            return vector.normalized;
        }

        private static bool LooksLikeTailAtPositiveAxis(IReadOnlyList<Vector3> vertices, Vector3 axis)
        {
            if (vertices == null || vertices.Count == 0)
                return false;

            Vector3 mean = Vector3.zero;
            for (int i = 0; i < vertices.Count; i++)
                mean += vertices[i];
            mean /= vertices.Count;

            float minProjection = float.PositiveInfinity;
            float maxProjection = float.NegativeInfinity;
            for (int i = 0; i < vertices.Count; i++)
            {
                float projection = Vector3.Dot(vertices[i] - mean, axis);
                minProjection = Mathf.Min(minProjection, projection);
                maxProjection = Mathf.Max(maxProjection, projection);
            }

            float span = Mathf.Max(0.0001f, maxProjection - minProjection);
            float lowThreshold = minProjection + span * 0.12f;
            float highThreshold = maxProjection - span * 0.12f;

            float lowRadius = AverageRadius(vertices, axis, mean, lowThreshold, true);
            float highRadius = AverageRadius(vertices, axis, mean, highThreshold, false);
            return highRadius > lowRadius;
        }

        private static float AverageRadius(
            IReadOnlyList<Vector3> vertices,
            Vector3 axis,
            Vector3 mean,
            float threshold,
            bool lowerSide)
        {
            Vector3 axisNormal = axis.normalized;
            Vector3 reference = Mathf.Abs(Vector3.Dot(axisNormal, Vector3.up)) > 0.92f ? Vector3.right : Vector3.up;
            Vector3 sideAxis = Vector3.Cross(reference, axisNormal).normalized;
            Vector3 upAxis = Vector3.Cross(axisNormal, sideAxis).normalized;

            float sum = 0f;
            int count = 0;
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 relativeToMean = vertices[i] - mean;
                float projection = Vector3.Dot(relativeToMean, axisNormal);
                if (lowerSide)
                {
                    if (projection > threshold)
                        continue;
                }
                else
                {
                    if (projection < threshold)
                        continue;
                }

                Vector3 relative = relativeToMean - axisNormal * projection;
                float radius = Mathf.Sqrt(
                    Mathf.Pow(Vector3.Dot(relative, sideAxis), 2f)
                    + Mathf.Pow(Vector3.Dot(relative, upAxis), 2f));
                sum += radius;
                count++;
            }

            if (count == 0)
                return 0f;

            return sum / count;
        }
    }
}
