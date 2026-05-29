using System;
using System.Collections.Generic;
using System.IO;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace ModelSpaceAlignment
{
    internal static class MatrixAlignmentResultExporter
    {
        private const string ResultFileName = "alignment-results.json";

        internal static string Export(
            DynamicBuffer<MatrixAlignmentMatch> matches,
            double elapsedMilliseconds,
            out int offsetCount)
        {
            var groups = new List<OffsetGroupBuilder>();
            for (var i = 0; i < matches.Length; i++)
            {
                var match = matches[i];
                var group = FindOffsetGroup(groups, match.Offset);
                if (group == null)
                {
                    group = new OffsetGroupBuilder
                    {
                        OffsetKey = CreateOffsetKey(match.Offset),
                        Representative = match,
                        MatchedSpaceIndices = new List<int>(),
                        MatchedSpaceLookup = new HashSet<int>(),
                        DetailedMatches = new List<DetailedMatch>(),
                        DetailedMatchLookup = new HashSet<string>()
                    };
                    groups.Add(group);
                }

                if (group.MatchedSpaceLookup.Add(match.SpaceIndex))
                {
                    group.MatchedSpaceIndices.Add(match.SpaceIndex);
                }

                var detailedKey = $"{match.ModelIndex}:{match.SpaceIndex}";
                if (group.DetailedMatchLookup.Add(detailedKey))
                {
                    group.DetailedMatches.Add(new DetailedMatch
                    {
                        modelIndex = match.ModelIndex,
                        spaceIndex = match.SpaceIndex
                    });
                }
            }

            groups.Sort((left, right) => string.CompareOrdinal(left.OffsetKey, right.OffsetKey));

            var export = new MatrixAlignmentExport
            {
                elapsedMilliseconds = elapsedMilliseconds,
                matchTolerance = MatrixAlignmentAlgorithmSettings.MatrixMatchTolerance,
                offsetCount = groups.Count,
                offsets = new List<OffsetResult>(groups.Count)
            };
            offsetCount = groups.Count;

            foreach (var group in groups)
            {
                group.MatchedSpaceIndices.Sort();
                group.DetailedMatches.Sort((left, right) =>
                {
                    var modelComparison = left.modelIndex.CompareTo(right.modelIndex);
                    return modelComparison != 0
                        ? modelComparison
                        : left.spaceIndex.CompareTo(right.spaceIndex);
                });

                export.offsets.Add(new OffsetResult
                {
                    translation = ToOffset(group.Representative.Offset),
                    matrix = ToMatrix(group.Representative.Offset),
                    matchedSpaceIndices = group.MatchedSpaceIndices,
                    detailedMatches = group.DetailedMatches
                });
            }

            var outputPath = Path.Combine(Application.dataPath, "..", ResultFileName);
            outputPath = Path.GetFullPath(outputPath);
            File.WriteAllText(outputPath, JsonUtility.ToJson(export, true));
            Debug.Log($"Matrix alignment results exported to: {outputPath}. Unique offsets: {offsetCount}, match pairs: {matches.Length}.");
            return outputPath;
        }

        private static OffsetGroupBuilder FindOffsetGroup(List<OffsetGroupBuilder> groups, float4x4 offset)
        {
            for (var i = 0; i < groups.Count; i++)
            {
                if (MatricesMatch(groups[i].Representative.Offset, offset))
                {
                    return groups[i];
                }
            }

            return null;
        }

        private static bool MatricesMatch(float4x4 left, float4x4 right)
        {
            var tolerance = MatrixAlignmentAlgorithmSettings.MatrixMatchTolerance;
            return Mathf.Abs(left.c0.x - right.c0.x) <= tolerance &&
                Mathf.Abs(left.c0.y - right.c0.y) <= tolerance &&
                Mathf.Abs(left.c0.z - right.c0.z) <= tolerance &&
                Mathf.Abs(left.c0.w - right.c0.w) <= tolerance &&
                Mathf.Abs(left.c1.x - right.c1.x) <= tolerance &&
                Mathf.Abs(left.c1.y - right.c1.y) <= tolerance &&
                Mathf.Abs(left.c1.z - right.c1.z) <= tolerance &&
                Mathf.Abs(left.c1.w - right.c1.w) <= tolerance &&
                Mathf.Abs(left.c2.x - right.c2.x) <= tolerance &&
                Mathf.Abs(left.c2.y - right.c2.y) <= tolerance &&
                Mathf.Abs(left.c2.z - right.c2.z) <= tolerance &&
                Mathf.Abs(left.c2.w - right.c2.w) <= tolerance &&
                Mathf.Abs(left.c3.x - right.c3.x) <= tolerance &&
                Mathf.Abs(left.c3.y - right.c3.y) <= tolerance &&
                Mathf.Abs(left.c3.z - right.c3.z) <= tolerance &&
                Mathf.Abs(left.c3.w - right.c3.w) <= tolerance;
        }

        private static string CreateOffsetKey(float4x4 matrix)
        {
            var quantizationScale = MatrixAlignmentAlgorithmSettings.OffsetQuantizationScale;
            return string.Join(
                ":",
                Mathf.RoundToInt(matrix.c0.x * quantizationScale),
                Mathf.RoundToInt(matrix.c1.x * quantizationScale),
                Mathf.RoundToInt(matrix.c2.x * quantizationScale),
                Mathf.RoundToInt(matrix.c3.x * quantizationScale),
                Mathf.RoundToInt(matrix.c0.y * quantizationScale),
                Mathf.RoundToInt(matrix.c1.y * quantizationScale),
                Mathf.RoundToInt(matrix.c2.y * quantizationScale),
                Mathf.RoundToInt(matrix.c3.y * quantizationScale),
                Mathf.RoundToInt(matrix.c0.z * quantizationScale),
                Mathf.RoundToInt(matrix.c1.z * quantizationScale),
                Mathf.RoundToInt(matrix.c2.z * quantizationScale),
                Mathf.RoundToInt(matrix.c3.z * quantizationScale),
                Mathf.RoundToInt(matrix.c0.w * quantizationScale),
                Mathf.RoundToInt(matrix.c1.w * quantizationScale),
                Mathf.RoundToInt(matrix.c2.w * quantizationScale),
                Mathf.RoundToInt(matrix.c3.w * quantizationScale));
        }

        private static OffsetDto ToOffset(float4x4 offset)
        {
            return new OffsetDto
            {
                x = offset.c3.x,
                y = offset.c3.y,
                z = offset.c3.z
            };
        }

        private static MatrixDto ToMatrix(float4x4 matrix)
        {
            return new MatrixDto
            {
                m00 = matrix.c0.x,
                m10 = matrix.c0.y,
                m20 = matrix.c0.z,
                m30 = matrix.c0.w,
                m01 = matrix.c1.x,
                m11 = matrix.c1.y,
                m21 = matrix.c1.z,
                m31 = matrix.c1.w,
                m02 = matrix.c2.x,
                m12 = matrix.c2.y,
                m22 = matrix.c2.z,
                m32 = matrix.c2.w,
                m03 = matrix.c3.x,
                m13 = matrix.c3.y,
                m23 = matrix.c3.z,
                m33 = matrix.c3.w
            };
        }

        [Serializable]
        private sealed class MatrixAlignmentExport
        {
            public double elapsedMilliseconds;
            public float matchTolerance;
            public int offsetCount;
            public List<OffsetResult> offsets;
        }

        private sealed class OffsetGroupBuilder
        {
            public string OffsetKey;
            public MatrixAlignmentMatch Representative;
            public List<int> MatchedSpaceIndices;
            public HashSet<int> MatchedSpaceLookup;
            public List<DetailedMatch> DetailedMatches;
            public HashSet<string> DetailedMatchLookup;
        }

        [Serializable]
        private sealed class OffsetResult
        {
            public OffsetDto translation;
            public MatrixDto matrix;
            public List<int> matchedSpaceIndices;
            public List<DetailedMatch> detailedMatches;
        }

        [Serializable]
        private sealed class DetailedMatch
        {
            public int modelIndex;
            public int spaceIndex;
        }

        [Serializable]
        private struct OffsetDto
        {
            public float x;
            public float y;
            public float z;
        }

        [Serializable]
        private struct MatrixDto
        {
            public float m00;
            public float m10;
            public float m20;
            public float m30;
            public float m01;
            public float m11;
            public float m21;
            public float m31;
            public float m02;
            public float m12;
            public float m22;
            public float m32;
            public float m03;
            public float m13;
            public float m23;
            public float m33;
        }
    }
}
