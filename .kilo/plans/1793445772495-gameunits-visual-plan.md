# Техническое задание: Визуальная часть модуля GameUnits

## 1. Обзор

Разработать визуальную подсистему для модуля `GameUnits`, которая преобразует ECS-сущности юнитов (содержащие компонеты `UnitComponent`) в визуально представляемые объекты в сцене Unity.

**Зависимость:** система визуализации должна обрабатывать только сущности, содержащие `UnitComponent` (с полями `UnitID` и `Position`).

**Шаблоны:** префаб юнита прокидывается по аналогии с `GameAreaBlockAuthoring` — дерево Authoring → Baker преобразует `GameObject`-префаб в `Entity`-префаб, сохраняемый в синглтон-компоненте.

---

## 2. Архитектурный обзор

```
┌─────────────────────────────────────────────────────────┐
│  GameUnitVisualPrefabAuthoring (MonoBehaviour)          │
│    [SerializeField] private GameObject _prefab;         │
│    ↓ Baker                                               │
│    GetEntity(_prefab, TransformUsageFlags.Dynamic)       │
└──────────────────────┬──────────────────────────────────┘
                       │
                       ▼
            ┌──────────────────────┐
            │  UnitVisualPrefabTag │  ← Singleton-компонент
            │  { Entity Prefab; }  │    хранит Entity-префаб
            └──────────────────────┘
                       │
                       ▼
            ┌──────────────────────────┐
            │  GameUnitVisualSystem     │
            │  [UpdateAfter(typeof(UnitCreateSystem))]
            │
            │  Query: RefRO<UnitComponent>
            │        + UnitVisualTag    ← добавляется после визуализации
            │
            │  ecb.Instantiate(prefab)
            │  ecb.SetComponent(worldPos)
            └──────────────────────────┘
```

---

## 3. Новые компоненты

### 3.1 `UnitVisualPrefabTag` (IComponentData)

**Файл:** `Assets/Scripts/GameUnits/Components/UnitVisualPrefabTag.cs`

```csharp
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
```

### 3.2 `UnitVisualTag` (IComponentData — тег)

**Файл:** `Assets/Scripts/GameUnits/Components/UnitVisualTag.cs`

```csharp
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
```

### 3.3 `UnitVisualInstance` (IBufferElementData — опционально)

**Файл:** `Assets/Scripts/GameUnits/Components/UnitVisualInstance.cs`

```csharp
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
```

---

## 4. Authoring-класс визуального префаба

### 4.1 Файл: `Assets/Scripts/GameUnits/Authoring/GameUnitVisualPrefabAuthoring.cs`

```csharp
using Unity.Entities;
using UnityEngine;

namespace GameUnits
{
    /// <summary>
    ///     MonoBehaviour-аутноринг для префаба визуализации юнита.
    ///     Размещается на GameObject в сцене или подсцене.
    ///     Baker конвертирует GameObject-префаб в Entity-ссылку и сохраняет
    ///     в синглтон-компоненте UnitVisualPrefabTag.
    ///     Как в GameAreaBlockAuthoring, только вместо GetEntity(TransformUsageFlags.Dynamic)
    ///     — получаем уже готовую Entity-ссылку через единичный инфлейт.
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

                // Один инфлейт = один синглтон-компонент.
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new UnitVisualPrefabTag
                {
                    Prefab = prefabEntity
                });
            }
        }
    }
}
```

**Поведение при множество инфлейтов:** Если в сцене несколько `GameUnitVisualPrefabAuthoring`, каждый инфлейт перезапишет синглтон `UnitVisualPrefabTag`. Рекомендуется использовать один инфлейт или один подсцен с единственным префабом.

---

## 5. Система визуализации юнитов

### 5.1 Файл: `Assets/Scripts/GameUnits/Systems/GameUnitVisualSystem.cs`

