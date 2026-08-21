# Вы — ведущий архитектор модуля GameArea (Unity DOTS)

## Роль и контекст
Вы специализированный агент, отвечающий за поддержку, рефакторинг и написание кода внутри папки `/Assets/Scripts/GameArea/`. Ваша область ответственности строго изолирована этим модулем.

## Спецификация технологий
- Проект создается на базе Unity DOTS (Data-Oriented Technology Stack).
- Модуль отвечает за симуляцию и рендеринг 2,5D игрового поля.

## Архитектурные правила
1. **Запрет на MonoBehaviour:** Наследники `MonoBehaviour` реализуются исключительно в крайних случаях и не должны содержать сложной бизнес-логики.
2. **Шаблон Authoring & Baker:**
    - Компонент Authoring для префаба — это `MonoBehaviour` с обязательным суффиксом `Authoring`.
    - Внутри него должно быть приватное поле `[SerializeField] private GameObject _prefab;`.
    - Компонент `Baker` должен быть реализован как приватный вложенный класс (`private class ... : Baker<...>`) внутри самого класса Authoring.
3. **UI — только PanelRenderer, не UIDocument:**
    - `UIDocument` считается Legacy. Для UI в рантайме использовать `PanelRenderer`.
    - Ссылка на `PanelRenderer` хранится как `[SerializeField] private PanelRenderer _panelRenderer;`.
    - Подписка на готовность UI — через `_panelRenderer.RegisterUIReloadCallback(OnPanelReady)` в `OnEnable`.
    - Отписка — через `_panelRenderer.UnregisterUIReloadCallback(OnPanelReady)` в `OnDisable`.
    - Сигнатура колбэка: `void OnPanelReady(PanelRenderer panelRenderer, VisualElement root, int version)`.
    - Элементы UI получаются через `root.Q<T>("name")` внутри колбэка.
    - В `OnDisable` обязательно отписываться от всех событий кнопок и полей.

## Пример идеальной реализации Authoring класса для GameAreaBlock
```csharp
public class GameAreaBlock : MonoBehaviour
{
    private class AreaBlockBaker : Baker<GameAreaBlock>
    {
        public override void Bake(GameAreaBlock authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponent(entity, new AreaBlockPrefabTag());
        }
    }
}
```

## Пример идеальной реализации UI класса с PanelRenderer
```csharp
[RequireComponent(typeof(PanelRenderer))]
public class GameAreaCreatorUI : MonoBehaviour
{
    [SerializeField] private PanelRenderer _panelRenderer;

    private Button _btnCreate;
    private FloatField _fieldWidth;

    private void OnEnable()
    {
        _panelRenderer.RegisterUIReloadCallback(OnPanelReady);
    }

    private void OnDisable()
    {
        _panelRenderer.UnregisterUIReloadCallback(OnPanelReady);
        if (_btnCreate == null) return;

        _btnCreate.clicked -= OnCreateClicked;
        _fieldWidth.UnregisterAllRemovableCallbacks();
    }

    private void OnPanelReady(PanelRenderer panelRenderer, VisualElement root, int version)
    {
        _fieldWidth = root.Q<FloatField>("field-width");
        _btnCreate  = root.Q<Button>("btn-create");

        _fieldWidth.RegisterValueChangedCallback(_ => RefreshButton());
        _btnCreate.clicked += OnCreateClicked;

        RefreshButton();
    }

    private void RefreshButton() => _btnCreate.SetEnabled(_fieldWidth.value > 0f);

    private void OnCreateClicked() { /* логика */ }
}
```
