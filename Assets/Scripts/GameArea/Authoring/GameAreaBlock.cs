using Unity.Entities;
using UnityEngine;

namespace GameArea
{
    public class GameAreaBlock : MonoBehaviour
    {
        private class AreaBlockBaker : Baker<GameAreaBlock>
        {
            public override void Bake(GameAreaBlock authoring)
            {
                var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
                AddComponent(entity, new AreaBlockPrefabTag());
            }
        }
    }
}