using Unity.Entities;
using UnityEngine;

namespace GameArea
{
    public class GameAreaBlockAuthoring : MonoBehaviour
    {
        [SerializeField] private GameAreaBlock _prefab;

        // In baking, this Baker will run once for every SpawnerAuthoring instance in a subscene.
        // (Note that nesting an authoring component's Baker class inside the authoring MonoBehaviour class
        // is simply an optional matter of style.)
        class Baker : Baker<GameAreaBlockAuthoring>
        {
            public override void Bake(GameAreaBlockAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new AreaBlockPrefabTag()
                {
                    Prefab = GetEntity(authoring._prefab, TransformUsageFlags.Dynamic)
                });
            }
        }
    }
}