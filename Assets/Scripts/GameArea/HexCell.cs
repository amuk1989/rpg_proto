using Unity.Entities;
using Unity.Mathematics;

namespace GameArea
{
    [InternalBufferCapacity(0)]
    public struct HexCell : IBufferElementData
    {
        public int2 Coord;
        public float3 Position;
        public float Size;
    }
}
