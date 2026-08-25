using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace GameArea
{
    /// <summary>
    /// Клонирует префаб с <see cref="AreaBlockPrefabTag"/> для каждого <see cref="HexCell"/>
    /// на сущности с <see cref="GameAreaData"/>.
    /// Позиция клона берется из HexCell.Position.
    /// Префаб уникален — получается через GetSingletonEntity.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(GameAreaSystem))]
    public partial struct AreaBlockSpawnSystem : ISystem
    {
        private EntityQuery _pendingAreaQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameAreaData>();
            // Система не запустится пока нет singleton-префаба
            state.RequireForUpdate<AreaBlockPrefabTag>();

            _pendingAreaQuery = SystemAPI.QueryBuilder()
                .WithAll<GameAreaData, HexCell>()
                .WithNone<AreaBlocksSpawned>()
                .Build();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_pendingAreaQuery.IsEmpty)
                return;

            Entity prefab = SystemAPI.GetSingleton<AreaBlockPrefabTag>().Prefab;

            // Наследуем rotation/scale префаба
            LocalTransform prefabTransform = SystemAPI.HasComponent<LocalTransform>(prefab)
                ? SystemAPI.GetComponent<LocalTransform>(prefab)
                : LocalTransform.Identity;

            using var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in SystemAPI.Query<RefRO<GameAreaData>>()
                         .WithAll<HexCell>()
                         .WithNone<AreaBlocksSpawned>()
                         .WithEntityAccess())
            {
                DynamicBuffer<HexCell> hexes = SystemAPI.GetBuffer<HexCell>(entity);
                if (hexes.Length == 0)
                    continue;

                foreach (HexCell hex in hexes)
                {
                    Entity instance = ecb.Instantiate(prefab);
                    ecb.SetComponent(instance, new LocalTransform
                    {
                        Position = hex.Position,
                        Rotation = prefabTransform.Rotation,
                        Scale = prefabTransform.Scale * hex.Size * 1.9f,
                    });
                }

                ecb.AddComponent(entity, new AreaBlocksSpawned { SpawnedCount = hexes.Length });
            }

            ecb.Playback(state.EntityManager);
        }
    }
}
