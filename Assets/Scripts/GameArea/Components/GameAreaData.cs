using Unity.Entities;
using Unity.Mathematics;

namespace GameArea
{
    public struct GameAreaData : IComponentData
    {
        public float2 Size;
        public int HexCount;
    }
}
