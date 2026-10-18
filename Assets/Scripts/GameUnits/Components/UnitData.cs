using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace GameUnits
{
    /// <summary>
    ///     Данные одного юнита для DynamicBuffer в UnitPlacementTag-сущности.
    ///     Сериализуется в редакторе через UnitPlacementAuthoring.
    ///     В рантайме хранится в DynamicBuffer как NativeArray.
    /// </summary>
    [System.Serializable]
    public struct UnitData : IBufferElementData
    {
        /// <summary>ID юнита (например, PlayerUnit = 1, EnemyUnit = 2).</summary>
        [Tooltip("Уникальный идентификатор юнита.")]
        public int UnitID;

        /// <summary>Координаты гекса (col, row) в офсет-пространстве GameArea.</summary>
        public int2 Position;
    }
}