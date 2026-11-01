using Unity.Entities;

namespace GameUnits
{
    /// <summary>
    ///     Запись в DynamicBuffer на сущности юнита для связи UnitComponent
    ///     с его визуальной ECS-сущностью.
    /// </summary>
    public struct UnitVisualInstance : IBufferElementData
    {
        /// <summary>Entity визуального клона (результат ecb.Instantiate).</summary>
        public Entity VisualEntity;
    }
}