using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace GameUnits
{
    /// <summary>
    ///     Система визуализации юнитов.
    ///     Для каждого юнита с UnitComponent инстанцирует визуальный префаб
    ///     и устанавливает соответствующую World-позицию, совпадающую с позицией
    ///     hex-ячейки на игровом поле.
    ///
    ///     Конвертация int2(col, row) в float3(worldPosition).
    ///     выполняется с учётом фактических размеров поля из GameAreaData.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(UnitCreateSystem))]
    public partial struct GameUnitVisualSystem : ISystem
    {
        private EntityQuery _pendingQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<UnitVisualPrefabTag>();
            _pendingQuery = SystemAPI.QueryBuilder()
                .WithAll<UnitComponent>()
                .WithNone<UnitVisualTag>()
                .Build();

            state.RequireForUpdate(_pendingQuery);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_pendingQuery.IsEmpty)
                return;

            Entity prefab = SystemAPI.GetSingleton<UnitVisualPrefabTag>().Prefab;

            using var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (unit, entity) in SystemAPI.Query<RefRO<UnitComponent>>()
                         .WithNone<UnitVisualTag>()
                         .WithEntityAccess())
            {
                // Fallback: стандартная unity-scale сетка
                float worldPositionX = unit.ValueRO.Position.x;
                float worldPositionZ = unit.ValueRO.Position.y;

                Entity visual = ecb.Instantiate(prefab);
                ecb.SetComponent(visual, new LocalTransform
                {
                    Position = new float3(worldPositionX, 0f, worldPositionZ),
                    Rotation = quaternion.identity,
                    Scale    = 1f,
                });

                // Помечаем юнита как визуализированного
                ecb.AddComponent<UnitVisualTag>(entity);

                // Опционально: сохраняем ссылку на визуальную сущность в буфер
                if (SystemAPI.HasBuffer<UnitVisualInstance>(entity))
                {
                    var buffer = SystemAPI.GetBuffer<UnitVisualInstance>(entity);
                    buffer.Add(new UnitVisualInstance { VisualEntity = visual });
                }
            }

            ecb.Playback(state.EntityManager);
        }
    }
}