```csharp
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace GameUnits
{
    /// <summary>
    ///     Система визуализации юнитов.
    ///     Для каждого юнита с UnitComponent но без UnitVisualTag инстанцирует
    ///     визуальный префаб и устанавливает соответствующую World-позицию.
    ///
    ///     Конвертирует int2-координаты гекса (col, row) в float3-позицию нужным
    ///     образом в зависимости от типа гексагональной сетки:
    ///     — flat-topped: x = col * hexSize * 3 / 2, z = row * hexSize * sqrt(3)
    ///     — pointy-topped: x = col * hexSize * sqrt(3), z = row * hexSize * 3 / 2
    ///
    ///     Обновляется после UnitCreateSystem.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(UnitCreateSystem))]
    public partial struct GameUnitVisualSystem : ISystem
    {
        private EntityQuery _pendingQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<UnitVisualPrefabTag>();

            _pendingQuery = SystemAPI.QueryBuilder()
                .WithAll<UnitComponent, UnitVisualPrefabTag>()
                .WithNone<UnitVisualTag>()
                .Build();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_pendingQuery.IsEmpty)
                return;

            Entity prefab = SystemAPI.GetSingleton<UnitVisualPrefabTag>().Prefab;

            using var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (unit, entity) in SystemAPI.Query<RefRO<UnitComponent>>()
                         .WithAll<UnitVisualPrefabTag>()
                         .WithNone<UnitVisualTag>()
                         .WithEntityAccess())
            {
                // hexHeight, hexWidth — значения FlatTop/PointTop гейм-дизайнера.
                // CoordToHexTileSize — глубина системы (тиль 1.9 из HexCell).
                // Координаты юнита — int2 offset-пространстве, 
                // нужно конвертировать в ресурс-позицию.
                float3 worldPosition = ConvertOffsetToHexWorldPosition(unit.ValueRO.Position);

                Entity visual = ecb.Instantiate(prefab);
                ecb.SetComponent(visual, new LocalTransform
                {
                    Position = worldPosition,
                    Rotation = quaternion.identity,
                    Scale    = 1f,
                });

                // Добавляем тег чтобы избежать повторного рендера
                ecb.AddComponent<UnitVisualTag>(entity);

                // Эта часть опциональна — подписка на визуальную сущность
                if (SystemAPI.HasBuffer<UnitVisualInstance>(entity))
                {
                    var buffer = SystemAPI.GetBuffer<UnitVisualInstance>(entity);
                    buffer.Add(new UnitVisualInstance { VisualEntity = visual });
                }
            }

            ecb.Playback(state.EntityManager);
        }

        /// <summary>
        ///     Конвертация гексагонных офсет-координат (col, row) в world-позицию float3.
        ///     Формула соответствует GameAreaSystem.ComputeGrid (flat-top, odd-rowOffset).
        /// </summary>
        private static float3 ConvertOffsetToHexWorldPosition(int2 hexOffset)
        {
            const float hexSize = 1f;
            float horiz = hexSize * math.sqrt(3f);
            float vert  = hexSize * 1.5f;
            float oddRowOffset = 0.5f; // примем по умолчанию для rows > 1
            float originX = -horiz * 0.5f; // нужно учесть columns/rows
            float originZ = 0f;

            float3 p = new float3(
                originX + horiz * (hexOffset.x + oddRowOffset),
                0f,
                originZ + vert * hexOffset.y
            );
            return p;
        }
    }
}
```

---

## 6. Структура файлов

```
Assets/Scripts/GameUnits/
├── Authoring/
│   ├── GameUnitVisualPrefabAuthoring.cs   ← новая
│   └── UnitPlacementAuthoring.cs          ← существующий (без изменений)
├── Components/
│   ├── UnitComponent.cs                   ← существующий (без изменений)
│   ├── UnitData.cs                        ← существующий (без изменений)
│   ├── UnitPlacementTag.cs                ← существующий (без изменений)
│   ├── UnitCreateFinished.cs              ← существующий (без изменений)
│   ├── UnitVisualPrefabTag.cs             ← новая
│   ├── UnitVisualTag.cs                   ← новая
│   └── UnitVisualInstance.cs              ← новая
└── Systems/
    ├── UnitCreateSystem.cs                ← существующий (без изменений)
    └── GameUnitVisualSystem.cs            ← новая
```

---

## 7. Детальные требования

### 7.1 Авторинг и бейкинг

| Элемент       | Описание                                                                     |
|---------------|------------------------------------------------------------------------------|
| `[DisallowMultipleComponent]` | Гарантирует один инфлейт на сцену.                            |
| `[SerializeField] private GameObject _prefab;` | Приватное сериализуемое поле.                    |
| Baker         | `private class VisualPrefabBaker : Baker<GameUnitVisualPrefabAuthoring>`     |
| prefab        | `GetEntity(authoring._prefab, TransformUsageFlags.Dynamic)`                  |
| singleton     | `AddComponent(new UnitVisualPrefabTag { Prefab = prefabEntity })`            |

