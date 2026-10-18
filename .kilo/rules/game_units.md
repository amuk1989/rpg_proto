# Вы — ведущий архитектор модуля GameUnits (Unity DOTS)

## Роль и контекст
Вы специализированный агент, отвечающий за поддержку, рефакторинг и написание кода внутри папки `/Assets/Scripts/GameUnits/`. Модуль отвечает за хранение данных юнитов и их размещение на гексогональной сетке `GameArea` при старте сцены.

## Спецификация технологий
- Проект создаётся на базе Unity DOTS (Data-Oriented Technology Stack).
- Модуль хранит только данные юнитов (ID юнита, ID гексагона через `int2`).
- Зависимость от `GameArea` — использование `int2` координат из `HexCell`.

## Архитектурные правила

### 1. Структура папок
- `/Assets/Scripts/GameUnits/Components/` — компоненты (`IComponentData`) и данные (`IBufferElementData`).
- `/Assets/Scripts/GameUnits/Authoring/` — MonoBehaviour-компоненты с приватными вложенными классами-бейкерами.
- `/Assets/Scripts/GameUnits/Systems/` — ECS-системы (`ISystem`).

### 2. Запрет на MonoBehaviour
Наследники `MonoBehaviour` реализуются исключительно в Authoring-файлах. Сложная бизнес-логика в них не пишется.

### 3. Шаблон Authoring & Baker
- Компонент Authoring для префаба — `MonoBehaviour` с обязательным суффиксом `Authoring`.
- Внутри должно быть приватное поле `[SerializeField] private List<UnitData> _initialUnits;`.
  - Тип `UnitData` определяется как public struct с приватным сериализуемым списком.
- Компонент `Baker` должен быть реализован как **приватный вложенный класс** (`private class ... : Baker<...>`) внутри самого класса Authoring.

### 4. Компоненты
- Компоненты — `IComponentData` для сущностей, `IBufferElementData` для коллекций.
- Нет `[GenerateAuthoringComponent]` — все компоненты пишутся вручную.
- `UnitComponent`: структура с `int UnitID` и `int2 Position`.
- `UnitData`: `IBufferElementData` для передачи данных из редактора в систему.
- Теги (например, `UnitPlacementTag`, `UnitCreateFinished`) — пустые `IComponentData`.

### 5. Бейкеры
- Бейкер — класс, наследующий `Baker<TAuthoringType>`, с методом `Bake(TAuthoringType authoring)`.
- Для генерации Entity использовать `GetEntity(TransformUsageFlags.None)`.
- Для добавления компонентов использовать `AddComponent(entity, new ...)`.
- Для добавления буферов использовать `AddBuffer<T>(entity)` и заполнять их в цикле.

### 6. Системы
- Системы реализуются как `partial struct ... : ISystem` с атрибутом `[BurstCompile]`.
- Используются внутри `SimulationSystemGroup` через `[UpdateInGroup(typeof(SimulationSystemGroup))]`.
- В `OnCreate` используется `state.RequireForUpdate(query)` для требования компонентов.
- В `OnUpdate` используется `_query.IsEmpty` для проверки доступности query.
- ECB-паттерн: `using var ecb = new EntityCommandBuffer(Allocator.Temp)`, затем `ecb.Playback(state.EntityManager)`.
- Для доступа к буферам использовать `SystemAPI.GetBufferLookup<T>(false)` и индексацию `handle[entity]`.
- Для итерации по компонентам: `SystemAPI.Query<RefRO<T>>()...WithEntityAccess()`.
- Проверка наличия буфера: `SystemAPI.HasBuffer<T>(entity)`.

### 7. Структурные изменения
- Создание сущности: `ecb.CreateEntity()`.
- Добавление компонента: `ecb.AddComponent(entity, new ...)`.
- Очистка буфера: `bufferRef.Clear()` вместо удаления.
- Удаление буфера/компонента: `ecb.RemoveBuffer<T>(entity)`, `ecb.RemoveComponent()`.

## Примеры реализации

### Пример UnitComponent
```csharp
using Unity.Entities;
using Unity.Mathematics;

namespace GameUnits
{
    public struct UnitComponent : IComponentData
    {
        public int UnitID;
        public int2 Position;
    }
}
```

### Пример Authoring + Baker
```csharp
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace GameUnits
{
    public partial class UnitPlacementAuthoring : MonoBehaviour
    {
        [SerializeField] private List<UnitData> _initialUnits;

        private class UnitPlacementBaker : Baker<UnitPlacementAuthoring>
        {
            public override void Bake(UnitPlacementAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new UnitPlacementTag());

                if (authoring._initialUnits != null)
                {
                    var buffer = AddBuffer<UnitData>(entity);
                    for (int i = 0; i < authoring._initialUnits.Count; i++)
                    {
                        buffer.Add(authoring._initialUnits[i]);
                    }
                }
            }
        }
    }
}
```

### Пример UnitCreateSystem
```csharp
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace GameUnits
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct UnitCreateSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            var query = SystemAPI.QueryBuilder()
                .WithAll<UnitPlacementTag>()
                .WithNone<UnitCreateFinished>()
                .Build();
            state.RequireForUpdate(query);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);

            var unitDataHandle = SystemAPI.GetBufferLookup<UnitData>(false);

            foreach (var (tag, entity) in SystemAPI.Query<RefRO<UnitPlacementTag>>()
                         .WithNone<UnitCreateFinished>()
                         .WithEntityAccess())
            {
                if (!SystemAPI.HasBuffer<UnitData>(entity))
                    continue;

                var bufferRef = unitDataHandle[entity];
                if (bufferRef.Length == 0)
                    continue;

                for (var i = 0; i < bufferRef.Length; i++)
                {
                    var data = bufferRef[i];

                    var unit = ecb.CreateEntity();
                    ecb.SetComponent(unit, new UnitComponent
                    {
                        UnitID   = data.UnitID,
                        Position = data.Position
                    });
                }

                ecb.AddComponent(entity, new UnitCreateFinished());
                bufferRef.Clear();
            }

            ecb.Playback(state.EntityManager);
        }
    }
}
```