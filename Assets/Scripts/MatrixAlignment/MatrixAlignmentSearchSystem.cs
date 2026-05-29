using System.Diagnostics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace ModelSpaceAlignment
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct MatrixAlignmentSearchSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MatrixAlignmentDataset>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var entityManager = state.EntityManager;
            var datasetEntity = SystemAPI.GetSingletonEntity<MatrixAlignmentDataset>();
            if (entityManager.HasComponent<MatrixAlignmentCompletedTag>(datasetEntity))
            {
                return;
            }

            var dataset = entityManager.GetComponentData<MatrixAlignmentDataset>(datasetEntity);
            ref var modelBlob = ref dataset.Model.Value;
            ref var spaceBlob = ref dataset.Space.Value;
            var modelCount = modelBlob.Matrices.Length;
            var spaceCount = spaceBlob.Matrices.Length;

            if (modelCount == 0 || spaceCount == 0)
            {
                CreateEmptyResult(ref state, datasetEntity, modelCount, spaceCount);
                return;
            }

            var model = new NativeArray<CompactMatrix>(modelCount, Allocator.TempJob);
            var space = new NativeArray<CompactMatrix>(spaceCount, Allocator.TempJob);

            for (var i = 0; i < modelCount; i++)
            {
                model[i] = modelBlob.Matrices[i];
            }

            for (var i = 0; i < spaceCount; i++)
            {
                space[i] = spaceBlob.Matrices[i];
            }

            var stopwatch = Stopwatch.StartNew();
            var maxMatches = modelCount * spaceCount;
            var matches = new NativeList<MatrixAlignmentMatch>(maxMatches, Allocator.TempJob);

            var spaceByPositionKey = new NativeParallelMultiHashMap<PositionKey, SpaceLookupEntry>(spaceCount, Allocator.TempJob);
            var buildHandle = new BuildSpaceLookupJob
            {
                Space = space,
                SpaceByPositionKey = spaceByPositionKey.AsParallelWriter()
            }.Schedule(spaceCount, 128, state.Dependency);

            var matchHandle = new FindValidModelOffsetsJob
            {
                Model = model,
                Space = space,
                SpaceByPositionKey = spaceByPositionKey,
                OutputMatches = matches.AsParallelWriter()
            }.Schedule(spaceCount, 64, buildHandle);

            matchHandle.Complete();

            stopwatch.Stop();
            state.Dependency = default;

            var resultEntity = entityManager.CreateEntity(typeof(MatrixAlignmentResultTag), typeof(MatrixAlignmentSearchStats));
            var resultBuffer = entityManager.AddBuffer<MatrixAlignmentMatch>(resultEntity);
            resultBuffer.ResizeUninitialized(matches.Length);
            for (var i = 0; i < matches.Length; i++)
            {
                resultBuffer[i] = matches[i];
            }

            MatrixAlignmentResultExporter.Export(
                resultBuffer,
                stopwatch.Elapsed.TotalMilliseconds,
                out var offsetCount);
            entityManager.SetComponentData(resultEntity, new MatrixAlignmentSearchStats
            {
                ModelCount = modelCount,
                SpaceCount = spaceCount,
                MatchCount = matches.Length,
                OffsetCount = offsetCount,
                ElapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds
            });

            entityManager.AddComponent<MatrixAlignmentCompletedTag>(datasetEntity);
            UnityEngine.Debug.Log(
                $"Matrix alignment completed. Model: {modelCount}, Space: {spaceCount}, Unique offsets: {offsetCount}, Match pairs: {matches.Length}, Tolerance: {MatrixAlignmentAlgorithmSettings.MatrixMatchTolerance}, Time: {stopwatch.Elapsed.TotalMilliseconds:F2} ms.");

            matches.Dispose();
            spaceByPositionKey.Dispose();
            model.Dispose();
            space.Dispose();
        }

        private static void CreateEmptyResult(
            ref SystemState state,
            Entity datasetEntity,
            int modelCount,
            int spaceCount)
        {
            var entityManager = state.EntityManager;
            var resultEntity = entityManager.CreateEntity(typeof(MatrixAlignmentResultTag), typeof(MatrixAlignmentSearchStats));
            var resultBuffer = entityManager.AddBuffer<MatrixAlignmentMatch>(resultEntity);
            MatrixAlignmentResultExporter.Export(resultBuffer, 0d, out var offsetCount);
            entityManager.SetComponentData(resultEntity, new MatrixAlignmentSearchStats
            {
                ModelCount = modelCount,
                SpaceCount = spaceCount,
                MatchCount = 0,
                OffsetCount = offsetCount,
                ElapsedMilliseconds = 0d
            });
            entityManager.AddComponent<MatrixAlignmentCompletedTag>(datasetEntity);
            UnityEngine.Debug.Log($"Matrix alignment completed. Model: {modelCount}, Space: {spaceCount}, Unique offsets: {offsetCount}, Match pairs: 0, Tolerance: {MatrixAlignmentAlgorithmSettings.MatrixMatchTolerance}, Time: 0.00 ms.");
        }
    }
}
