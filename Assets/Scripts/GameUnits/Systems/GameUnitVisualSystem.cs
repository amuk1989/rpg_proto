using GameArea;
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
    ///     Позиция юнита берётся напрямую из буфера HexCell сущности поля,
    ///     поэтому всегда совпадает с сеткой, построенной GameAreaSystem
    ///     (размер гексов и центрирование учитываются автоматически).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(GameAreaSystem))]
    [UpdateAfter(typeof(UnitCreateSystem))]
    public partial struct GameUnitVisualSystem : ISystem
    {
        private EntityQuery _pendingQuery;
        private EntityQuery _areaQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<UnitVisualPrefabTag>();
            _pendingQuery = SystemAPI.QueryBuilder()
                .WithAll<UnitComponent>()
                .WithNone<UnitVisualTag>()
                .Build();

            _areaQuery = SystemAPI.QueryBuilder()
                .WithAll<GameAreaData>()
                .WithAll<HexCell>()
                .Build();

            state.RequireForUpdate(_pendingQuery);
            state.RequireForUpdate(_areaQuery);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            // Поле есть, но ещё не построено (некорректные параметры) —
            // ждём, юнитов визуализируем и помечаем только по готовой сетке
            var cells = SystemAPI.GetBuffer<HexCell>(_areaQuery.GetSingletonEntity());
            if (cells.IsEmpty)
                return;

            Entity prefab = SystemAPI.GetSingleton<UnitVisualPrefabTag>().Prefab;

            using var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (unit, entity) in SystemAPI.Query<RefRO<UnitComponent>>()
                         .WithNone<UnitVisualTag>()
                         .WithEntityAccess())
            {
                float3 worldPosition = ResolveCellPosition(in cells, unit.ValueRO.Position);

                Entity visual = ecb.Instantiate(prefab);
                ecb.SetComponent(visual, new LocalTransform
                {
                    Position = worldPosition,
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

        /// <summary>
        ///     Ищет гекс с координатой coord в буфере поля и возвращает его позицию.
        ///     Если ячейка не найдена (юнит за пределами поля) — возвращает
        ///     центр поля: сетка строится с центром в origin (0, 0, 0).
        /// </summary>
        private static float3 ResolveCellPosition(in DynamicBuffer<HexCell> cells, int2 coord)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i].Coord.Equals(coord))
                    return cells[i].Position;
            }

            return float3.zero;
        }
    }
}