# Вы — ведущий архитектор модуля GameUnits (Unity DOTS)

## Роль и контекст
Вы специализированный агент, отвечающий за поддержку, рефакторинг и написание кода внутри папки `/Assets/Scripts/GameUnits/`. Модуль отвечает за хранение данных юнитов, их начальное размещение на гексогональной сетке `GameArea` при старте сцены и визуализацию юнитов.

## Спецификация технологий
- Проект создаётся на базе Unity DOTS (Data-Oriented Technology Stack).
- Модуль хранит данные юнитов (ID юнита, ID гексагона через `int2`).
- Модуль также стоитит визуализацию юнитов — инстанцирование GameObject-префаба для каждой сущности с `UnitComponent`.
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
- Вложенный Baker должен быть **приватным вложенным классом** (`private class ... : Baker<...>`) внутри класса Authoring.
- Или используется `[DisallowMultipleComponent]` на классе Authoring.

#### 3.1 UnitPlacementAuthoring (список начальных юнитов)
- Внутреннее поле: `[SerializeField] private List<UnitData> _initialUnits;`
- Baker: `GetEntity(TransformUsageFlags.None)` → `AddComponent(UnitPlacementTag)` → `AddBuffer<UnitData>` с копированием из `authoring._initialUnits`.

#### 3.2 GameUnitVisualPrefabAuthoring (префаб визуализации)
- Внутреннее поле: `[SerializeField] private GameObject _prefab;`
- Baker: `GetEntity(authoring._prefab, TransformUsageFlags.Dynamic)` → `GetEntity(TransformUsageFlags.None)` → `AddComponent(UnitVisualPrefabTag { Prefab = prefabEntity })`.

### 4. Компоненты

#### IComponentData (теги и данные)
| Компонент | Поля | Назначение |
|-----------|------|------------|
| `UnitPlacementTag` | — | Тег сущности-контейнера с `DynamicBuffer<UnitData>` |
| `UnitCreateFinished` | — | Маркер: размещение юнитов обработано |
| `UnitComponent` | `int UnitID`, `int2 Position` | Сущность юнита (главный компонент данных) |
| `UnitVisualPrefabTag` | `Entity Prefab` | Синглтон: ссылка на Entity-префаб визуализации |
| `UnitVisualTag` | — | Тег сущности-юнита: визуал уже создан (защита от дублирования) |

#### IBufferElementData (буферные данные)
| Компонент | Поля | Назначение |
|-----------|------|------------|
| `UnitData` | `int UnitID`, `int2 Position` | Данные юнита для `DynamicBuffer` в `UnitPlacementTag`-сущности |
| `UnitVisualInstance` | `Entity VisualEntity` | Связь юнита с его визуальной ECS-сущностью (опционально) |

**Правила компонентов:**
- Нет `[GenerateAuthoringComponent]` — все компоненты пишутся вручную.
- Текстовые комментарии с `/// <summary>` обязательны.
- Теги — пустые структуры без полей.

### 5. Бейкеры
- Бейкер — приватный вложенный класс, наследующий `Baker<TAuthoringType>`, с методом `Bake(TAuthoringType authoring)`.
- Для генерации Entity использовать `GetEntity(TransformUsageFlags.None)` / `GetEntity(TransformUsageFlags.Dynamic)`.
- Для добавления компонентов использовать `AddComponent(entity, new ...)`.
- Для добавления буферов использовать `AddBuffer<T>(entity)` и заполнять их в цикле.
- Для конвертации GameObject→Entity использовать `GetEntity(authoring._prefab, TransformUsageFlags.Dynamic)`.

### 6. Системы
- Системы реализуются как `partial struct ... : ISystem` с атрибутом `[BurstCompile]`.
- Используются внутри `SimulationSystemGroup` через `[UpdateInGroup(typeof(SimulationSystemGroup))]`.

#### Порядок обновления систем
```
UnitCreateSystem        (создаёт сущности юнитов из UnitData)
    ↓
GameUnitVisualSystem    (инстанцирует визуал для каждого UnitComponent)
```

