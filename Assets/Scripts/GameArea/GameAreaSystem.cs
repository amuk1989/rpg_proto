using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace GameArea
{
    public partial struct GameAreaSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameAreaData>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in SystemAPI.Query<RefRO<GameAreaData>>()
                         .WithNone<HexCell>()
                         .WithEntityAccess())
            {
                ecb.AddBuffer<HexCell>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            foreach (var (area, hexes) in SystemAPI.Query<RefRO<GameAreaData>, DynamicBuffer<HexCell>>())
            {
                Rebuild(in area.ValueRO, hexes);
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        private static void Rebuild(in GameAreaData area, DynamicBuffer<HexCell> hexes)
        {
            hexes.Clear();

            int hexCount = area.HexCount;
            float2 rectSize = area.Size;
            if (hexCount <= 0 || rectSize.x <= 0f || rectSize.y <= 0f)
            {
                return;
            }

            ComputeGrid(hexCount, rectSize, out int columns, out int rows, out float hexSize);

            float horiz = hexSize * math.sqrt(3f);
            float vert = hexSize * 1.5f;
            float oddRowOffset = rows > 1 ? 0.5f : 0f;
            float originX = -horiz * ((columns - 1) + oddRowOffset) * 0.5f;
            float originZ = -vert * (rows - 1) * 0.5f;

            hexes.Capacity = hexCount;

            int index = 0;
            for (int row = 0; row < rows && index < hexCount; row++)
            {
                float rowOffset = (row & 1) * 0.5f;
                for (int col = 0; col < columns && index < hexCount; col++)
                {
                    hexes.Add(new HexCell
                    {
                        Coord = new int2(col, row),
                        Position = new float3(originX + horiz * (col + rowOffset), 0f, originZ + vert * row),
                        Size = hexSize
                    });
                    index++;
                }
            }
        }

        private static void ComputeGrid(int hexCount, float2 rectSize, out int columns, out int rows, out float hexSize)
        {
            float aspect = rectSize.x / rectSize.y;
            float colToRow = aspect * 1.5f / math.sqrt(3f);
            columns = math.max(1, (int)math.round(math.sqrt(hexCount * colToRow)));
            rows = math.max(1, (int)math.ceil(hexCount / (float)columns));

            float widthUnits = math.sqrt(3f) * (columns + (rows > 1 ? 0.5f : 0f));
            float heightUnits = 2f + 1.5f * (rows - 1);
            hexSize = math.min(rectSize.x / widthUnits, rectSize.y / heightUnits);
        }
    }
}
