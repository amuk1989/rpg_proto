## План: Модуль Юнитов (Размещение при старте, данные)

**1. Цель:**
Разработать модуль "Юниты", который позволит размещать юнитов на гексаго Gnaльной сетке `GameArea` при старте сцены, храня только их ID и ID гексагона.

**2. Область применения:**
- Определить структуры данных для юнитов, включая их ID и ID гексагона.
- Реализовать логику размещения юнитов на гексаго Gnaльной сетке `GameArea` при старте сцены.
- Хранить только данные юнитов (ID юнита, ID гексагона).

**3. Технологический стек:**
- Unity DOTS (Data-Oriented Technology Stack)
- C#

**4. Ключевые особенности и структуры данных:**

    *   **Данные юнита:**
        *   `UnitComponent`: Структура для хранения данных юнита (`int UnitID`, `HexCoordinates Position`). `HexCoordinates` будет использоваться как ID гексагона.
        *   `HexCoordinates`: Структура, предположительно из модуля `GameArea`, представляющая координаты гексагона.

    *   **Размещение юнитов при старте сцены:**
        *   `UnitPlacementAuthoring` (MonoBehaviour): Компонент для настройки префабов юнитов и их начальных позиций на сцене. Будет содержать, например, список начальных юнитов с их ID и целевыми гексагонами.
        *   `UnitPlacementBaker`: Бейкер для преобразования данных из `UnitPlacementAuthoring` в DOTS-компоненты.
        *   `UnitPlacementSystem`: Система, которая при старте сцены (например, в `OnCreate`) будет считывать данные из `Component` и размещать юнитов на соответствующих гексагонах, используя данные из `GameArea`.

    *   **Интеграция с `GameArea`:**
        *   Зависимость: Модуль "Юниты" будет зависеть от `GameArea` для получения информации о гексаго Gnaльной сетке и ее координатах.
        *   Взаимодействие: Система `UnitPlacementSystem` будет запрашивать у `GameArea` информацию о гексагонах для размещения юнитов.

**5. Архитектура:**
- Стандартные паттерны Unity DOTS: Системы, Компоненты (structs), Entities.
- Authoring components (`MonoBehaviour` with `Authoring` suffix) for prefabs and initial configuration.
- Bakers (`Baker<T>`) to convert Authoring data to DOTS components.

**6. Шаги реализации:**

    1.  **Определить структуру `UnitComponent`:** Создать структуру `UnitComponent` с полями `UnitID` (int) и `Position` (`HexCoordinates`).
    2.  **Создать Authoring компонент `UnitPlacementAuthoring`:** Добавить `MonoBehaviour` с полем `[SerializeField] private List<InitialUnitData> _initialUnits;`, где `InitialUnitData` содержит `UnitID` и `HexCoordinates`.
    3.  **Реализовать `UnitPlacementBaker`:** Бейкер для `UnitPlacementAuthoring`, который будет создавать `UnitComponent` для каждой сущности юнита.
    4.  **Разработать `UnitPlacementSystem`:** Система, которая при старте сцены будет создавать сущности юнитов и присваивать им `UnitComponent` с начальными данными.
    5.  **Интеграция с `GameArea`:** Убедиться, что `UnitPlacementSystem` корректно использует `HexCoordinates` из `GameArea`.

**7. Риски и их митигация:**
- **Отсутствие визуализации:** Пока юниты будут только данными. Если потребуется визуализация, это будет отдельной задачей.
- **Зависимость от `GameArea`:** Корректность работы зависит от доступности и правильности структуры `HexCoordinates` в `GameArea`.

**8. Валидация:**
- Юнит-тесты для `UnitComponent` и `UnitPlacementBaker`.
- Тестирование в редакторе Unity: размещение юнитов при старте сцены с проверкой корректности их ID и координат.

**9. Открытые вопросы:**
- Нет.
