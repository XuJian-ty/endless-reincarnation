using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DragonSwordQiEffect
{
    public static class DragonSwordQiProceduralMesh
    {
        public sealed class TubeMesh
        {
            private readonly int _sectionCount;
            private readonly int _radialSides;
            private readonly Vector3[] _vertices;
            private readonly Vector3[] _normals;
            private readonly Vector2[] _uv;
            private readonly Color[] _colors;
            private readonly Mesh _mesh;

            public TubeMesh(string name, int longitudinalSegments, int radialSides)
            {
                if (longitudinalSegments < 2)
                    throw new ArgumentOutOfRangeException(nameof(longitudinalSegments));
                if (radialSides < 3)
                    throw new ArgumentOutOfRangeException(nameof(radialSides));

                _sectionCount = longitudinalSegments + 1;
                _radialSides = radialSides;
                int vertexCount = _sectionCount * _radialSides;

                _vertices = new Vector3[vertexCount];
                _normals = new Vector3[vertexCount];
                _uv = new Vector2[vertexCount];
                _colors = new Color[vertexCount];

                int[] triangles = new int[longitudinalSegments * _radialSides * 6];
                int triangleIndex = 0;
                for (int section = 0; section < longitudinalSegments; section++)
                {
                    int currentRing = section * _radialSides;
                    int nextRing = (section + 1) * _radialSides;
                    for (int side = 0; side < _radialSides; side++)
                    {
                        int nextSide = (side + 1) % _radialSides;

                        triangles[triangleIndex++] = currentRing + side;
                        triangles[triangleIndex++] = nextRing + side;
                        triangles[triangleIndex++] = nextRing + nextSide;

                        triangles[triangleIndex++] = currentRing + side;
                        triangles[triangleIndex++] = nextRing + nextSide;
                        triangles[triangleIndex++] = currentRing + nextSide;
                    }
                }

                _mesh = new Mesh
                {
                    name = name,
                    indexFormat = vertexCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
                };
                _mesh.MarkDynamic();
                _mesh.vertices = _vertices;
                _mesh.normals = _normals;
                _mesh.uv = _uv;
                _mesh.colors = _colors;
                _mesh.triangles = triangles;
            }

            public Mesh Mesh => _mesh;

            public int SectionCount => _sectionCount;

            public void Update(
                Vector3[] centers,
                Vector3[] tangents,
                float[] radii,
                Color color,
                float alpha,
                float longitudinalTiling)
            {
                if (centers == null || centers.Length < _sectionCount)
                    throw new ArgumentException("Tube center buffer is too small.", nameof(centers));
                if (tangents == null || tangents.Length < _sectionCount)
                    throw new ArgumentException("Tube tangent buffer is too small.", nameof(tangents));
                if (radii == null || radii.Length < _sectionCount)
                    throw new ArgumentException("Tube radius buffer is too small.", nameof(radii));

                Color vertexColor = color;
                vertexColor.a *= Mathf.Clamp01(alpha);

                for (int section = 0; section < _sectionCount; section++)
                {
                    Vector3 tangent = tangents[section].sqrMagnitude > 0.000001f
                        ? tangents[section].normalized
                        : Vector3.forward;
                    Vector3 reference = Mathf.Abs(Vector3.Dot(tangent, Vector3.up)) > 0.92f
                        ? Vector3.right
                        : Vector3.up;
                    Vector3 sideAxis = Vector3.Cross(reference, tangent).normalized;
                    Vector3 upAxis = Vector3.Cross(tangent, sideAxis).normalized;
                    float sectionUv = (float)section / (_sectionCount - 1);

                    for (int side = 0; side < _radialSides; side++)
                    {
                        float sideUv = (float)side / _radialSides;
                        float angle = sideUv * Mathf.PI * 2f;
                        Vector3 radial = sideAxis * Mathf.Cos(angle) + upAxis * Mathf.Sin(angle);
                        int vertexIndex = section * _radialSides + side;

                        _vertices[vertexIndex] = centers[section] + radial * radii[section];
                        _normals[vertexIndex] = radial;
                        _uv[vertexIndex] = new Vector2(sectionUv * longitudinalTiling, sideUv);
                        _colors[vertexIndex] = vertexColor;
                    }
                }

                _mesh.vertices = _vertices;
                _mesh.normals = _normals;
                _mesh.uv = _uv;
                _mesh.colors = _colors;
                _mesh.RecalculateBounds();
            }

            public void Dispose()
            {
                DestroyMesh(_mesh);
            }
        }

        public static Mesh CreateDragonHeadMesh()
        {
            const int sides = 10;
            float[] zPositions = { -0.82f, -0.34f, 0.24f, 0.75f, 1.08f };
            float[] halfWidths = { 0.34f, 0.68f, 0.64f, 0.38f, 0.12f };
            float[] halfHeights = { 0.30f, 0.54f, 0.46f, 0.28f, 0.10f };

            var vertices = new List<Vector3>(zPositions.Length * sides + 24);
            var normals = new List<Vector3>(zPositions.Length * sides + 24);
            var uv = new List<Vector2>(zPositions.Length * sides + 24);
            var triangles = new List<int>((zPositions.Length - 1) * sides * 6 + 72);

            for (int ring = 0; ring < zPositions.Length; ring++)
            {
                float ringUv = (float)ring / (zPositions.Length - 1);
                for (int side = 0; side < sides; side++)
                {
                    float sideUv = (float)side / sides;
                    float angle = sideUv * Mathf.PI * 2f;
                    float sin = Mathf.Sin(angle);
                    float cos = Mathf.Cos(angle);
                    float lowerJawWeight = Mathf.Clamp01(-sin);
                    float height = halfHeights[ring] * Mathf.Lerp(1f, 0.72f, lowerJawWeight);

                    vertices.Add(new Vector3(cos * halfWidths[ring], sin * height, zPositions[ring]));
                    normals.Add(new Vector3(cos, sin, 0.18f).normalized);
                    uv.Add(new Vector2(ringUv * 2.4f, sideUv));
                }
            }

            for (int ring = 0; ring < zPositions.Length - 1; ring++)
            {
                int currentRing = ring * sides;
                int nextRing = (ring + 1) * sides;
                for (int side = 0; side < sides; side++)
                {
                    int nextSide = (side + 1) % sides;
                    triangles.Add(currentRing + side);
                    triangles.Add(nextRing + side);
                    triangles.Add(nextRing + nextSide);
                    triangles.Add(currentRing + side);
                    triangles.Add(nextRing + nextSide);
                    triangles.Add(currentRing + nextSide);
                }
            }

            AddCheekFin(vertices, normals, uv, triangles, -1f);
            AddCheekFin(vertices, normals, uv, triangles, 1f);

            return BuildMesh("DragonSwordQi_Head", vertices, normals, uv, triangles);
        }

        public static Mesh CreateHornMesh(int radialSides = 10)
        {
            radialSides = Mathf.Max(3, radialSides);
            var vertices = new List<Vector3>(radialSides * 3 + 1);
            var normals = new List<Vector3>(radialSides * 3 + 1);
            var uv = new List<Vector2>(radialSides * 3 + 1);
            var triangles = new List<int>(radialSides * 6);

            for (int side = 0; side < radialSides; side++)
            {
                float u = (float)side / radialSides;
                float angle = u * Mathf.PI * 2f;
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices.Add(radial * 0.18f);
                normals.Add(new Vector3(radial.x, 0.25f, radial.z).normalized);
                uv.Add(new Vector2(u, 0f));

                vertices.Add(new Vector3(radial.x * 0.08f, 0.68f, radial.z * 0.08f - 0.10f));
                normals.Add(new Vector3(radial.x, 0.5f, radial.z).normalized);
                uv.Add(new Vector2(u, 0.68f));
            }

            int tipIndex = vertices.Count;
            vertices.Add(new Vector3(0f, 1.05f, -0.28f));
            normals.Add(Vector3.up);
            uv.Add(new Vector2(0.5f, 1f));

            for (int side = 0; side < radialSides; side++)
            {
                int nextSide = (side + 1) % radialSides;
                int baseCurrent = side * 2;
                int upperCurrent = baseCurrent + 1;
                int baseNext = nextSide * 2;
                int upperNext = baseNext + 1;

                triangles.Add(baseCurrent);
                triangles.Add(upperCurrent);
                triangles.Add(upperNext);
                triangles.Add(baseCurrent);
                triangles.Add(upperNext);
                triangles.Add(baseNext);

                triangles.Add(upperCurrent);
                triangles.Add(tipIndex);
                triangles.Add(upperNext);
            }

            return BuildMesh("DragonSwordQi_Horn", vertices, normals, uv, triangles);
        }

        public static Mesh CreateUvSphereMesh(int latitudeSegments = 8, int longitudeSegments = 12)
        {
            latitudeSegments = Mathf.Max(3, latitudeSegments);
            longitudeSegments = Mathf.Max(4, longitudeSegments);
            int rowLength = longitudeSegments + 1;
            var vertices = new Vector3[(latitudeSegments + 1) * rowLength];
            var normals = new Vector3[vertices.Length];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[latitudeSegments * longitudeSegments * 6];

            int vertexIndex = 0;
            for (int latitude = 0; latitude <= latitudeSegments; latitude++)
            {
                float v = (float)latitude / latitudeSegments;
                float phi = v * Mathf.PI;
                float y = Mathf.Cos(phi);
                float ringRadius = Mathf.Sin(phi);

                for (int longitude = 0; longitude <= longitudeSegments; longitude++)
                {
                    float u = (float)longitude / longitudeSegments;
                    float theta = u * Mathf.PI * 2f;
                    Vector3 normal = new Vector3(
                        Mathf.Cos(theta) * ringRadius,
                        y,
                        Mathf.Sin(theta) * ringRadius);

                    vertices[vertexIndex] = normal;
                    normals[vertexIndex] = normal;
                    uv[vertexIndex] = new Vector2(u, v);
                    vertexIndex++;
                }
            }

            int triangleIndex = 0;
            for (int latitude = 0; latitude < latitudeSegments; latitude++)
            {
                for (int longitude = 0; longitude < longitudeSegments; longitude++)
                {
                    int current = latitude * rowLength + longitude;
                    int next = current + rowLength;

                    triangles[triangleIndex++] = current;
                    triangles[triangleIndex++] = next;
                    triangles[triangleIndex++] = next + 1;
                    triangles[triangleIndex++] = current;
                    triangles[triangleIndex++] = next + 1;
                    triangles[triangleIndex++] = current + 1;
                }
            }

            var mesh = new Mesh { name = "DragonSwordQi_Sphere" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh CreateCrescentMesh(int segments = 32)
        {
            segments = Mathf.Max(8, segments);
            var vertices = new Vector3[(segments + 1) * 2];
            var normals = new Vector3[vertices.Length];
            var uv = new Vector2[vertices.Length];
            var colors = new Color[vertices.Length];
            var triangles = new int[segments * 6];

            const float startAngle = -118f;
            const float endAngle = 118f;
            const float innerRadius = 0.78f;
            const float outerRadius = 2.28f;

            for (int segment = 0; segment <= segments; segment++)
            {
                float t = (float)segment / segments;
                float angle = Mathf.Lerp(startAngle, endAngle, t) * Mathf.Deg2Rad;
                float taper = Mathf.Pow(Mathf.Sin(t * Mathf.PI), 0.38f);
                float inner = Mathf.Lerp(outerRadius * 0.94f, innerRadius, taper);
                Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                int index = segment * 2;

                vertices[index] = direction * inner;
                vertices[index + 1] = direction * outerRadius;
                normals[index] = Vector3.forward;
                normals[index + 1] = Vector3.forward;
                uv[index] = new Vector2(t * 2.5f, 0f);
                uv[index + 1] = new Vector2(t * 2.5f, 1f);
                colors[index] = new Color(1f, 1f, 1f, taper * 0.4f);
                colors[index + 1] = new Color(1f, 1f, 1f, taper);
            }

            int triangleIndex = 0;
            for (int segment = 0; segment < segments; segment++)
            {
                int current = segment * 2;
                int next = current + 2;
                triangles[triangleIndex++] = current;
                triangles[triangleIndex++] = next;
                triangles[triangleIndex++] = next + 1;
                triangles[triangleIndex++] = current;
                triangles[triangleIndex++] = next + 1;
                triangles[triangleIndex++] = current + 1;
            }

            var mesh = new Mesh { name = "DragonSwordQi_Crescent" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh CreateQuadMesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f)
            };
            mesh.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Vector3 EvaluateCubicBezier(
            Vector3 start,
            Vector3 controlA,
            Vector3 controlB,
            Vector3 end,
            float t)
        {
            t = Mathf.Clamp01(t);
            float inverse = 1f - t;
            return inverse * inverse * inverse * start
                   + 3f * inverse * inverse * t * controlA
                   + 3f * inverse * t * t * controlB
                   + t * t * t * end;
        }

        public static Vector3 EvaluateCubicBezierTangent(
            Vector3 start,
            Vector3 controlA,
            Vector3 controlB,
            Vector3 end,
            float t)
        {
            t = Mathf.Clamp01(t);
            float inverse = 1f - t;
            Vector3 tangent = 3f * inverse * inverse * (controlA - start)
                              + 6f * inverse * t * (controlB - controlA)
                              + 3f * t * t * (end - controlB);
            return tangent.sqrMagnitude > 0.000001f ? tangent.normalized : Vector3.forward;
        }

        private static void AddCheekFin(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uv,
            List<int> triangles,
            float side)
        {
            int baseIndex = vertices.Count;
            vertices.Add(new Vector3(0.46f * side, 0.02f, 0.08f));
            vertices.Add(new Vector3(1.18f * side, 0.34f, -0.28f));
            vertices.Add(new Vector3(0.56f * side, -0.24f, 0.55f));
            vertices.Add(new Vector3(0.46f * side, 0.02f, 0.08f));
            vertices.Add(new Vector3(0.56f * side, -0.24f, 0.55f));
            vertices.Add(new Vector3(1.04f * side, -0.10f, 0.12f));

            Vector3 normal = new Vector3(side, 0.25f, 0.1f).normalized;
            for (int i = 0; i < 6; i++)
            {
                normals.Add(normal);
                uv.Add(new Vector2(i % 3 == 1 ? 1f : 0f, i % 3 == 2 ? 1f : 0f));
            }

            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 1);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex + 3);
            triangles.Add(baseIndex + 4);
            triangles.Add(baseIndex + 5);
        }

        private static Mesh BuildMesh(
            string name,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uv,
            List<int> triangles)
        {
            var mesh = new Mesh
            {
                name = name,
                indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void DestroyMesh(Mesh mesh)
        {
            if (mesh == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(mesh);
            else
                UnityEngine.Object.DestroyImmediate(mesh);
        }
    }
}
