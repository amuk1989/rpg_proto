using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameArea.UI
{
    /// <summary>
    ///     MonoBehaviour-контроллер панели создания игрового поля.
    ///     Требует PanelRenderer на том же GameObject.
    ///     Кнопка Create спавнит новую entity с GameAreaData через EntityManager.
    /// </summary>
    [RequireComponent(typeof(PanelRenderer))]
    public class GameAreaCreatorUI : MonoBehaviour
    {
        [SerializeField] private PanelRenderer _panelRenderer;

        private FloatField _fieldWidth;
        private FloatField _fieldHeight;
        private IntegerField _fieldHexCount;
        private Button _btnCreate;


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
            _fieldHeight.UnregisterAllRemovableCallbacks();
            _fieldHexCount.UnregisterAllRemovableCallbacks();
        }

        private void OnPanelReady(PanelRenderer panelRenderer, VisualElement root, int version)
        {
            _panelRenderer.UnregisterUIReloadCallback(OnPanelReady);

            _fieldWidth = root.Q<FloatField>("field-width");
            _fieldHeight = root.Q<FloatField>("field-height");
            _fieldHexCount = root.Q<IntegerField>("field-hex-count");
            _btnCreate = root.Q<Button>("btn-create");

            _fieldWidth.RegisterValueChangedCallback(_ => RefreshButton());
            _fieldHeight.RegisterValueChangedCallback(_ => RefreshButton());
            _fieldHexCount.RegisterValueChangedCallback(_ => RefreshButton());

            _btnCreate.clicked += OnCreateClicked;

            RefreshButton();
        }

        private bool IsValid()
        {
            return _fieldWidth.value > 0f &&
                   _fieldHeight.value > 0f &&
                   _fieldHexCount.value > 0;
        }

        private void RefreshButton()
        {
            _btnCreate.SetEnabled(IsValid());
        }

        private void OnCreateClicked()
        {
            var width = _fieldWidth.value;
            var height = _fieldHeight.value;
            var hexCount = _fieldHexCount.value;

            if (width <= 0f || height <= 0f || hexCount <= 0)
            {
                Debug.LogWarning("[GameAreaCreatorUI] Width, Height и HexCount должны быть больше нуля.");
                return;
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                Debug.LogError("[GameAreaCreatorUI] DefaultGameObjectInjectionWorld не найден.");
                return;
            }

            var em = world.EntityManager;
            var entity = em.CreateEntity();

            em.AddComponentData(entity, new GameAreaData
            {
                Size = new float2(width, height),
                HexCount = hexCount
            });

            Debug.Log(
                $"[GameAreaCreatorUI] Создана GameArea entity {entity}: Size=({width},{height}), HexCount={hexCount}");

            gameObject.SetActive(false);
        }
    }
}