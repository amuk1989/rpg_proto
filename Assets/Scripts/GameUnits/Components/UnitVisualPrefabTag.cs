using Unity.Entities;

namespace GameUnits
{
    /// <summary>
    ///     Синглтон-компонент, содержащий Entity-ссылку на префаб визуализации юнита.
    ///     Заполняется через GameUnitVisualPrefabAuthoring.Bake().
    /// </summary>
    public struct UnitVisualPrefabTag : IComponentData
    {
        public Entity Prefab;
    }
}