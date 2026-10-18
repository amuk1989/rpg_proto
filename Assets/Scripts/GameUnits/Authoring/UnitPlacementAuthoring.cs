using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace GameUnits
{
    /// <summary>
    ///     MonoBehaviour-аутноринг для начальных юнитов.
    ///     Размещается на GameObject в сцене. Содержит список начальных юнитов
    ///     с их ID и координатами гекса (col, row).
    ///     Превращается в сущность с тегоом и DynamicBuffer<UnitData> через UnitPlacementBaker.
    /// </summary>
    public partial class UnitPlacementAuthoring : MonoBehaviour
    {
        [SerializeField] private List<UnitData> _initialUnits;

        public List<UnitData> InitialUnits => _initialUnits;

        /// <summary>
        ///     Приватный вложенный Baker для преобразования данных UnitPlacementAuthoring
        ///     в DOTS-компоненты (тег UnitPlacementTag + DynamicBuffer<UnitData>).
        /// </summary>
        private class UnitPlacementBaker : Baker<UnitPlacementAuthoring>
        {
            public override void Bake(UnitPlacementAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new UnitPlacementTag());

                if (authoring._initialUnits != null)
                {
                    var buffer = AddBuffer<UnitData>(entity);
                    for (int i = 0; i < authoring._initialUnits.Count; i++)
                    {
                        buffer.Add(authoring._initialUnits[i]);
                    }
                }
            }
        }
    }
}