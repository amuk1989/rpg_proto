using RpgProto.Addressables;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace Addressable.Editor
{
    public class ReferenceDrawerProcessor<T> : OdinValueDrawer<Reference<T>> where T : class
    {
        private InspectorProperty _guidProperty;

        protected override void Initialize()
        {
            // Находим скрытую строку GUID внутри вашей структуры Reference<T>
            _guidProperty = Property.Children["_assetGuid"];
        }

        protected override void DrawPropertyLayout(GUIContent label)
        {
            if (_guidProperty == null)
            {
                Sirenix.Utilities.Editor.SirenixEditorGUI.ErrorMessageBox($"Field '_assetGuid' not found in Reference<{typeof(T).Name}>");
                return;
            }

            // 1. Извлекаем текущую строку GUID из сериализованных данных
            string currentGuid = (string)_guidProperty.ValueEntry.WeakSmartValue;

            // 2. Конвертируем GUID в GameObject (префаб) или компонент для показа в инспекторе
            string assetPath = AssetDatabase.GUIDToAssetPath(currentGuid);
            
            // Если T это компонент (например, ScriptableObject или MonoBehaviour), 
            // для ObjectField нам нужен тип, наследуемый от UnityEngine.Object
            System.Type targetType = typeof(UnityEngine.Object).IsAssignableFrom(typeof(T)) ? typeof(T) : typeof(GameObject);
            UnityEngine.Object currentAsset = AssetDatabase.LoadAssetAtPath(assetPath, targetType);

            // 3. Рисуем стандартное и привычное поле выбора объекта (туда можно ДРАГАТЬ префабы!)
            EditorGUI.BeginChangeCheck();
            UnityEngine.Object newAsset = EditorGUILayout.ObjectField(label, currentAsset, targetType, false);

            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            string newGuid = string.Empty;

            if (newAsset != null)
            {
                // Получаем GUID перетащенного префаба
                string path = AssetDatabase.GetAssetPath(newAsset);
                string guid = AssetDatabase.AssetPathToGUID(path);

                if (!string.IsNullOrEmpty(guid))
                {
                    // 4. АВТО-РЕГИСТРАЦИЯ В ADDRESSABLES
                    var settings = AddressableAssetSettingsDefaultObject.Settings;
                    if (settings != null)
                    {
                        var entry = settings.FindAssetEntry(guid);
                            
                        // Если префаб еще НЕ в Addressables, добавляем его автоматически в дефолтную группу!
                        if (entry == null)
                        {
                            entry = settings.CreateOrMoveEntry(guid, settings.DefaultGroup);
                                
                            // Опционально: сразу генерируем красивое имя по вашему стандарту
                            string shortTypeName = newAsset.GetType().Name;
                            entry.address = $"{shortTypeName}/{newAsset.name}";
                                
                            Debug.Log($"<color=green>[Addressables]</color> Asset '{newAsset.name}' was automatically marked as Addressable with address: <b>{entry.address}</b>");
                        }
                    }

                    newGuid = guid;
                }
            }

            // 5. Записываем полученный GUID обратно в структуру, Unity сам его сериализует
            _guidProperty.ValueEntry.WeakSmartValue = newGuid;
        }
    }
}