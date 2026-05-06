using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NamPhuThuy.AssetPipelineTools
{
#if UNITY_EDITOR
    [System.Serializable]
    public class RefLookingEntry
    {
        public Object targetAsset;
        public bool isSceneObject;
        public bool foldout = true;
        public List<string> referencePaths = new List<string>();
    }

    public class Window_AssetRefLooking : EditorWindow
    {
        #region Private Fields
        private Vector2 _scrollPos;
        private Vector2 _resultsScrollPos;
        private GUIStyle _centeredButtonStyle;
        private GUIStyle _headerStyle;

        [SerializeField] private List<RefLookingEntry> _entries = new List<RefLookingEntry>();

        private bool _isSearching;
        private int _totalReferencesFound;

        // Filter
        private string _filterText = "";
        private readonly string[] _filterTypeOptions = { "All", "Prefab", "Scene", "Material", "ScriptableObject", "Script", "Shader", "Texture", "AnimationClip" };
        private int _filterTypeIndex;
        #endregion

        #region Menu Item
        [MenuItem("NamPhuThuy/Assets Pipeline/Asset Ref Looking")]
        public static void ShowWindow()
        {
            var window = GetWindow<Window_AssetRefLooking>("Asset Ref Looking");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }
        #endregion

        #region Unity Callbacks
        private void OnGUI()
        {
            InitializeStyles();

            GUILayout.Space(10);
            GUILayout.Label("Asset Reference Looking", _headerStyle);
            EditorGUILayout.HelpBox(
                "• Project Assets: finds all other assets in the project that reference them.\n" +
                "• Hierarchy GameObjects: finds all project assets used by their components (materials, meshes, textures, etc.).\n" +
                "Drag & drop from Project or Hierarchy, or use 'Add Selected' to populate the list.",
                MessageType.Info);
            GUILayout.Space(10);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            DrawTargetAssetsSection();
            GUILayout.Space(10);
            DrawFilterSection();
            GUILayout.Space(10);
            DrawActionButtons();
            GUILayout.Space(10);
            DrawResultsSection();

            EditorGUILayout.EndScrollView();

            GUILayout.Space(10);
        }
        #endregion

        #region Initialization
        private void InitializeStyles()
        {
            if (_centeredButtonStyle == null)
            {
                _centeredButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 14,
                    fontStyle = FontStyle.Bold
                };
            }

            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 16
                };
            }
        }
        #endregion

        #region Drawing
        private void DrawTargetAssetsSection()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);

            // Header row with buttons
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"Added Objects ({_entries.Count})", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Add Selected", GUILayout.Width(100)))
            {
                Undo.RecordObject(this, "Add Selected Assets");
                foreach (var obj in Selection.objects)
                {
                    if (_entries.Any(e => e.targetAsset == obj)) continue;

                    bool isProjectAsset = AssetDatabase.Contains(obj);
                    bool isSceneGO = !isProjectAsset && obj is GameObject;

                    if (isProjectAsset || isSceneGO)
                    {
                        _entries.Add(new RefLookingEntry { targetAsset = obj, isSceneObject = isSceneGO });
                    }
                }
            }
            if (GUILayout.Button("Clear All", GUILayout.Width(80)))
            {
                Undo.RecordObject(this, "Clear All Entries");
                _entries.Clear();
                _totalReferencesFound = 0;
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(5);

            // Entry list
            for (int i = 0; i < _entries.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("X", GUILayout.Width(20)))
                {
                    Undo.RecordObject(this, "Remove Entry");
                    _entries.RemoveAt(i);
                    GUIUtility.ExitGUI();
                }

                Object prevObj = _entries[i].targetAsset;
                _entries[i].targetAsset = EditorGUILayout.ObjectField(
                    _entries[i].targetAsset, typeof(Object), true);

                // Update isSceneObject flag when the user changes the object via the picker
                if (_entries[i].targetAsset != prevObj && _entries[i].targetAsset != null)
                {
                    _entries[i].isSceneObject = !AssetDatabase.Contains(_entries[i].targetAsset)
                                                && _entries[i].targetAsset is GameObject;
                    _entries[i].referencePaths.Clear();
                }

                // Badge to indicate source
                if (_entries[i].targetAsset != null)
                {
                    string badge = _entries[i].isSceneObject ? "[Scene]" : "[Asset]";
                    GUILayout.Label(badge, EditorStyles.miniLabel, GUILayout.Width(45));
                }

                if (_entries[i].referencePaths.Count > 0)
                {
                    GUILayout.Label($"({_entries[i].referencePaths.Count} refs)",
                        EditorStyles.miniLabel, GUILayout.Width(70));
                }

                EditorGUILayout.EndHorizontal();
            }

            // Drop area
            GUILayout.Space(5);
            Rect dropRect = GUILayoutUtility.GetRect(0, 35, GUILayout.ExpandWidth(true));
            GUI.Box(dropRect, "Drag & Drop Assets or Hierarchy GameObjects Here", _centeredButtonStyle);
            HandleDragAndDrop(dropRect);

            EditorGUILayout.EndVertical();
        }

        private void DrawFilterSection()
        {
            EditorGUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label("Filter:", EditorStyles.miniLabel, GUILayout.Width(38));
            _filterTypeIndex = EditorGUILayout.Popup(_filterTypeIndex, _filterTypeOptions, GUILayout.Width(130));
            _filterText = EditorGUILayout.TextField(_filterText);
            if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(45)))
            {
                _filterText = "";
                _filterTypeIndex = 0;
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawActionButtons()
        {
            bool hasValidEntries = _entries.Count > 0 && _entries.Any(e => e.targetAsset != null);
            GUI.enabled = hasValidEntries && !_isSearching;

            if (GUILayout.Button("Find All References", _centeredButtonStyle, GUILayout.Height(35)))
            {
                FindAllReferences();
            }

            GUI.enabled = true;

            if (_totalReferencesFound > 0)
            {
                EditorGUILayout.HelpBox(
                    $"Search complete. Found {_totalReferencesFound} total reference(s) across {_entries.Count(e => e.referencePaths.Count > 0)} asset(s).",
                    MessageType.Info);
            }
        }

        private void DrawResultsSection()
        {
            bool hasResults = _entries.Any(e => e.referencePaths.Count > 0);
            if (!hasResults) return;

            EditorGUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Results", EditorStyles.boldLabel);
            GUILayout.Space(5);

            _resultsScrollPos = EditorGUILayout.BeginScrollView(_resultsScrollPos, GUILayout.MinHeight(300));

            foreach (var entry in _entries)
            {
                if (entry.targetAsset == null || entry.referencePaths.Count == 0) continue;

                string assetName = entry.targetAsset.name;
                string assetTypeName = entry.targetAsset.GetType().Name;
                string direction = entry.isSceneObject ? "uses" : "referenced by";

                entry.foldout = EditorGUILayout.Foldout(entry.foldout,
                    $"{assetName} ({assetTypeName}) — {direction} {entry.referencePaths.Count} asset(s)", true, EditorStyles.foldoutHeader);

                if (!entry.foldout) continue;

                EditorGUI.indentLevel++;

                var filteredPaths = GetFilteredPaths(entry.referencePaths);

                if (filteredPaths.Count == 0)
                {
                    EditorGUILayout.LabelField("(No results match the current filter)", EditorStyles.miniLabel);
                }

                foreach (string refPath in filteredPaths)
                {
                    EditorGUILayout.BeginHorizontal();

                    Object refAsset = AssetDatabase.LoadMainAssetAtPath(refPath);
                    if (refAsset != null)
                    {
                        // Clickable icon + object field (read-only)
                        Texture icon = AssetDatabase.GetCachedIcon(refPath);
                        GUIContent iconContent = icon != null
                            ? new GUIContent(icon)
                            : GUIContent.none;

                        if (GUILayout.Button(iconContent, EditorStyles.label, GUILayout.Width(18), GUILayout.Height(18)))
                        {
                            EditorGUIUtility.PingObject(refAsset);
                        }

                        GUI.enabled = false;
                        EditorGUILayout.ObjectField(refAsset, typeof(Object), false);
                        GUI.enabled = true;

                        if (GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(50)))
                        {
                            Selection.activeObject = refAsset;
                            EditorGUIUtility.PingObject(refAsset);
                        }
                    }
                    else
                    {
                        EditorGUILayout.LabelField(refPath, EditorStyles.miniLabel);
                    }

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUI.indentLevel--;
                GUILayout.Space(5);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }
        #endregion

        #region Core Logic
        private void FindAllReferences()
        {
            _isSearching = true;
            _totalReferencesFound = 0;

            try
            {
                // Split entries into two groups
                var projectEntries = _entries.Where(e => e.targetAsset != null && !e.isSceneObject).ToList();
                var sceneEntries = _entries.Where(e => e.targetAsset != null && e.isSceneObject).ToList();

                // Clear all results first
                foreach (var entry in _entries)
                    entry.referencePaths.Clear();

                // === Mode A: Project Assets — find who references them ===
                if (projectEntries.Count > 0)
                {
                    FindProjectAssetReferences(projectEntries);
                }

                // === Mode B: Scene GameObjects — find what assets they use ===
                if (sceneEntries.Count > 0)
                {
                    FindSceneObjectUsedAssets(sceneEntries);
                }

                // Count totals
                foreach (var entry in _entries)
                {
                    _totalReferencesFound += entry.referencePaths.Count;
                    entry.referencePaths.Sort();
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _isSearching = false;
                Repaint();
            }

            Debug.Log($"Asset Ref Looking: Found {_totalReferencesFound} total reference(s) for {_entries.Count(e => e.targetAsset != null)} object(s).");
        }

        /// <summary>
        /// For project assets: reverse-scan all assets in the project to find which ones depend on the targets.
        /// </summary>
        private void FindProjectAssetReferences(List<RefLookingEntry> projectEntries)
        {
            var targetGuids = new Dictionary<string, RefLookingEntry>();
            foreach (var entry in projectEntries)
            {
                string assetPath = AssetDatabase.GetAssetPath(entry.targetAsset);
                string guid = AssetDatabase.AssetPathToGUID(assetPath);

                if (!string.IsNullOrEmpty(guid))
                {
                    targetGuids[guid] = entry;
                }
            }

            if (targetGuids.Count == 0) return;

            string[] allAssetPaths = AssetDatabase.GetAllAssetPaths()
                .Where(p => p.StartsWith("Assets/"))
                .ToArray();

            int totalPaths = allAssetPaths.Length;

            for (int i = 0; i < totalPaths; i++)
            {
                string candidatePath = allAssetPaths[i];

                if (i % 100 == 0)
                {
                    bool cancelled = EditorUtility.DisplayCancelableProgressBar(
                        "Finding References (Project Assets)",
                        $"Scanning {candidatePath}... ({i}/{totalPaths})",
                        (float)i / totalPaths);

                    if (cancelled) break;
                }

                if (AssetDatabase.IsValidFolder(candidatePath)) continue;

                string[] dependencies = AssetDatabase.GetDependencies(candidatePath, false);

                foreach (string depPath in dependencies)
                {
                    string depGuid = AssetDatabase.AssetPathToGUID(depPath);

                    if (targetGuids.TryGetValue(depGuid, out var matchedEntry))
                    {
                        string selfPath = AssetDatabase.GetAssetPath(matchedEntry.targetAsset);
                        if (candidatePath != selfPath && !matchedEntry.referencePaths.Contains(candidatePath))
                        {
                            matchedEntry.referencePaths.Add(candidatePath);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// For scene GameObjects: inspect all components (including children) via SerializedObject
        /// to extract every referenced project asset.
        /// </summary>
        private void FindSceneObjectUsedAssets(List<RefLookingEntry> sceneEntries)
        {
            for (int e = 0; e < sceneEntries.Count; e++)
            {
                var entry = sceneEntries[e];
                var go = entry.targetAsset as GameObject;
                if (go == null) continue;

                EditorUtility.DisplayProgressBar(
                    "Finding Used Assets (Scene Objects)",
                    $"Inspecting {go.name}... ({e + 1}/{sceneEntries.Count})",
                    (float)e / sceneEntries.Count);

                var usedAssetPaths = new HashSet<string>();

                // Get all components on this GameObject and its children
                Component[] components = go.GetComponentsInChildren<Component>(true);

                foreach (var component in components)
                {
                    if (component == null) continue; // missing script

                    // Add the script asset itself
                    MonoScript script = MonoScript.FromMonoBehaviour(component as MonoBehaviour);
                    if (script == null && component is MonoBehaviour mb)
                        script = MonoScript.FromMonoBehaviour(mb);
                    if (script == null)
                        script = FindScriptForComponent(component);

                    if (script != null)
                    {
                        string scriptPath = AssetDatabase.GetAssetPath(script);
                        if (!string.IsNullOrEmpty(scriptPath) && scriptPath.StartsWith("Assets/"))
                            usedAssetPaths.Add(scriptPath);
                    }

                    // Walk all serialized properties to find Object references
                    var so = new SerializedObject(component);
                    var prop = so.GetIterator();

                    while (prop.NextVisible(true))
                    {
                        if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;
                        if (prop.objectReferenceValue == null) continue;

                        Object refObj = prop.objectReferenceValue;

                        // Skip scene objects — we only want project assets
                        if (!AssetDatabase.Contains(refObj)) continue;

                        string refPath = AssetDatabase.GetAssetPath(refObj);
                        if (!string.IsNullOrEmpty(refPath) && refPath.StartsWith("Assets/"))
                        {
                            usedAssetPaths.Add(refPath);
                        }
                    }
                }

                entry.referencePaths = usedAssetPaths.ToList();
            }
        }

        /// <summary>
        /// Attempts to find the MonoScript for a non-MonoBehaviour component (e.g. Renderer, Collider).
        /// Returns null for built-in Unity components (which is expected).
        /// </summary>
        private MonoScript FindScriptForComponent(Component component)
        {
            if (component is MonoBehaviour mb)
                return MonoScript.FromMonoBehaviour(mb);

            // For ScriptableObject-derived components or custom types, try via type
            var scripts = Resources.FindObjectsOfTypeAll<MonoScript>();
            var componentType = component.GetType();

            foreach (var s in scripts)
            {
                if (s.GetClass() == componentType)
                    return s;
            }

            return null;
        }
        #endregion

        #region Helpers
        private List<string> GetFilteredPaths(List<string> paths)
        {
            IEnumerable<string> result = paths;

            // Filter by type
            if (_filterTypeIndex > 0)
            {
                string typeFilter = _filterTypeOptions[_filterTypeIndex];
                result = result.Where(p => MatchesTypeFilter(p, typeFilter));
            }

            // Filter by text
            if (!string.IsNullOrEmpty(_filterText))
            {
                string lower = _filterText.ToLowerInvariant();
                result = result.Where(p => p.ToLowerInvariant().Contains(lower));
            }

            return result.ToList();
        }

        private bool MatchesTypeFilter(string assetPath, string typeFilter)
        {
            switch (typeFilter)
            {
                case "Prefab":            return assetPath.EndsWith(".prefab");
                case "Scene":             return assetPath.EndsWith(".unity");
                case "Material":          return assetPath.EndsWith(".mat");
                case "ScriptableObject":  return assetPath.EndsWith(".asset");
                case "Script":            return assetPath.EndsWith(".cs");
                case "Shader":            return assetPath.EndsWith(".shader") || assetPath.EndsWith(".shadergraph");
                case "Texture":           return assetPath.EndsWith(".png") || assetPath.EndsWith(".jpg") ||
                                                 assetPath.EndsWith(".tga") || assetPath.EndsWith(".psd") ||
                                                 assetPath.EndsWith(".exr");
                case "AnimationClip":     return assetPath.EndsWith(".anim") || assetPath.EndsWith(".controller");
                default:                  return true;
            }
        }

        private void HandleDragAndDrop(Rect dropRect)
        {
            Event currentEvent = Event.current;
            EventType currentEventType = currentEvent.type;

            if (dropRect.Contains(currentEvent.mousePosition))
            {
                if (currentEventType == EventType.DragUpdated || currentEventType == EventType.DragPerform)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                    if (currentEventType == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        Undo.RecordObject(this, "Drag and Drop Assets");

                        foreach (Object obj in DragAndDrop.objectReferences)
                        {
                            if (_entries.Any(e => e.targetAsset == obj)) continue;

                            bool isProjectAsset = AssetDatabase.Contains(obj);
                            bool isSceneGO = !isProjectAsset && obj is GameObject;

                            if (isProjectAsset || isSceneGO)
                            {
                                _entries.Add(new RefLookingEntry { targetAsset = obj, isSceneObject = isSceneGO });
                            }
                        }
                        currentEvent.Use();
                    }
                }
            }
        }
        #endregion
    }
#endif
}
