using System;
using Unity.Entities;
using Unity.Mathematics;

namespace ModelSpaceAlignment
{
    public struct MatrixAlignmentDataset : IComponentData
    {
        public BlobAssetReference<MatrixAlignmentBlob> Model;
        public BlobAssetReference<MatrixAlignmentBlob> Space;
    }

    public struct MatrixAlignmentBlob
    {
        public BlobArray<CompactMatrix> Matrices;
    }

    public struct CompactMatrix
    {
        public float4x4 Matrix;
        public PositionKey PositionKey;
        public int SourceIndex;
    }

    public struct PositionKey : IEquatable<PositionKey>
    {
        public int X;
        public int Y;
        public int Z;

        public bool Equals(PositionKey other)
        {
            return X == other.X && Y == other.Y && Z == other.Z;
        }

        public override bool Equals(object obj)
        {
            return obj is PositionKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + X;
                hash = hash * 31 + Y;
                hash = hash * 31 + Z;
                return hash;
            }
        }
    }

    public struct SpaceLookupEntry
    {
        public int SpaceIndex;
        public float4x4 Matrix;
    }

    public struct MatrixAlignmentMatch : IBufferElementData
    {
        public int ModelIndex;
        public int SpaceIndex;
        public float4x4 Offset;
    }

    public struct MatrixAlignmentSearchStats : IComponentData
    {
        public int ModelCount;
        public int SpaceCount;
        public int MatchCount;
        public int OffsetCount;
        public double ElapsedMilliseconds;
    }

    public struct MatrixAlignmentCompletedTag : IComponentData
    {
    }

    public struct MatrixAlignmentResultTag : IComponentData
    {
    }

    public struct MatrixAlignmentVisualizedTag : IComponentData
    {
    }

    public struct MatrixAlignmentSpaceVisual : IComponentData
    {
        public int SpaceIndex;
    }
}