#### UnitCreateSystem
- `OnCreate`: Query с `UnitPlacementTag`, без `UnitCreateFinished`. `state.RequireForUpdate(query)`.
- `OnUpdate`:
  - `using var ecb = new EntityCommandBuffer(Allocator.Temp)`.
  - `var unitDataHandle = SystemAPI.GetBufferLookup<UnitData>(false)`.
  - `foreach (var (tag, entity) in SystemAPI.Query<RefRO<UnitPlacementTag>>().WithNone<UnitCreateFinished>().WithEntityAccess())`.
  - Проверка буфера: `SystemAPI.HasBuffer<UnitData>(entity)`.
  - Получение буфера: `var bufferRef = unitDataHandle[entity]`.
  - Создание Entity: `var unit = ecb.CreateEntity()` → `ecb.SetComponent(unit, new UnitComponent { ... })`.
  - Маркер завершения: `ecb.AddComponent(entity, new UnitCreateFinished())`.
  - Очистка буфера: `bufferRef.Clear()`.
  - `ecb.Playback(state.EntityManager)`.

#### GameUnitVisualSystem
- Атрибуты: `[BurstCompile]`, `[UpdateInGroup(typeof(SimulationSystemGroup))]`, `[UpdateAfter(typeof(UnitCreateSystem))]`.
- `OnCreate`: `state.RequireForUpdate<UnitVisualPrefabTag>()`. Query: `UnitComponent` + без `UnitVisualTag`. `_pendingQuery` в private-поле.
- `OnUpdate`:
  - Проверка пустоты: `if (_pendingQuery.IsEmpty) return;`
  - Получение префаба-синглтона: `Entity prefab = SystemAPI.GetSingleton<UnitVisualPrefabTag>().Prefab;`
  - ECB: `using var ecb = new EntityCommandBuffer(Allocator.Temp);`
  - Итерация: `foreach (var (unit, entity) in SystemAPI.Query<RefRO<UnitComponent>>().WithNone<UnitVisualTag>().WithEntityAccess())`
  - Позиция: `unit.ValueRO.Position.x` → `worldPositionX`, `unit.ValueRO.Position.y` → `worldPositionZ`
  - Инстанцирование: `Entity visual = ecb.Instantiate(prefab);`
  - Трансформ: `ecb.SetComponent(visual, new LocalTransform { Position = new float3(worldPositionX, 0f, worldPositionZ), Rotation = quaternion.identity, Scale = 1f });`
  - Теги: `ecb.AddComponent<UnitVisualTag>(entity);`
  - Буфер визуала: `if (SystemAPI.HasBuffer<UnitVisualInstance>(entity)) { var buffer = SystemAPI.GetBuffer<UnitVisualInstance>(entity); buffer.Add(new UnitVisualInstance { VisualEntity = visual }); }`
  - `ecb.Playback(state.EntityManager);`

#### Общие правила для всех систем
- Проверка наличия буфера: `SystemAPI.HasBuffer<T>(entity)`.
- Использование ECB-паттерна: `using var ecb = new EntityCommandBuffer(Allocator.Temp)` → `ecb.Playback(state.EntityManager)`.
- Запрос component-by-value с entity в foreach: `(unit, entity)` деструктуризация через `WithEntityAccess()`.
- Проверка отсутствия компонента: `.WithNone<T>()`.
- Метод `OnDestroy` всегда пустой или с заглушкой.

### 7. Структурные изменения
- Создание сущности: `ecb.CreateEntity()`.
- Добавление компонента: `ecb.AddComponent(entity, new ...)` или `ecb.SetComponent(entity, new ...)`.
- Очистка буфера: `bufferRef.Clear()` вместо удаления.
- Инстанцирование префаба: `ecb.Instantiate(prefab)`.

### 8. Пространство имён и организация
- Всё в пространстве имён `GameUnits`.
- Все типы — `public`.
- Комментарии с `/// <summary>` обязательны для всех `IComponentData`, `IBufferElementData`, систем и авторинг-классов.
- Мета-файлы (`.meta`) — не редактировать.