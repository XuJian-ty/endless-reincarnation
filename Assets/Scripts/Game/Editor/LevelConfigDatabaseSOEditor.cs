using Game.Data;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Game.Editor
{
    [CustomEditor(typeof(LevelConfigDatabaseSO))]
    public class LevelConfigDatabaseSOEditor : UnityEditor.Editor
    {
        private const float DefaultCellSize = 40f;
        private const int DefaultShopCount = 2;
        private const int DefaultChestMin = 0;
        private const int DefaultChestMax = 2;

        private ReorderableList _levelsList;

        private void OnEnable()
        {
            SerializedProperty levelsProperty = serializedObject.FindProperty("levels");
            if (levelsProperty == null)
                return;

            _levelsList = new ReorderableList(serializedObject, levelsProperty, true, true, true, true);
            _levelsList.drawHeaderCallback = rect =>
            {
                EditorGUI.LabelField(rect, "关卡列表");
            };
            _levelsList.elementHeightCallback = index =>
            {
                SerializedProperty element = levelsProperty.GetArrayElementAtIndex(index);
                return element != null
                    ? EditorGUI.GetPropertyHeight(element, true) + 8f
                    : EditorGUIUtility.singleLineHeight + 8f;
            };
            _levelsList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                SerializedProperty element = levelsProperty.GetArrayElementAtIndex(index);
                if (element == null)
                    return;

                rect.y += 2f;
                rect.height = EditorGUI.GetPropertyHeight(element, true);
                EditorGUI.PropertyField(rect, element, true);
            };
            _levelsList.onAddCallback = list =>
            {
                AppendLevel(list.serializedProperty);
                RenumberLevels(list.serializedProperty);
                serializedObject.ApplyModifiedProperties();
            };
            _levelsList.onRemoveCallback = list =>
            {
                if (list.index < 0 || list.index >= list.serializedProperty.arraySize)
                    return;

                list.serializedProperty.DeleteArrayElementAtIndex(list.index);
                RenumberLevels(list.serializedProperty);
                serializedObject.ApplyModifiedProperties();
            };
            _levelsList.onReorderCallback = list =>
            {
                RenumberLevels(list.serializedProperty);
                serializedObject.ApplyModifiedProperties();
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty levelsProperty = serializedObject.FindProperty("levels");
            if (levelsProperty == null)
            {
                DrawDefaultInspector();
                serializedObject.ApplyModifiedProperties();
                return;
            }

            DrawScriptField();
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("关卡列表", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "这里按关卡槽位顺序管理关卡配置。支持直接拖拽调整顺序，删除当前选中的任意关卡。新增关卡后，记得同步补场景和对应生成方案。",
                MessageType.Info);
            _levelsList?.DoLayoutList();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawScriptField()
        {
            using (new EditorGUI.DisabledScope(true))
            {
                MonoScript script = MonoScript.FromScriptableObject((ScriptableObject)target);
                EditorGUILayout.ObjectField("脚本", script, typeof(MonoScript), false);
            }
        }

        private static void AppendLevel(SerializedProperty levelsProperty)
        {
            int oldSize = levelsProperty.arraySize;
            levelsProperty.arraySize++;
            SerializedProperty newLevel = levelsProperty.GetArrayElementAtIndex(oldSize);
            if (newLevel == null)
                return;

            SerializedProperty levelIndex = newLevel.FindPropertyRelative("levelIndex");
            SerializedProperty sceneName = newLevel.FindPropertyRelative("sceneName");
            SerializedProperty sceneGuid = newLevel.FindPropertyRelative("sceneGuid");
            SerializedProperty library = newLevel.FindPropertyRelative("globalSpawnPlanLibrary");
            SerializedProperty planIndex = newLevel.FindPropertyRelative("globalSpawnPlanIndex");
            SerializedProperty chestMinCount = newLevel.FindPropertyRelative("chestMinCount");
            SerializedProperty chestMaxCount = newLevel.FindPropertyRelative("chestMaxCount");
            SerializedProperty shopCount = newLevel.FindPropertyRelative("shopCount");
            SerializedProperty cellSize = newLevel.FindPropertyRelative("cellSize");
            SerializedProperty eliteDropEntries = newLevel.FindPropertyRelative("eliteDropEntries");
            SerializedProperty guardianDropEntries = newLevel.FindPropertyRelative("guardianDropEntries");
            SerializedProperty bossDropEntries = newLevel.FindPropertyRelative("bossDropEntries");

            levelIndex.intValue = oldSize + 1;
            sceneName.stringValue = $"Level_{oldSize + 1}";
            sceneGuid.stringValue = string.Empty;
            planIndex.intValue = 0;
            chestMinCount.intValue = DefaultChestMin;
            chestMaxCount.intValue = DefaultChestMax;
            shopCount.intValue = DefaultShopCount;
            cellSize.floatValue = DefaultCellSize;
            eliteDropEntries.arraySize = 0;
            guardianDropEntries.arraySize = 0;
            bossDropEntries.arraySize = 0;

            if (oldSize > 0)
            {
                SerializedProperty previousLevel = levelsProperty.GetArrayElementAtIndex(oldSize - 1);
                SerializedProperty previousLibrary = previousLevel?.FindPropertyRelative("globalSpawnPlanLibrary");
                if (previousLibrary != null)
                    library.objectReferenceValue = previousLibrary.objectReferenceValue;
            }
            else
            {
                library.objectReferenceValue = null;
            }
        }

        private static void RenumberLevels(SerializedProperty levelsProperty)
        {
            for (int i = 0; i < levelsProperty.arraySize; i++)
            {
                SerializedProperty levelProperty = levelsProperty.GetArrayElementAtIndex(i);
                SerializedProperty levelIndex = levelProperty?.FindPropertyRelative("levelIndex");
                if (levelIndex != null)
                    levelIndex.intValue = i + 1;
            }
        }
    }
}