### 7.2 Система GameUnitVisualSystem

| Параметр            | Значение                                                                       |
|---------------------|--------------------------------------------------------------------------------|
| [BurstCompile]      | Да (вся система)                                                              |
| [UpdateInGroup]     | SimulationSystemGroup                                                          |
| [UpdateAfter]       | UnitCreateSystem                                                               |
| [UpdateBefore]      | — (можно обойти UnitMovementSystem, если будет)                               |
| require             | `UnitVisualPrefabTag` (singleton)                                              |
| query               | `UnitComponent` + `UnitVisualPrefabTag` (для потока) − `UnitVisualTag`        |
| ECB-аллокация       | `Allocator.Temp`                                                               |
| Паттерн             | ECB-карта + Playback                                                           |
| Позиция             | `ConvertOffsetToHexWorldPosition(pos)` → `float3`                             |

### 7.3 Обновление визуала

Когда юнит меняет позицию (будущая система `UnitMovementSystem` или логика геймплея), существующая визуальная инстанция должна обновляться. Варианты:

1. **EOSE-паттерн (Event-Only simulation engine):** `UnitMovementSystem` пишет новые координаты, `UnitVisualSystem` — обновляет `LocalTransform` существующего визуального клона.
2. **Синхронная подстройка:** в самой `GameUnitVisualSystem` при наличии `RefRW<UnitComponent>` обновлять `LocalTransform` визуальной сущности (без создания новых `Instantiate`).

---

## 8. Зависимости

| Компонент                  | Зависимость от                                          |
|----------------------------|---------------------------------------------------------|
| `UnitVisualPrefabTag`     | `Unity.Entities`                                        |
| `UnitVisualTag`            | `Unity.Entities`                                        |
| `UnitVisualInstance`       | `Unity.Entities`                                        |
| `GameUnitVisualPrefabAuthoring` | `Unity.Entities`, `UnityEngine`                    |
| `GameUnitVisualSystem`     | `Unity.Burst`, `Unity.Entities`, `Unity.Mathematics`, `Unity.Transforms` |

Визуальная система зависит от `UnitComponent` — система фильтрует кроме дополнительных тегов только сущности с `UnitComponent` и фильтрует из genesis серьёзные юниты, которые уже были визуализированы.

---

## 9. Edge-кейсы и заметки

1. **Отсутствие префаба:** если `UnitVisualPrefabTag` не добавлен ни на одну сущность, система молча завершает работу (`state.RequireForUpdate<UnitVisualPrefabTag>()` не пропустит без синглтона).

2. **Дублирующаяся визуализация при рестарте:** `UnitVisualTag` поставлен на сущность юнита и остаётся — система пропустит юнит. Если нет (кинутая сцена / reload), визуализация пересоздаётся. Для устойчивого поведения — добавление `UnitVisualTag` на юнита нужен также добавление на юнит через `ecb.AddComponent<UnitVisualTag>(entity)`.

3. **Разные модели для разных юнитов:** Текущая архитектура использует один глобальный префаб. Если требуется разные модели, необходимо:
   - Добавить в `UnitComponent` интегральный тип юнита (к таким штукам, где тип = единица);
   - Использовать `NativeHashMap<int, Entity> unitTypeToEntityMap` для маппинка `UnitID → Entity-префаб`;
   - при старте (например, через коллекцию префабов с разными `UnitID`) бараком `NativeHashMap` из набора ранее распознатых Entity-префабов.

4. **Позиция:** `UnitComponent.Position` — это `int2` в offset-координатах (col, row). Система конвертирует их в `float3` world-позицию, соответствующую `GameAreaSystem`. При flat-topped hexagon:
   - odd-row offset: `x = originX + hexSize * sqrt(3) * (col + 0.5)`, `z = originZ + hexSize * 1.5 * row`
   - Если hex grid сформирован `GameAreaSystem`, нужно иметь доступ к `GameAreaData` для `originX`/`originZ`, чтобы визуально не блокировать.

**Рекомендация:** для точного совпадения с `GameAreaSystem` лучше получать спрайт `originX`/`originZ` и `hexSize` через `SystemAPI.GetSingleton<GameAreaData>()`.