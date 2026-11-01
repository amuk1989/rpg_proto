using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

using GameArea;
using GameUnits;

namespace GameArea.UI
{
    /// <summary>
    ///     MonoBehaviour-контроллер панели создания игрового поля.
    ///     Требует PanelRenderer на том же GameObject.
    ///     Кнопка Create спавнит новую entity с GameAreaData через EntityManager.
    ///     Дополнительно поддерживает расстановку начальных юнитов (UnitID + col/row),
    ///     которые передаются в ECS-сущность с UnitPlacementTag и DynamicBuffer<UnitData>.
    /// </summary>
    [RequireComponent(typeof(PanelRenderer))]
    public class GameAreaCreatorUI : MonoBehaviour
    {
        [SerializeField] private PanelRenderer _panelRenderer;

        // Game Area fields
        private FloatField _fieldWidth;
        private FloatField _fieldHeight;
        private IntegerField _fieldHexCount;
        private Button _btnCreate;

        // Unit fields
        private IntegerField _fieldUnitId;
        private IntegerField _fieldUnitCol;
        private IntegerField _fieldUnitRow;
        private Button _btnAddUnit;
        private Button _btnClearUnits;
        private VisualElement _unitListContainer;

        // Collected units (before creation)
        private List<UnitData> _units = new List<UnitData>();

        private void OnEnable()
        {
            _panelRenderer.RegisterUIReloadCallback(OnPanelReady);
        }

        private void OnDisable()
        {
            _panelRenderer.UnregisterUIReloadCallback(OnPanelReady);
            if (_btnCreate == null) return;

            _btnCreate.clicked -= OnCreateClicked;
            _btnAddUnit.clicked -= OnAddUnitClicked;
            _btnClearUnits.clicked -= OnClearUnitsClicked;

            _fieldWidth.UnregisterAllRemovableCallbacks();
            _fieldHeight.UnregisterAllRemovableCallbacks();
            _fieldHexCount.UnregisterAllRemovableCallbacks();
            _fieldUnitId.UnregisterAllRemovableCallbacks();
            _fieldUnitCol.UnregisterAllRemovableCallbacks();
            _fieldUnitRow.UnregisterAllRemovableCallbacks();
        }

        private void OnPanelReady(PanelRenderer panelRenderer, VisualElement root, int version)
        {
            _panelRenderer.UnregisterUIReloadCallback(OnPanelReady);

            // Game Area
            _fieldWidth = root.Q<FloatField>("field-width");
            _fieldHeight = root.Q<FloatField>("field-height");
            _fieldHexCount = root.Q<IntegerField>("field-hex-count");
            _btnCreate = root.Q<Button>("btn-create");

            _fieldWidth.RegisterValueChangedCallback(_ => RefreshButton());
            _fieldHeight.RegisterValueChangedCallback(_ => RefreshButton());
            _fieldHexCount.RegisterValueChangedCallback(_ => RefreshButton());

            _btnCreate.clicked += OnCreateClicked;

            // Units
            _fieldUnitId = root.Q<IntegerField>("field-unit-id");
            _fieldUnitCol = root.Q<IntegerField>("field-unit-col");
            _fieldUnitRow = root.Q<IntegerField>("field-unit-row");
            _btnAddUnit = root.Q<Button>("btn-add-unit");
            _btnClearUnits = root.Q<Button>("btn-clear-units");
            _unitListContainer = root.Q<VisualElement>("unit-list-container");

            _btnAddUnit.clicked += OnAddUnitClicked;
            _btnClearUnits.clicked += OnClearUnitsClicked;

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

        // ==================== unit list management ====================

        private void OnAddUnitClicked()
        {
            var unitId = _fieldUnitId.value;
            var col = _fieldUnitCol.value;
            var row = _fieldUnitRow.value;

            var unit = new UnitData
            {
                UnitID = unitId,
                Position = new int2(col, row)
            };
            _units.Add(unit);

            RenderUnitList();

            Debug.Log($"[GameAreaCreatorUI] Added unit #{_units.Count}: ID={unitId}, Pos=({col},{row})");

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                Debug.LogError("[GameAreaCreatorUI] DefaultGameObjectInjectionWorld не найден.");
                return;
            }

            var em = world.EntityManager;

            // 1. Game Area
            var entity = em.CreateEntity();
            em.AddComponentData(entity, new UnitPlacementTag());
            var buffer = em.AddBuffer<UnitData>(entity);
            buffer.Add(unit);
        }

        private void RenderUnitList()
        {
            _unitListContainer.Clear();

            if (_units.Count == 0)
            {
                var emptyLabel = new Label("No units added");
                emptyLabel.name = "unit-list-empty";
                _unitListContainer.Add(emptyLabel);
                return;
            }

            for (int i = 0; i < _units.Count; i++)
            {
                var u = _units[i];
                var row = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        marginBottom = 2f,
                    }
                };

                var label = new Label($"[{i}] ID={u.UnitID}, Pos=({u.Position.x},{u.Position.y})")
                {
                    style = { flexGrow = 1, fontSize = 11, color = new Color(1, 1, 1, 0.8f) }
                };
                row.Add(label);

                var removeBtn = new Button(() => OnRemoveUnit(i))
                {
                    text = "✕",
                    style =
                    {
                        width = 24f,
                        height = 20f,
                        fontSize = 10,
                        backgroundColor = new Color(0.8f, 0.3f, 0.3f),
                        color = Color.white,
                        marginLeft = 4f,
                    }
                };
                row.Add(removeBtn);
                _unitListContainer.Add(row);
            }
        }

        private void OnRemoveUnit(int index)
        {
            _units.RemoveAt(index);
            RenderUnitList();
        }

        private void OnClearUnitsClicked()
        {
            _units.Clear();
            RenderUnitList();
        }

        // ==================== creation ====================

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

            if (_units.Count == 0)
            {
                Debug.LogWarning("[GameAreaCreatorUI] Список начальных юнитов пуст. Создано только поле без юнитов.");
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                Debug.LogError("[GameAreaCreatorUI] DefaultGameObjectInjectionWorld не найден.");
                return;
            }

            var em = world.EntityManager;

            // 1. Game Area
            var entity = em.CreateEntity();
            em.AddComponentData(entity, new GameAreaData
            {
                Size = new float2(width, height),
                HexCount = hexCount
            });

            Debug.Log(
                $"[GameAreaCreatorUI] Создана GameArea entity {entity}: Size=({width},{height}), HexCount={hexCount}");

            // 2. Units (if any)
            if (_units.Count > 0)
            {
                var placementEntity = em.CreateEntity();
                em.AddComponent<UnitPlacementTag>(placementEntity);

                var buffer = em.AddBuffer<UnitData>(placementEntity);
                for (int i = 0; i < _units.Count; i++)
                {
                    buffer.Add(_units[i]);
                }

                // Force simulation step so UnitCreateSystem processes units immediately
                var simulation = world.GetOrCreateSystemManaged<SimulationSystemGroup>();
                if (simulation != null)
                    simulation.Update();

                Debug.Log($"[GameAreaCreatorUI] Created {(_units.Count > 0 ? _units.Count : 0)} unit(s) on entity {placementEntity}");
            }

            // gameObject.SetActive(false);
        }
    }
}