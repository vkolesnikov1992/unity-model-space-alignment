using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace ModelSpaceAlignment
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct MatrixAlignmentVisualizationSystem : ISystem
    {
        private static readonly float4 SpaceColor = new float4(0.57f, 0.60f, 0.64f, 1f);
        private static readonly float4[] OffsetColors =
        {
            new float4(0.01f, 0.11f, 1f, 1f),
            new float4(0.95f, 0.18f, 0.10f, 1f),
            new float4(0.05f, 0.62f, 0.22f, 1f),
            new float4(0.98f, 0.72f, 0.04f, 1f),
            new float4(0.66f, 0.25f, 0.95f, 1f),
            new float4(0.00f, 0.65f, 0.72f, 1f),
            new float4(0.93f, 0.30f, 0.67f, 1f),
            new float4(0.98f, 0.49f, 0.06f, 1f)
        };

        private EntityQuery resultQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MatrixAlignmentResultTag>();
            state.RequireForUpdate<MatrixAlignmentSpaceVisual>();
            resultQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<MatrixAlignmentResultTag>(),
                ComponentType.ReadOnly<MatrixAlignmentMatch>(),
                ComponentType.Exclude<MatrixAlignmentVisualizedTag>());
        }

        public void OnUpdate(ref SystemState state)
        {
            if (resultQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var resultEntity = resultQuery.GetSingletonEntity();
            var matches = state.EntityManager.GetBuffer<MatrixAlignmentMatch>(resultEntity);
            var offsetGroups = new NativeList<float4x4>(math.max(1, matches.Length), Allocator.Temp);
            var matchedSpaceGroups = new NativeHashMap<int, int>(math.max(1, matches.Length), Allocator.Temp);

            for (var i = 0; i < matches.Length; i++)
            {
                var groupIndex = FindOrAddOffsetGroup(ref offsetGroups, matches[i].Offset);
                matchedSpaceGroups.TryAdd(matches[i].SpaceIndex, groupIndex);
            }

            var highlightedCount = 0;
            foreach (var (spaceVisual, baseColor) in
                SystemAPI.Query<RefRO<MatrixAlignmentSpaceVisual>, RefRW<URPMaterialPropertyBaseColor>>())
            {
                var isMatched = matchedSpaceGroups.TryGetValue(spaceVisual.ValueRO.SpaceIndex, out var groupIndex);
                baseColor.ValueRW.Value = isMatched ? OffsetColors[groupIndex % OffsetColors.Length] : SpaceColor;
                if (isMatched)
                {
                    highlightedCount++;
                }
            }

            var offsetGroupCount = offsetGroups.Length;
            offsetGroups.Dispose();
            matchedSpaceGroups.Dispose();
            state.EntityManager.AddComponent<MatrixAlignmentVisualizedTag>(resultEntity);
            if (highlightedCount == 0)
            {
                Debug.LogWarning("Matrix alignment completed, but no space cubes were highlighted because no matches were found.");
            }
            else
            {
                Debug.Log($"Highlighted {highlightedCount} matched space cubes across {offsetGroupCount} offset groups.");
            }
        }

        private static int FindOrAddOffsetGroup(ref NativeList<float4x4> groups, float4x4 offset)
        {
            for (var i = 0; i < groups.Length; i++)
            {
                if (MatricesMatch(groups[i], offset))
                {
                    return i;
                }
            }

            groups.Add(offset);
            return groups.Length - 1;
        }

        private static bool MatricesMatch(float4x4 left, float4x4 right)
        {
            var toleranceVector = new float4(MatrixAlignmentAlgorithmSettings.MatrixMatchTolerance);
            return math.all(math.abs(left.c0 - right.c0) <= toleranceVector) &&
                math.all(math.abs(left.c1 - right.c1) <= toleranceVector) &&
                math.all(math.abs(left.c2 - right.c2) <= toleranceVector) &&
                math.all(math.abs(left.c3 - right.c3) <= toleranceVector);
        }
    }
}
