using Unity.Entities;

namespace ModelSpaceAlignment
{
    public partial struct MatrixAlignmentBlobDisposalSystem : ISystem
    {
        public void OnDestroy(ref SystemState state)
        {
            foreach (var dataset in SystemAPI.Query<RefRO<MatrixAlignmentDataset>>())
            {
                var value = dataset.ValueRO;
                if (value.Model.IsCreated)
                {
                    value.Model.Dispose();
                }

                if (value.Space.IsCreated)
                {
                    value.Space.Dispose();
                }
            }
        }
    }
}
