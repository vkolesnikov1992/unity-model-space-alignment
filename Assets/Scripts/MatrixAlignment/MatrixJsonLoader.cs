using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace ModelSpaceAlignment
{
    internal static class MatrixJsonLoader
    {
        internal static MatrixJson[] LoadResourceArray(string resourceName)
        {
            var asset = Resources.Load<TextAsset>(resourceName);
            if (asset == null)
            {
                throw new InvalidOperationException($"Resource '{resourceName}' was not found.");
            }

            return LoadArray(asset.text, resourceName);
        }

        private static MatrixJson[] LoadArray(string json, string sourceName)
        {
            var wrappedJson = "{\"items\":" + json + "}";
            var wrapper = JsonUtility.FromJson<MatrixJsonArray>(wrappedJson);
            if (wrapper == null || wrapper.items == null)
            {
                throw new InvalidOperationException($"Resource '{sourceName}' does not contain a matrix array.");
            }

            return wrapper.items;
        }

        internal static BlobAssetReference<MatrixAlignmentBlob> CreateBlob(MatrixJson[] matrices)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<MatrixAlignmentBlob>();
            var blobMatrices = builder.Allocate(ref root.Matrices, matrices.Length);

            for (var i = 0; i < matrices.Length; i++)
            {
                blobMatrices[i] = Convert(matrices[i], i);
            }

            var blob = builder.CreateBlobAssetReference<MatrixAlignmentBlob>(Allocator.Persistent);
            builder.Dispose();
            return blob;
        }

        private static CompactMatrix Convert(MatrixJson matrix, int sourceIndex)
        {
            var floatMatrix = ToFloat4X4(matrix);

            return new CompactMatrix
            {
                Matrix = floatMatrix,
                PositionKey = CreatePositionKey(floatMatrix),
                SourceIndex = sourceIndex
            };
        }

        internal static Vector3 GetPosition(MatrixJson matrix)
        {
            return new Vector3(matrix.m03, matrix.m13, matrix.m23);
        }

        internal static quaternion GetRotation(MatrixJson matrix)
        {
            var rotation = math.orthonormalize(new float3x3(ToFloat4X4(matrix)));
            return new quaternion(rotation);
        }

        private static float4x4 ToFloat4X4(MatrixJson matrix)
        {
            return new float4x4(
                new float4(matrix.m00, matrix.m10, matrix.m20, matrix.m30),
                new float4(matrix.m01, matrix.m11, matrix.m21, matrix.m31),
                new float4(matrix.m02, matrix.m12, matrix.m22, matrix.m32),
                new float4(matrix.m03, matrix.m13, matrix.m23, matrix.m33));
        }

        private static PositionKey CreatePositionKey(float4x4 matrix)
        {
            return new PositionKey
            {
                X = Quantize(matrix.c3.x),
                Y = Quantize(matrix.c3.y),
                Z = Quantize(matrix.c3.z)
            };
        }

        private static int Quantize(float value)
        {
            return Mathf.RoundToInt(value * MatrixAlignmentAlgorithmSettings.PositionQuantizationScale);
        }

        [Serializable]
        private sealed class MatrixJsonArray
        {
            public MatrixJson[] items;
        }
    }

    [Serializable]
    internal struct MatrixJson
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
