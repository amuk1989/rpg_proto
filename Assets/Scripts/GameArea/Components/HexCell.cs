using Unity.Entities;
using Unity.Mathematics;

namespace GameArea
{
    [InternalBufferCapacity(8)]
    public struct HexCell : IBufferElementData
    {
        public int2 Coord;
        public float3 Position;
        public float Size;
    }

    public struct HexCellComponent : IComponentData
    {
        public int2 Coord;
        public float3 Position;
        public float Size;
    }
}
