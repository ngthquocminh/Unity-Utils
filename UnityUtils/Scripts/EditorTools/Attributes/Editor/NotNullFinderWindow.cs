using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityUtils
{
    public class NotNullFinderWindow : EditorWindow
    {
        private Vector2 _scroll;
        private List<NotNullIssue> _results = new List<NotNullIssue>();
        private bool _isScanning;

        [MenuItem("Tools/NotNull Finder")]
        public static void ShowWindow()
        {
            var w = GetWindow<NotNullFinderWindow>(false, "NotNull Finder", true);
            w.minSize = new Vector2(600, 300);
        }

        private void OnGUI()
        {
            GUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Scan Prefabs & Assets"))
            {
                ScanAllAssets();
            }
            if (GUILayout.Button("Scan Scenes"))
            {
                ScanAllScenes();
            }
            if (GUILayout.Button("Scan Everything"))
            {
                ScanEverything();
            }
            if (GUILayout.Button("Clear"))
            {
                _results.Clear();
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(6);
            EditorGUILayout.LabelField($"Results: {_results.Count}", EditorStyles.boldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < _results.Count; i++)
            {
                var r = _results[i];
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField(r.Summary, EditorStyles.label);
                EditorGUILayout.LabelField(r.Details, EditorStyles.miniLabel);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Ping"))
                {
                    if (r.Asset != null)
                    {
                        EditorGUIUtility.PingObject(r.Asset);
                        Selection.activeObject = r.Asset;
                    }
                    else if (!string.IsNullOrEmpty(r.ScenePath) && r.SceneObject != null)
                    {
                        // open the scene and select the object
                        EditorSceneManager.OpenScene(r.ScenePath, OpenSceneMode.Single);
                        Selection.activeGameObject = r.SceneObject;
                        EditorGUIUtility.PingObject(r.SceneObject);
                    }
                }
                if (GUILayout.Button("Select"))
                {
                    if (r.Asset != null)
                    {
                        Selection.activeObject = r.Asset;
                    }
                    else if (!string.IsNullOrEmpty(r.ScenePath) && r.SceneObject != null)
                    {
                        EditorSceneManager.OpenScene(r.ScenePath, OpenSceneMode.Single);
                        Selection.activeGameObject = r.SceneObject;
                    }
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();
        }

        private void ScanEverything()
        {
            _results.Clear();
            ScanAllAssets();
            ScanAllScenes();
            Repaint();
        }

        private void ScanAllAssets()
        {
            if (_isScanning) return;
            try
            {
                _isScanning = true;
                _results.RemoveAll(r => !string.IsNullOrEmpty(r.ScenePath));

                // Find prefabs and ScriptableObjects and other asset types
                string[] guids = AssetDatabase.FindAssets("t:Prefab t:ScriptableObject t:GameObject");
                int total = guids.Length;
                for (int i = 0; i < total; i++)
                {
                    string guid = guids[i];
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    EditorUtility.DisplayProgressBar("NotNull Finder", "Scanning assets...", (float)i / Math.Max(1, total));

                    UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                    if (asset == null) continue;

                    if (asset is GameObject go)
                    {
                        // prefab: check root and all children components
                        CheckGameObjectForNotNull(go, path);
                    }
                    else if (asset is ScriptableObject so)
                    {
                        CheckObjectFieldsForNotNull(so, path);
                    }
                    else
                    {
                        // fallback: try to reflect over asset
                        CheckObjectFieldsForNotNull(asset, path);
                    }
                }
                EditorUtility.ClearProgressBar();
            }
            finally
            {
                _isScanning = false;
            }
        }

        private void ScanAllScenes()
        {
            if (_isScanning) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                // user cancelled - abort
                return;
            }

            try
            {
                _isScanning = true;
                _results.RemoveAll(r => !string.IsNullOrEmpty(r.AssetPath) && r.AssetPath.StartsWith("scene:"));

                string[] sceneGuids = AssetDatabase.FindAssets("t:Scene");
                int total = sceneGuids.Length;

                // Save currently open scene paths to restore later
                var openScenes = new List<string>();
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var s = SceneManager.GetSceneAt(i);
                    openScenes.Add(s.path);
                }

                for (int i = 0; i < total; i++)
                {
                    string guid = sceneGuids[i];
                    string scenePath = AssetDatabase.GUIDToAssetPath(guid);
                    EditorUtility.DisplayProgressBar("NotNull Finder", "Scanning scenes...", (float)i / Math.Max(1, total));

                    var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    if (!scene.isLoaded) continue;

                    var roots = scene.GetRootGameObjects();
                    foreach (var root in roots)
                    {
                        foreach (var comp in root.GetComponentsInChildren<Component>(true))
                        {
                            if (comp == null) continue; // missing script
                            CheckObjectFieldsForNotNull(comp, scenePath, rootSceneObject: comp.gameObject);
                        }
                    }
                }

                // restore previous scenes
                if (openScenes.Count > 0)
                {
                    // open first originally-open scene to restore user's state
                    EditorSceneManager.OpenScene(openScenes[0], OpenSceneMode.Single);
                    // open others additively
                    for (int i = 1; i < openScenes.Count; i++)
                    {
                        EditorSceneManager.OpenScene(openScenes[i], OpenSceneMode.Additive);
                    }
                }

                EditorUtility.ClearProgressBar();
            }
            finally
            {
                _isScanning = false;
            }
        }

        private void CheckGameObjectForNotNull(GameObject prefabRoot, string assetPath)
        {
            foreach (var comp in prefabRoot.GetComponentsInChildren<Component>(true))
            {
                if (comp == null) continue;
                CheckObjectFieldsForNotNull(comp, assetPath);
            }
        }

        private void CheckObjectFieldsForNotNull(UnityEngine.Object obj, string assetPath, GameObject rootSceneObject = null)
        {
            if (obj == null) return;

            var type = obj.GetType();
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var f in fields)
            {
                // Only check fields decorated with NotNullAttribute
                if (!f.IsDefined(typeof(NotNullAttribute), inherit: true)) continue;

                object value = null;
                try
                {
                    value = f.GetValue(obj);
                }
                catch { /* ignore reflection failures */ }

                if (IsNullOrContainsNull(value, out var detail))
                {
                    var issue = new NotNullIssue
                    {
                        Asset = (assetPath != null && assetPath.EndsWith(".unity") == false && rootSceneObject == null) ? AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) : null,
                        AssetPath = assetPath,
                        ScenePath = assetPath != null && assetPath.EndsWith(".unity") ? assetPath : null,
                        SceneObject = rootSceneObject,
                        FieldName = f.Name,
                        FieldType = f.FieldType.FullName,
                        Details = detail
                    };
                    _results.Add(issue);
                }
            }
        }

        private bool IsNullOrContainsNull(object value, out string details)
        {
            details = string.Empty;
            if (value == null)
            {
                details = "Value is null";
                return true;
            }

            // Handle UnityEngine.Object null semantics
            if (value is UnityEngine.Object uo)
            {
                if (uo == null)
                {
                    details = "UnityEngine.Object is null";
                    return true;
                }
                return false;
            }

            // Arrays
            if (value is Array arr)
            {
                for (int i = 0; i < arr.Length; i++)
                {
                    var el = arr.GetValue(i);
                    if (el is UnityEngine.Object u)
                    {
                        if (u == null)
                        {
                            details = $"Array element #{i} is null";
                            return true;
                        }
                    }
                    else if (el == null)
                    {
                        details = $"Array element #{i} is null";
                        return true;
                    }
                }
                return false;
            }

            // IList (List<>)
            if (value is IList list)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var el = list[i];
                    if (el is UnityEngine.Object u)
                    {
                        if (u == null)
                        {
                            details = $"List element #{i} is null";
                            return true;
                        }
                    }
                    else if (el == null)
                    {
                        details = $"List element #{i} is null";
                        return true;
                    }
                }
                return false;
            }

            // If it's some other non-unity object and not null, ignore
            return false;
        }

        private class NotNullIssue
        {
            public UnityEngine.Object Asset; // non-scene asset where issue was found (prefab, scriptable object)
            public string AssetPath;
            public string ScenePath; // if scene
            public GameObject SceneObject; // object in scene to select
            public string FieldName;
            public string FieldType;
            public string Details { get; set; }

            public string Summary => $"{(Asset != null ? Asset.name : (SceneObject != null ? SceneObject.name : "(obj)"))}: {FieldName} ({FieldType})";

            public string DetailsVerbose => $"Path: {(AssetPath ?? ScenePath)} | Field: {FieldName} | Info: {Details}";
        }
    }
}
