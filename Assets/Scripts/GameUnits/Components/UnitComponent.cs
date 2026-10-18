using Unity.Entities;
using Unity.Mathematics;

namespace GameUnits
{
    /// <summary>
    ///     Тег сущности-контейнера, содержащей DynamicBuffer<UnitData>
    ///     с начальными данными юнитов из редактора.
    /// </summary>
    public struct UnitPlacementTag : IComponentData
    {
    }

    /// <summary>
    ///     Структура данных юнита. Хранит ID юнита и координаты гекса (int2 — col, row).
    ///     Добавляется к ECS-сущности юнита.
    /// </summary>
    public struct UnitComponent : IComponentData
    {
        /// <summary>Уникальный идентификатор юнита.</summary>
        public int UnitID;

        /// <summary>Координаты гекса (col, row) в офсет-пространстве GameArea.</summary>
        public int2 Position;
    }
}