using System;
using System.Collections.Generic;
using HamPocha.Data;
using HamPocha.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HamPocha.Editor
{
    // 씬 편집 단계에서만 사용한다. 기존 ID와 schema2 대응 키를 자동 재발급하지 않는다.
    public static class SaveIdSetup
    {
        [MenuItem("Tools/Ham Pocha/Save/Assign Missing IDs in Active Scene")]
        private static void AssignInActiveScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Scene scene = SceneManager.GetActiveScene();
            int assigned = AssignMissingIds(scene);
            Debug.Log($"[Save] event=save_id_setup result=assigned count={assigned}");
        }

        public static int AssignMissingIds(Scene scene)
        {
            int assigned = 0;
            foreach (GameObject target in GetTargets(scene))
            {
                SaveId id = target.GetComponent<SaveId>();
                if (id == null) id = Undo.AddComponent<SaveId>(target);
                if (!string.IsNullOrEmpty(id.Id)) continue;
                SetNewId(id);
                assigned++;
            }
            if (assigned > 0) EditorSceneManager.MarkSceneDirty(scene);
            return assigned;
        }

        [MenuItem("Tools/Ham Pocha/Save/Assign New IDs to Selected Copies")]
        private static void AssignToSelectedCopies()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            int assigned = 0;
            foreach (GameObject selected in Selection.gameObjects)
            {
                SaveId id = selected.GetComponent<SaveId>();
                if (id == null || !selected.scene.IsValid()) continue;
                SetNewId(id);
                EditorSceneManager.MarkSceneDirty(selected.scene);
                assigned++;
            }
            Debug.Log($"[Save] event=save_id_setup result=replaced_selected count={assigned}");
        }

        private static void SetNewId(SaveId id)
        {
            Undo.RecordObject(id, "Assign Save ID");
            SerializedObject serialized = new SerializedObject(id);
            serialized.FindProperty("_id").stringValue = Guid.NewGuid().ToString("N");
            // 신규/복제 대상은 이전 데이터의 대응 키를 이어받지 않는다.
            serialized.FindProperty("_schema2Key").stringValue = string.Empty;
            serialized.ApplyModifiedProperties();
        }

        public static List<GameObject> GetTargets(Scene scene)
        {
            List<GameObject> targets = new List<GameObject>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (TableOrder table in root.GetComponentsInChildren<TableOrder>(true)) AddTarget(targets, table.gameObject);
                foreach (VillageNpc customer in root.GetComponentsInChildren<VillageNpc>(true)) AddTarget(targets, customer.gameObject);
                foreach (HarvestTarget target in root.GetComponentsInChildren<HarvestTarget>(true)) AddTarget(targets, target.gameObject);
                foreach (ForestRegrowth forest in root.GetComponentsInChildren<ForestRegrowth>(true))
                {
                    SerializedObject serialized = new SerializedObject(forest);
                    AddProps(targets, serialized.FindProperty("_randomProps"));
                    AddProps(targets, serialized.FindProperty("_decorations"));
                }
            }
            return targets;
        }

        private static void AddProps(List<GameObject> targets, SerializedProperty props)
        {
            for (int i = 0; i < props.arraySize; i++)
            {
                Transform prop = props.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                if (prop == null) throw new InvalidOperationException("missing_forest_prop");
                AddTarget(targets, prop.gameObject);
            }
        }

        private static void AddTarget(List<GameObject> targets, GameObject target)
        {
            if (!targets.Contains(target)) targets.Add(target);
        }
    }
}
