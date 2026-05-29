using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace ModelSpaceAlignment
{
    [BurstCompile]
    public struct BuildSpaceLookupJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<CompactMatrix> Space;

        public NativeParallelMultiHashMap<PositionKey, SpaceLookupEntry>.ParallelWriter SpaceByPositionKey;

        public void Execute(int index)
        {
            var spaceMatrix = Space[index];
            SpaceByPositionKey.Add(spaceMatrix.PositionKey, new SpaceLookupEntry
            {
                SpaceIndex = spaceMatrix.SourceIndex,
                Matrix = spaceMatrix.Matrix
            });
        }
    }

    [BurstCompile]
    public struct FindValidModelOffsetsJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<CompactMatrix> Model;

        [ReadOnly]
        public NativeArray<CompactMatrix> Space;

        [ReadOnly]
        public NativeParallelMultiHashMap<PositionKey, SpaceLookupEntry> SpaceByPositionKey;

        public NativeList<MatrixAlignmentMatch>.ParallelWriter OutputMatches;

        public void Execute(int spaceIndex)
        {
            var modelAnchor = Model[0];
            var spaceAnchor = Space[spaceIndex];
            var offset = math.mul(spaceAnchor.Matrix, math.inverse(modelAnchor.Matrix));
            for (var i = 0; i < Model.Length; i++)
            {
                var modelMatrix = Model[i];
                var transformed = math.mul(offset, modelMatrix.Matrix);

                if (!TryFindMatchingSpaceMatrix(transformed))
                {
                    return;
                }
            }

            for (var i = 0; i < Model.Length; i++)
            {
                var modelMatrix = Model[i];
                var transformed = math.mul(offset, modelMatrix.Matrix);
                AddFirstMatchingSpaceMatrix(modelMatrix.SourceIndex, transformed, offset);
            }
        }

        private bool TryFindMatchingSpaceMatrix(float4x4 transformed)
        {
            var centerKey = CreatePositionKey(transformed);
            var searchRadius = GetPositionSearchRadius();
            for (var x = -searchRadius; x <= searchRadius; x++)
            {
                for (var y = -searchRadius; y <= searchRadius; y++)
                {
                    for (var z = -searchRadius; z <= searchRadius; z++)
                    {
                        var key = new PositionKey
                        {
                            X = centerKey.X + x,
                            Y = centerKey.Y + y,
                            Z = centerKey.Z + z
                        };

                        SpaceLookupEntry spaceEntry;
                        NativeParallelMultiHashMapIterator<PositionKey> iterator;
                        if (!SpaceByPositionKey.TryGetFirstValue(key, out spaceEntry, out iterator))
                        {
                            continue;
                        }

                        do
                        {
                            if (MatricesMatch(transformed, spaceEntry.Matrix))
                            {
                                return true;
                            }
                        }
                        while (SpaceByPositionKey.TryGetNextValue(out spaceEntry, ref iterator));
                    }
                }
            }

            return false;
        }

        private void AddFirstMatchingSpaceMatrix(int modelIndex, float4x4 transformed, float4x4 offset)
        {
            var centerKey = CreatePositionKey(transformed);
            var searchRadius = GetPositionSearchRadius();
            for (var x = -searchRadius; x <= searchRadius; x++)
            {
                for (var y = -searchRadius; y <= searchRadius; y++)
                {
                    for (var z = -searchRadius; z <= searchRadius; z++)
                    {
                        var key = new PositionKey
                        {
                            X = centerKey.X + x,
                            Y = centerKey.Y + y,
                            Z = centerKey.Z + z
                        };

                        SpaceLookupEntry spaceEntry;
                        NativeParallelMultiHashMapIterator<PositionKey> iterator;
                        if (!SpaceByPositionKey.TryGetFirstValue(key, out spaceEntry, out iterator))
                        {
                            continue;
                        }

                        do
                        {
                            if (!MatricesMatch(transformed, spaceEntry.Matrix))
                            {
                                continue;
                            }

                            OutputMatches.AddNoResize(new MatrixAlignmentMatch
                            {
                                ModelIndex = modelIndex,
                                SpaceIndex = spaceEntry.SpaceIndex,
                                Offset = offset
                            });
                            return;
                        }
                        while (SpaceByPositionKey.TryGetNextValue(out spaceEntry, ref iterator));
                    }
                }
            }
        }

        private static int GetPositionSearchRadius()
        {
            return math.max(
                1,
                (int)math.ceil(
                    MatrixAlignmentAlgorithmSettings.MatrixMatchTolerance *
                    MatrixAlignmentAlgorithmSettings.PositionQuantizationScale) + 1);
        }

        private static bool MatricesMatch(float4x4 left, float4x4 right)
        {
            var toleranceVector = new float4(MatrixAlignmentAlgorithmSettings.MatrixMatchTolerance);
            return math.all(math.abs(left.c0 - right.c0) <= toleranceVector) &&
                math.all(math.abs(left.c1 - right.c1) <= toleranceVector) &&
                math.all(math.abs(left.c2 - right.c2) <= toleranceVector) &&
                math.all(math.abs(left.c3 - right.c3) <= toleranceVector);
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
            return (int)math.round(value * MatrixAlignmentAlgorithmSettings.PositionQuantizationScale);
        }
    }
}
