using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace GameUnits
{
    /// <summary>
    ///     Система создания сущностей юнитов из начальных данных (UnitPlacementTag).
    ///     При запуске ищет сущности с UnitPlacementTag без UnitCreateFinished,
    ///     для каждого элемента DynamicBuffer[UnitData] создаёт сущность с UnitComponent,
    ///     затем помечает placement как обработанный для предотвращения повторного запуска.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct UnitCreateSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            var query = SystemAPI.QueryBuilder()
                .WithAll<UnitPlacementTag>()
                .WithNone<UnitCreateFinished>()
                .Build();
            state.RequireForUpdate(query);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);

            var unitDataHandle = SystemAPI.GetBufferLookup<UnitData>(false);

            foreach (var (tag, entity) in SystemAPI.Query<RefRO<UnitPlacementTag>>()
                         .WithEntityAccess()
                         .WithNone<UnitCreateFinished>())
            {
                if (!SystemAPI.HasBuffer<UnitData>(entity))
                    continue;

                var bufferRef = unitDataHandle[entity];

                if (bufferRef.Length == 0)
                    continue;

                for (var i = 0; i < bufferRef.Length; i++)
                {
                    var data = bufferRef[i];

                    var unit = ecb.CreateEntity();
                    ecb.AddComponent(unit, new UnitComponent
                    {
                        UnitID   = data.UnitID,
                        Position = data.Position
                    });
                }

                ecb.AddComponent(entity, new UnitCreateFinished());
                bufferRef.Clear();
            }

            ecb.Playback(state.EntityManager);
        }
    }

    /// <summary>
    ///     Маркер: размещение юнитов обработано (данные конвертированы в сущности).
    /// </summary>
    public struct UnitCreateFinished : IComponentData
    {
    }
}