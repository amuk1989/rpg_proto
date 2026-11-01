using Unity.Entities;
using UnityEngine;

namespace GameUnits
{
    /// <summary>
    ///     MonoBehaviour-аутноринг для префаба визуализации юнита.
    ///     Размещается на GameObject в сцене.
    ///     Baker конвертирует GameObject-префаб в Entity-ссылку и сохраняет
    ///     в синглтон-компоненте UnitVisualPrefabTag.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameUnitVisualPrefabAuthoring : MonoBehaviour
    {
        [SerializeField] private GameObject _prefab;

        private class VisualPrefabBaker : Baker<GameUnitVisualPrefabAuthoring>
        {
            public override void Bake(GameUnitVisualPrefabAuthoring authoring)
            {
                Entity prefabEntity = GetEntity(authoring._prefab, TransformUsageFlags.Dynamic);

                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new UnitVisualPrefabTag
                {
                    Prefab = prefabEntity
                });
            }
        }
    }
}