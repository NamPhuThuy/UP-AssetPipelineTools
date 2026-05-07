using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NamPhuThuy.AssetPipelineTools
{
#if UNITY_EDITOR
    [System.Flags]
    public enum AssetTypeFilter
    {
        Prefab          = 1 << 0,
        Scene           = 1 << 1,
        Material        = 1 << 2,
        ScriptableObject = 1 << 3,
        Script          = 1 << 4,
        Shader          = 1 << 5,
        Texture         = 1 << 6,
        Model3D         = 1 << 7,
        Animation       = 1 << 8,

        All = Prefab | Scene | Material | ScriptableObject | Script | Shader | Texture | Model3D | Animation
    }

    [System.Serializable]
    public class ReferenceContext
    {
        public string assetPath;
        public string contextInfo;
    }

    [System.Serializable]
    public class RefLookingEntry
    {
        public Object targetAsset;
        public bool isSceneObject;
        public bool foldout = true;
        public List<string> referencePaths = new List<string>();
        public List<ReferenceContext> referenceContexts = new List<ReferenceContext>();
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
        private AssetTypeFilter _filterTypeMask = AssetTypeFilter.All;
        private bool _showContextDetails = false;

        // Move to folder
        private DefaultAsset _targetFolder;
        #endregion

        #region Menu Item
        [MenuItem("NamPhuThuy/Assets Pipeline/Window - Asset Ref Looking")]
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
                    _entries[i].referenceContexts.Clear();
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
            EditorGUILayout.BeginVertical(GUI.skin.box);

            // Row 1: Text filter
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Filter:", EditorStyles.miniLabel, GUILayout.Width(38));
            _filterText = EditorGUILayout.TextField(_filterText);
            if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(45)))
            {
                _filterText = "";
                _filterTypeMask = AssetTypeFilter.All;
            }
            EditorGUILayout.EndHorizontal();

            // Row 2: Type toggle buttons
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Types:", EditorStyles.miniLabel, GUILayout.Width(38));

            // All / None shortcuts
            if (GUILayout.Button("All", EditorStyles.miniButton, GUILayout.Width(30)))
                _filterTypeMask = AssetTypeFilter.All;
            if (GUILayout.Button("None", EditorStyles.miniButton, GUILayout.Width(38)))
                _filterTypeMask = 0;

            GUILayout.Space(5);

            float windowWidth = EditorGUIUtility.currentViewWidth - 20; // Accounts for scrollbar & padding
            float currentWidth = 120; // Start with approx width of Label + All + None + Space

            // Individual toggles for each type flag
            foreach (AssetTypeFilter flag in System.Enum.GetValues(typeof(AssetTypeFilter)))
            {
                if (flag == AssetTypeFilter.All || flag == 0) continue;

                string label = flag.ToString();
                Vector2 btnSize = EditorStyles.miniButton.CalcSize(new GUIContent(label));
                float buttonWidth = btnSize.x + 4;

                if (currentWidth + buttonWidth > windowWidth)
                {
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                    
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(42); // Indent to align with other buttons
                    currentWidth = 42;
                }

                bool isOn = (_filterTypeMask & flag) != 0;
                bool newIsOn = GUILayout.Toggle(isOn, label, EditorStyles.miniButton, GUILayout.Width(btnSize.x));
                if (newIsOn != isOn)
                {
                    if (newIsOn)
                        _filterTypeMask |= flag;
                    else
                        _filterTypeMask &= ~flag;
                }

                currentWidth += buttonWidth;
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
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

            // Results header with Move controls
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Results", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            GUILayout.Label("Target Folder:", EditorStyles.miniLabel, GUILayout.Width(78));
            _targetFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                _targetFolder, typeof(DefaultAsset), false, GUILayout.Width(200));

            // Collect all filtered paths to determine count and enable/disable button
            var allFilteredPaths = new List<string>();
            foreach (var entry in _entries)
            {
                if (entry.targetAsset == null || entry.referencePaths.Count == 0) continue;
                allFilteredPaths.AddRange(GetFilteredPaths(entry.referencePaths));
            }
            allFilteredPaths = allFilteredPaths.Distinct().ToList();

            bool canMove = _targetFolder != null && allFilteredPaths.Count > 0
                           && AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(_targetFolder));
            GUI.enabled = canMove;
            if (GUILayout.Button($"Move ({allFilteredPaths.Count}) to Folder", GUILayout.Width(180)))
            {
                MoveResultsToFolder(allFilteredPaths);
            }
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();

            // Options row
            EditorGUILayout.BeginHorizontal();
            _showContextDetails = EditorGUILayout.ToggleLeft("Show Context Details (Scene Objects Only)", _showContextDetails, GUILayout.Width(280));
            EditorGUILayout.EndHorizontal();

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

                    if (_showContextDetails && entry.isSceneObject)
                    {
                        var contexts = entry.referenceContexts
                            .Where(c => c.assetPath == refPath)
                            .Select(c => c.contextInfo)
                            .Distinct()
                            .ToList();

                        if (contexts.Count > 0)
                        {
                            EditorGUI.indentLevel++;
                            foreach (var ctx in contexts)
                            {
                                EditorGUILayout.LabelField($"↳ {ctx}", EditorStyles.miniLabel);
                            }
                            EditorGUI.indentLevel--;
                        }
                    }
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
                entry.referenceContexts.Clear();

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
                        {
                            usedAssetPaths.Add(scriptPath);
                            entry.referenceContexts.Add(new ReferenceContext { assetPath = scriptPath, contextInfo = $"Script: {component.GetType().Name}" });
                        }
                    }

                    // ── Explicit handling for common component types ──
                    // SerializedProperty.NextVisible can miss native array properties
                    // on Renderer/Animator in some Unity versions, so we read them directly.

                    if (component is Renderer renderer)
                    {
                        foreach (var mat in renderer.sharedMaterials)
                        {
                            if (mat == null || !AssetDatabase.Contains(mat)) continue;
                            string matPath = AssetDatabase.GetAssetPath(mat);
                            if (!string.IsNullOrEmpty(matPath) && matPath.StartsWith("Assets/"))
                            {
                                usedAssetPaths.Add(matPath);
                                entry.referenceContexts.Add(new ReferenceContext { assetPath = matPath, contextInfo = $"Renderer ({renderer.GetType().Name}) → Material" });
                            }
                        }
                    }

                    if (component is Animator animator && animator.runtimeAnimatorController != null)
                    {
                        var ctrl = animator.runtimeAnimatorController;
                        if (AssetDatabase.Contains(ctrl))
                        {
                            string ctrlPath = AssetDatabase.GetAssetPath(ctrl);
                            if (!string.IsNullOrEmpty(ctrlPath) && ctrlPath.StartsWith("Assets/"))
                            {
                                usedAssetPaths.Add(ctrlPath);
                                entry.referenceContexts.Add(new ReferenceContext { assetPath = ctrlPath, contextInfo = "Animator → Controller" });
                            }
                        }
                    }

                    // ── Generic walk for all other serialized Object references ──
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
                            entry.referenceContexts.Add(new ReferenceContext { assetPath = refPath, contextInfo = $"{component.GetType().Name} → {prop.name}" });
                        }
                    }
                }

                // ── Deep-scan Materials for their textures ──
                // AssetDatabase.GetDependencies sometimes misses textures in custom shaders, so we explicitly scan materials.
                var materialPaths = usedAssetPaths
                    .Where(p => p.EndsWith(".mat", System.StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (string matPath in materialPaths)
                {
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    if (mat == null) continue;

                    // Method 1: GetTexturePropertyNameIDs
                    int[] texPropIds = mat.GetTexturePropertyNameIDs();
                    foreach (int propId in texPropIds)
                    {
                        string propName = mat.GetTexturePropertyNames()[System.Array.IndexOf(texPropIds, propId)];
                        Texture tex = mat.GetTexture(propId);
                        if (tex != null)
                        {
                            string texPath = AssetDatabase.GetAssetPath(tex);
                            if (!string.IsNullOrEmpty(texPath) && texPath.StartsWith("Assets/"))
                            {
                                usedAssetPaths.Add(texPath);
                                entry.referenceContexts.Add(new ReferenceContext { assetPath = texPath, contextInfo = $"Material ({mat.name}) → Texture ({propName})" });
                            }
                        }
                    }

                    // Method 2: SerializedObject walk on the Material itself
                    var matSO = new SerializedObject(mat);
                    var matProp = matSO.GetIterator();
                    while (matProp.NextVisible(true))
                    {
                        if (matProp.propertyType == SerializedPropertyType.ObjectReference && matProp.objectReferenceValue != null)
                        {
                            string matRefPath = AssetDatabase.GetAssetPath(matProp.objectReferenceValue);
                            if (!string.IsNullOrEmpty(matRefPath) && matRefPath.StartsWith("Assets/"))
                            {
                                usedAssetPaths.Add(matRefPath);
                                entry.referenceContexts.Add(new ReferenceContext { assetPath = matRefPath, contextInfo = $"Material ({mat.name}) → {matProp.name}" });
                            }
                        }
                    }
                }

                // ── Deep-scan: resolve dependencies for all discovered assets ──
                // This covers AnimatorControllers→Clips, Prefabs→Meshes, ScriptableObjects, etc.
                var discoveredPaths = usedAssetPaths.ToList();
                foreach (string assetPath in discoveredPaths)
                {
                    string assetName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
                    string[] deps = AssetDatabase.GetDependencies(assetPath, true);
                    foreach (string dep in deps)
                    {
                        if (dep != assetPath && dep.StartsWith("Assets/"))
                        {
                            usedAssetPaths.Add(dep);
                            entry.referenceContexts.Add(new ReferenceContext { assetPath = dep, contextInfo = $"Dependency of [{assetName}]" });
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
        /// <summary>
        /// Moves all filtered result assets to the target folder.
        /// Updates the stored reference paths so the results panel stays in sync.
        /// </summary>
        private void MoveResultsToFolder(List<string> pathsToMove)
        {
            string targetFolderPath = AssetDatabase.GetAssetPath(_targetFolder);

            if (!AssetDatabase.IsValidFolder(targetFolderPath))
            {
                EditorUtility.DisplayDialog("Invalid Folder",
                    $"\"{targetFolderPath}\" is not a valid folder.", "OK");
                return;
            }

            // Filter out assets already in the target folder
            var assetsToMove = pathsToMove
                .Where(p => !p.StartsWith(targetFolderPath + "/"))
                .Distinct()
                .ToList();

            if (assetsToMove.Count == 0)
            {
                EditorUtility.DisplayDialog("Nothing to Move",
                    "All result assets are already in the target folder.", "OK");
                return;
            }

            // Confirmation dialog
            bool confirmed = EditorUtility.DisplayDialog(
                "Move Assets",
                $"Move {assetsToMove.Count} asset(s) to:\n{targetFolderPath}\n\nThis operation can be undone.",
                "Move", "Cancel");

            if (!confirmed) return;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Move Results to Folder");
            int undoGroup = Undo.GetCurrentGroup();

            int movedCount = 0;
            int failedCount = 0;
            var pathMapping = new Dictionary<string, string>(); // oldPath → newPath

            try
            {
                for (int i = 0; i < assetsToMove.Count; i++)
                {
                    string sourcePath = assetsToMove[i];
                    string fileName = Path.GetFileName(sourcePath);
                    string destPath = targetFolderPath + "/" + fileName;

                    EditorUtility.DisplayProgressBar(
                        "Moving Assets",
                        $"Moving {fileName}... ({i + 1}/{assetsToMove.Count})",
                        (float)i / assetsToMove.Count);

                    // Handle name collision: append (1), (2), etc.
                    if (sourcePath != destPath && AssetDatabase.LoadMainAssetAtPath(destPath) != null)
                    {
                        string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                        string ext = Path.GetExtension(fileName);
                        int suffix = 1;
                        do
                        {
                            destPath = $"{targetFolderPath}/{nameWithoutExt} ({suffix}){ext}";
                            suffix++;
                        } while (AssetDatabase.LoadMainAssetAtPath(destPath) != null);
                    }

                    string error = AssetDatabase.MoveAsset(sourcePath, destPath);

                    if (string.IsNullOrEmpty(error))
                    {
                        movedCount++;
                        pathMapping[sourcePath] = destPath;
                    }
                    else
                    {
                        failedCount++;
                        Debug.LogWarning($"Failed to move {sourcePath} → {destPath}: {error}");
                    }
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Undo.CollapseUndoOperations(undoGroup);

            // Update stored paths in all entries so the results panel stays accurate
            foreach (var entry in _entries)
            {
                for (int i = 0; i < entry.referencePaths.Count; i++)
                {
                    if (pathMapping.TryGetValue(entry.referencePaths[i], out string newPath))
                    {
                        entry.referencePaths[i] = newPath;
                    }
                }
            }

            string message = $"Move complete! Moved {movedCount} asset(s) to {targetFolderPath}.";
            if (failedCount > 0)
                message += $" ({failedCount} failed — see Console for details.)";

            Debug.Log(message);
            EditorUtility.DisplayDialog("Move Complete", message, "OK");

            Repaint();
        }
        #endregion

        #region Helpers
        private List<string> GetFilteredPaths(List<string> paths)
        {
            IEnumerable<string> result = paths;

            // Filter by type (if not All)
            if (_filterTypeMask != AssetTypeFilter.All && _filterTypeMask != 0)
            {
                result = result.Where(p => MatchesTypeMask(p, _filterTypeMask));
            }
            else if (_filterTypeMask == 0)
            {
                // None selected → show nothing
                return new List<string>();
            }

            // Filter by text
            if (!string.IsNullOrEmpty(_filterText))
            {
                string lower = _filterText.ToLowerInvariant();
                result = result.Where(p => p.ToLowerInvariant().Contains(lower));
            }

            return result.ToList();
        }

        private bool MatchesTypeMask(string assetPath, AssetTypeFilter mask)
        {
            string lower = assetPath.ToLowerInvariant();

            if ((mask & AssetTypeFilter.Prefab) != 0 && lower.EndsWith(".prefab")) return true;
            if ((mask & AssetTypeFilter.Scene) != 0 && lower.EndsWith(".unity")) return true;
            if ((mask & AssetTypeFilter.Material) != 0 && lower.EndsWith(".mat")) return true;
            if ((mask & AssetTypeFilter.ScriptableObject) != 0 && lower.EndsWith(".asset")) return true;
            if ((mask & AssetTypeFilter.Script) != 0 && lower.EndsWith(".cs")) return true;
            if ((mask & AssetTypeFilter.Shader) != 0 && (lower.EndsWith(".shader") || lower.EndsWith(".shadergraph") || lower.EndsWith(".hlsl"))) return true;
            if ((mask & AssetTypeFilter.Texture) != 0 && (lower.EndsWith(".png") || lower.EndsWith(".jpg") || lower.EndsWith(".jpeg") ||
                                                          lower.EndsWith(".tga") || lower.EndsWith(".psd") || lower.EndsWith(".exr") ||
                                                          lower.EndsWith(".hdr"))) return true;
            if ((mask & AssetTypeFilter.Model3D) != 0 && (lower.EndsWith(".fbx") || lower.EndsWith(".obj") || lower.EndsWith(".blend") ||
                                                          lower.EndsWith(".gltf") || lower.EndsWith(".glb") || lower.EndsWith(".dae") ||
                                                          lower.EndsWith(".3ds") || lower.EndsWith(".max"))) return true;
            if ((mask & AssetTypeFilter.Animation) != 0 && (lower.EndsWith(".anim") || lower.EndsWith(".controller") || lower.EndsWith(".overridecontroller"))) return true;

            return false;
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
