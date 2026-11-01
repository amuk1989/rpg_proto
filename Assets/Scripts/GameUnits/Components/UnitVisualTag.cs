using Unity.Entities;

namespace GameUnits
{
    /// <summary>
    ///     Тег сущности-юнита, указывающий что визуальная инстанция уже создана.
    ///     Предотвращает многократный рендер одного юнита при перезапуске.
    /// </summary>
    public struct UnitVisualTag : IComponentData
    {
    }
}