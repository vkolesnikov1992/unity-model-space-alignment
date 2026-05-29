using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace ModelSpaceAlignment
{
    public sealed class MatrixAlignmentBootstrap : MonoBehaviour
    {
        [SerializeField]
        private string modelResourceName = "model";

        [SerializeField]
        private string spaceResourceName = "space";

        [SerializeField]
        private float cubeScale = 0.25f;

        [SerializeField]
        private bool autoFrameCamera = true;

        private MatrixJson[] spaceMatrices;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (FindFirstObjectByType<MatrixAlignmentBootstrap>() != null)
            {
                return;
            }

            var bootstrap = new GameObject("Matrix Alignment Bootstrap");
            bootstrap.AddComponent<MatrixAlignmentBootstrap>();
        }

        private void Start()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                Debug.LogError("Default ECS world was not created.");
                return;
            }

            var entityManager = world.EntityManager;
            using (var existingDataset = entityManager.CreateEntityQuery(typeof(MatrixAlignmentDataset)))
            {
                if (!existingDataset.IsEmptyIgnoreFilter)
                {
                    return;
                }
            }

            var modelMatrices = MatrixJsonLoader.LoadResourceArray(modelResourceName);
            spaceMatrices = MatrixJsonLoader.LoadResourceArray(spaceResourceName);
            Debug.Log($"Loaded matrix resources. Model: {modelMatrices.Length}, Space: {spaceMatrices.Length}");

            var modelBlob = MatrixJsonLoader.CreateBlob(modelMatrices);
            var spaceBlob = MatrixJsonLoader.CreateBlob(spaceMatrices);

            var datasetEntity = entityManager.CreateEntity(typeof(MatrixAlignmentDataset));
            entityManager.SetComponentData(datasetEntity, new MatrixAlignmentDataset
            {
                Model = modelBlob,
                Space = spaceBlob
            });

            SpawnSpaceCubes(entityManager);

            if (autoFrameCamera)
            {
                FrameCamera();
            }
        }

        private void SpawnSpaceCubes(EntityManager entityManager)
        {
            var cubeMesh = CreateCubeMesh();
            var spaceColor = new Color(0.78f, 0.8f, 0.82f, 1f);
            var spaceMaterial = CreateMaterial(spaceColor);
            var renderMeshArray = new RenderMeshArray(
                new[] { spaceMaterial },
                new[] { cubeMesh });
            var renderDescription = new RenderMeshDescription(
                ShadowCastingMode.On,
                receiveShadows: true,
                motionVectorGenerationMode: MotionVectorGenerationMode.Camera,
                layer: gameObject.layer);

            for (var i = 0; i < spaceMatrices.Length; i++)
            {
                var entity = entityManager.CreateEntity(typeof(LocalTransform), typeof(MatrixAlignmentSpaceVisual));
                var position = MatrixJsonLoader.GetPosition(spaceMatrices[i]);
                entityManager.SetComponentData(
                    entity,
                    LocalTransform.FromPositionRotationScale(
                        new float3(position.x, position.y, position.z),
                        MatrixJsonLoader.GetRotation(spaceMatrices[i]),
                        cubeScale));
                entityManager.SetComponentData(entity, new MatrixAlignmentSpaceVisual { SpaceIndex = i });

                RenderMeshUtility.AddComponents(
                    entity,
                    entityManager,
                    renderDescription,
                    renderMeshArray,
                    MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
                entityManager.AddComponentData(entity, new URPMaterialPropertyBaseColor { Value = ToLinearFloat4(spaceColor) });
            }

            Debug.Log($"Spawned {spaceMatrices.Length} ECS space cubes for matrix alignment visualization.");
        }

        private static Mesh CreateCubeMesh()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.hideFlags = HideFlags.HideAndDontSave;
            var mesh = cube.GetComponent<MeshFilter>().sharedMesh;
            Destroy(cube);
            return mesh;
        }

        private static Material CreateMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            var material = new Material(shader)
            {
                color = color,
                enableInstancing = true
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            return material;
        }

        private void FrameCamera()
        {
            if (spaceMatrices == null || spaceMatrices.Length == 0)
            {
                return;
            }

            var bounds = CalculateRawBounds();

            var camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }

            var maxExtent = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z, 1f);
            var distance = maxExtent * 3f;
            camera.transform.position = bounds.center + new Vector3(0f, maxExtent * 0.75f, -distance);
            camera.transform.LookAt(bounds.center);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = distance + maxExtent * 4f;

            if (FindFirstObjectByType<Light>() == null)
            {
                var lightObject = new GameObject("Directional Light");
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
        }

        private Bounds CalculateRawBounds()
        {
            if (spaceMatrices == null || spaceMatrices.Length == 0)
            {
                return new Bounds(Vector3.zero, Vector3.zero);
            }

            var bounds = new Bounds(MatrixJsonLoader.GetPosition(spaceMatrices[0]), Vector3.zero);
            for (var i = 1; i < spaceMatrices.Length; i++)
            {
                bounds.Encapsulate(MatrixJsonLoader.GetPosition(spaceMatrices[i]));
            }

            return bounds;
        }

        private static float4 ToLinearFloat4(Color color)
        {
            var linear = color.linear;
            return new float4(linear.r, linear.g, linear.b, linear.a);
        }
    }
}
