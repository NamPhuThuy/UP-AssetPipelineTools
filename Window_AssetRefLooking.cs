using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
#endif

namespace NamPhuThuy.AssetPipelineTools
{
#if UNITY_EDITOR
    public class Window_AssetRefLooking : EditorWindow
    {
        #region Private Fields
        [SerializeField] private List<RefLookingEntry> _entries = new List<RefLookingEntry>();

        private bool _isSearching;
        private int _totalReferencesFound;

        // Filter
        private string _filterText = "";
        private AssetTypeFilter _filterTypeMask = AssetTypeFilter.All;

        // Move to folder
        private DefaultAsset _targetFolder;
        private bool _showContextDetails = false;

        // UI Element References
        private VisualElement _targetListContainer;
        private Label _targetCountLabel;
        private VisualElement _resultsContainer;
        private List<(AssetTypeFilter flag, Toggle toggle)> _typeToggles = new List<(AssetTypeFilter, Toggle)>();
        #endregion

        #region Menu Item
        [MenuItem("NamPhuThuy/Assets Pipeline/Window - Asset Ref Looking")]
        public static void ShowWindow()
        {
            var window = GetWindow<Window_AssetRefLooking>("Asset Ref Looking");
            window.minSize = new Vector2(600, 500);
            window.Show();
        }
        #endregion

        #region Initialization
        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;

            // Header
            var header = new Label("Asset Reference Looking")
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 16, unityTextAlign = TextAnchor.MiddleCenter, marginBottom = 10 }
            };
            root.Add(header);

            var helpBox = new HelpBox(
                "• Project Assets: finds all other assets in the project that reference them.\n" +
                "• Hierarchy GameObjects: finds all project assets used by their components (materials, meshes, textures, etc.).\n" +
                "Drag & drop from Project or Hierarchy, or use 'Add Selected' to populate the list.",
                HelpBoxMessageType.Info);
            root.Add(helpBox);

            var mainScroll = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1, marginTop = 10 } };
            root.Add(mainScroll);

            mainScroll.Add(BuildTargetAssetsSection());
            mainScroll.Add(BuildFilterSection());

            var findBtn = new Button(FindAllReferences)
            {
                text = "Find All References",
                style = { height = 35, marginTop = 10, marginBottom = 10, unityFontStyleAndWeight = FontStyle.Bold }
            };
            mainScroll.Add(findBtn);

            mainScroll.Add(BuildResultsSection());

            RefreshTargetList();
        }
        #endregion

        #region UI Builders
        private VisualElement BuildBox()
        {
            var box = new VisualElement();
            box.style.borderTopWidth = 1; box.style.borderBottomWidth = 1; box.style.borderLeftWidth = 1; box.style.borderRightWidth = 1;
            box.style.borderTopColor = new Color(0.15f, 0.15f, 0.15f, 1f); box.style.borderBottomColor = new Color(0.15f, 0.15f, 0.15f, 1f);
            box.style.borderLeftColor = new Color(0.15f, 0.15f, 0.15f, 1f); box.style.borderRightColor = new Color(0.15f, 0.15f, 0.15f, 1f);
            box.style.paddingLeft = 5; box.style.paddingRight = 5; box.style.paddingTop = 5; box.style.paddingBottom = 5;
            box.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f, 0.5f);
            box.style.borderTopLeftRadius = 3; box.style.borderTopRightRadius = 3;
            box.style.borderBottomLeftRadius = 3; box.style.borderBottomRightRadius = 3;
            box.style.marginBottom = 5;
            return box;
        }

        private VisualElement BuildTargetAssetsSection()
        {
            var box = BuildBox();

            // Header row
            var headerRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 5 } };
            _targetCountLabel = new Label($"Added Objects ({_entries.Count})") { style = { unityFontStyleAndWeight = FontStyle.Bold } };
            headerRow.Add(_targetCountLabel);
            
            var spacer = new VisualElement { style = { flexGrow = 1 } };
            headerRow.Add(spacer);

            headerRow.Add(new Button(OnAddSelected) { text = "Add Selected", style = { width = 100 } });
            headerRow.Add(new Button(OnClearAllTargets) { text = "Clear All", style = { width = 80 } });
            box.Add(headerRow);

            var scroll = new ScrollView { style = { maxHeight = 150, minHeight = 40, marginBottom = 5 } };
            _targetListContainer = new VisualElement();
            scroll.Add(_targetListContainer);
            box.Add(scroll);

            // Drop Area
            var dropArea = new VisualElement { style = { height = 35, backgroundColor = new Color(0, 0, 0, 0.1f), alignItems = Align.Center, justifyContent = Justify.Center, borderTopWidth = 1, borderBottomWidth = 1, borderLeftWidth = 1, borderRightWidth = 1, borderTopColor = Color.gray, borderBottomColor = Color.gray, borderLeftColor = Color.gray, borderRightColor = Color.gray } };
            dropArea.Add(new Label("Drag & Drop Assets or Hierarchy GameObjects Here") { style = { unityFontStyleAndWeight = FontStyle.Bold } });

            dropArea.RegisterCallback<DragUpdatedEvent>(e => { DragAndDrop.visualMode = DragAndDropVisualMode.Copy; });
            dropArea.RegisterCallback<DragPerformEvent>(e =>
            {
                DragAndDrop.AcceptDrag();
                Undo.RecordObject(this, "Drag and Drop Assets");
                foreach (Object obj in DragAndDrop.objectReferences)
                {
                    if (_entries.Any(en => en.targetAsset == obj)) continue;
                    bool isProjectAsset = AssetDatabase.Contains(obj);
                    bool isSceneGO = !isProjectAsset && obj is GameObject;
                    if (isProjectAsset || isSceneGO)
                    {
                        _entries.Add(new RefLookingEntry { targetAsset = obj, isSceneObject = isSceneGO });
                    }
                }
                RefreshTargetList();
            });
            box.Add(dropArea);

            return box;
        }

        private VisualElement BuildFilterSection()
        {
            var box = BuildBox();

            var row1 = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            row1.Add(new Label("Filter:") { style = { width = 45 } });
            var filterField = new TextField { value = _filterText, style = { flexGrow = 1 } };
            filterField.RegisterValueChangedCallback(e => { _filterText = e.newValue; RefreshResultsUI(); });
            row1.Add(filterField);
            row1.Add(new Button(() => { filterField.value = ""; _filterTypeMask = AssetTypeFilter.All; UpdateTypeToggles(); RefreshResultsUI(); }) { text = "Clear", style = { width = 45 } });
            box.Add(row1);

            var row2 = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, flexWrap = Wrap.Wrap, marginTop = 5 } };
            row2.Add(new Label("Types:") { style = { width = 45 } });
            
            row2.Add(new Button(() => { _filterTypeMask = AssetTypeFilter.All; UpdateTypeToggles(); RefreshResultsUI(); }) { text = "All" });
            row2.Add(new Button(() => { _filterTypeMask = 0; UpdateTypeToggles(); RefreshResultsUI(); }) { text = "None" });

            _typeToggles.Clear();
            foreach (AssetTypeFilter flag in System.Enum.GetValues(typeof(AssetTypeFilter)))
            {
                if (flag == AssetTypeFilter.All || flag == 0) continue;
                var toggle = new Toggle(flag.ToString()) { value = (_filterTypeMask & flag) != 0, style = { marginLeft = 8 } };
                toggle.RegisterValueChangedCallback(e =>
                {
                    if (e.newValue) _filterTypeMask |= flag;
                    else _filterTypeMask &= ~flag;
                    RefreshResultsUI();
                });
                _typeToggles.Add((flag, toggle));
                row2.Add(toggle);
            }
            box.Add(row2);

            return box;
        }

        private VisualElement BuildResultsSection()
        {
            var box = BuildBox();

            var headerRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 5 } };
            headerRow.Add(new Label("Results") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
            headerRow.Add(new VisualElement { style = { flexGrow = 1 } });
            
            headerRow.Add(new Label("Target Folder:") { style = { width = 80, unityTextAlign = TextAnchor.MiddleRight, marginRight = 5 } });
            var folderField = new ObjectField { objectType = typeof(DefaultAsset), allowSceneObjects = false, value = _targetFolder, style = { width = 200 } };
            folderField.RegisterValueChangedCallback(e => { _targetFolder = e.newValue as DefaultAsset; RefreshResultsUI(); });
            headerRow.Add(folderField);

            var moveBtn = new Button(OnMoveResultsClicked) { name = "moveBtn", text = "Move (0) to Folder", style = { width = 150 } };
            headerRow.Add(moveBtn);
            box.Add(headerRow);

            var optionsRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 5 } };
            var ctxToggle = new Toggle("Show Context Details (Scene Objects Only)") { value = _showContextDetails };
            ctxToggle.RegisterValueChangedCallback(e => { _showContextDetails = e.newValue; RefreshResultsUI(); });
            optionsRow.Add(ctxToggle);
            box.Add(optionsRow);

            var scroll = new ScrollView { style = { flexGrow = 1, minHeight = 250 } };
            _resultsContainer = new VisualElement();
            scroll.Add(_resultsContainer);
            box.Add(scroll);

            return box;
        }

        #endregion

        #region UI Updaters
        private void RefreshTargetList()
        {
            if (_targetListContainer == null) return;
            _targetListContainer.Clear();
            _targetCountLabel.text = $"Added Objects ({_entries.Count})";

            for (int i = 0; i < _entries.Count; i++)
            {
                int index = i;
                var entry = _entries[index];
                
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 2 } };
                row.Add(new Button(() => { Undo.RecordObject(this, "Remove Entry"); _entries.RemoveAt(index); RefreshTargetList(); }) { text = "✕", style = { width = 20 } });

                var objField = new ObjectField { objectType = typeof(Object), value = entry.targetAsset, allowSceneObjects = true, style = { flexGrow = 1 } };
                objField.RegisterValueChangedCallback(e =>
                {
                    Undo.RecordObject(this, "Change Asset");
                    entry.targetAsset = e.newValue;
                    if (entry.targetAsset != null)
                    {
                        entry.isSceneObject = !AssetDatabase.Contains(entry.targetAsset) && entry.targetAsset is GameObject;
                        entry.referencePaths.Clear();
                        entry.referenceContexts.Clear();
                    }
                    RefreshTargetList();
                });
                row.Add(objField);

                if (entry.targetAsset != null)
                {
                    string badge = entry.isSceneObject ? "[Scene]" : "[Asset]";
                    row.Add(new Label(badge) { style = { width = 45, fontSize = 10, color = Color.gray, unityTextAlign = TextAnchor.MiddleCenter } });
                }

                if (entry.referencePaths.Count > 0)
                {
                    row.Add(new Label($"({entry.referencePaths.Count} refs)") { style = { width = 60, fontSize = 10, color = Color.gray } });
                }

                _targetListContainer.Add(row);
            }
        }

        private void UpdateTypeToggles()
        {
            foreach (var t in _typeToggles)
            {
                t.toggle.SetValueWithoutNotify((_filterTypeMask & t.flag) != 0);
            }
        }

        private void RefreshResultsUI()
        {
            if (_resultsContainer == null) return;
            _resultsContainer.Clear();

            var allFilteredPaths = new List<string>();
            foreach (var entry in _entries)
            {
                if (entry.targetAsset == null || entry.referencePaths.Count == 0) continue;
                allFilteredPaths.AddRange(GetFilteredPaths(entry.referencePaths));
            }
            allFilteredPaths = allFilteredPaths.Distinct().ToList();

            var root = rootVisualElement;
            var moveBtn = root.Q<Button>("moveBtn");
            if (moveBtn != null)
            {
                moveBtn.text = $"Move ({allFilteredPaths.Count}) to Folder";
                moveBtn.SetEnabled(_targetFolder != null && allFilteredPaths.Count > 0 && AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(_targetFolder)));
            }

            foreach (var entry in _entries)
            {
                if (entry.targetAsset == null || entry.referencePaths.Count == 0) continue;

                var filteredPaths = GetFilteredPaths(entry.referencePaths);
                
                string assetName = entry.targetAsset.name;
                string assetTypeName = entry.targetAsset.GetType().Name;
                string direction = entry.isSceneObject ? "uses" : "referenced by";

                var foldout = new Foldout
                {
                    text = $"{assetName} ({assetTypeName}) — {direction} {entry.referencePaths.Count} asset(s)",
                    value = entry.foldout
                };
                foldout.RegisterValueChangedCallback(e => entry.foldout = e.newValue);

                if (filteredPaths.Count == 0)
                {
                    foldout.Add(new Label("(No results match the current filter)") { style = { color = Color.gray, marginLeft = 15 } });
                }

                foreach (string refPath in filteredPaths)
                {
                    var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginLeft = 15, marginBottom = 2 } };

                    Object refAsset = AssetDatabase.LoadMainAssetAtPath(refPath);
                    if (refAsset != null)
                    {
                        Texture icon = AssetDatabase.GetCachedIcon(refPath);
                        var iconEl = new VisualElement { style = { width = 16, height = 16, backgroundImage = icon as Texture2D, marginRight = 5 } };
                        iconEl.RegisterCallback<MouseDownEvent>(e => { EditorGUIUtility.PingObject(refAsset); });
                        row.Add(iconEl);

                        var field = new ObjectField { objectType = typeof(Object), value = refAsset, style = { flexGrow = 1 } };
                        field.SetEnabled(false);
                        row.Add(field);

                        row.Add(new Button(() => { Selection.activeObject = refAsset; EditorGUIUtility.PingObject(refAsset); }) { text = "Select", style = { width = 50 } });
                    }
                    else
                    {
                        row.Add(new Label(refPath) { style = { color = Color.gray, flexGrow = 1 } });
                    }

                    foldout.Add(row);

                    if (_showContextDetails && entry.isSceneObject)
                    {
                        var contexts = entry.referenceContexts
                            .Where(c => c.assetPath == refPath)
                            .Select(c => c.contextInfo)
                            .Distinct()
                            .ToList();

                        if (contexts.Count > 0)
                        {
                            var ctxContainer = new VisualElement { style = { marginLeft = 35, borderLeftWidth = 2, borderLeftColor = new Color(0.5f,0.5f,0.5f,0.5f), paddingLeft = 5, marginBottom = 5 } };
                            foreach (var ctx in contexts)
                            {
                                ctxContainer.Add(new Label($"↳ {ctx}") { style = { color = new Color(0.7f, 0.7f, 0.7f), fontSize = 10 } });
                            }
                            foldout.Add(ctxContainer);
                        }
                    }
                }

                _resultsContainer.Add(foldout);
            }
        }
        #endregion

        #region Actions
        private void OnAddSelected()
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
            RefreshTargetList();
        }

        private void OnClearAllTargets()
        {
            Undo.RecordObject(this, "Clear All");
            _entries.Clear();
            RefreshTargetList();
            RefreshResultsUI();
        }

        private void FindAllReferences()
        {
            _isSearching = true;
            _totalReferencesFound = 0;

            var validEntries = _entries.Where(e => e.targetAsset != null).ToList();
            if (validEntries.Count == 0)
            {
                _isSearching = false;
                return;
            }

            foreach (var entry in validEntries)
            {
                entry.referencePaths.Clear();
                entry.referenceContexts.Clear();
            }

            var projectEntries = validEntries.Where(e => !e.isSceneObject).ToList();
            var sceneEntries = validEntries.Where(e => e.isSceneObject).ToList();

            try
            {
                if (projectEntries.Count > 0)
                    FindProjectAssetReferences(projectEntries);

                if (sceneEntries.Count > 0)
                    FindSceneObjectUsedAssets(sceneEntries);

                foreach (var entry in validEntries)
                {
                    entry.foldout = entry.referencePaths.Count > 0;
                    _totalReferencesFound += entry.referencePaths.Count;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _isSearching = false;
                RefreshTargetList();
                RefreshResultsUI();
            }
        }
        #endregion

        #region Core Search Logic
        private void FindProjectAssetReferences(List<RefLookingEntry> projectEntries)
        {
            var targetPaths = projectEntries.Select(e => AssetDatabase.GetAssetPath(e.targetAsset)).ToList();
            var targetGUIDs = targetPaths.Select(p => AssetDatabase.AssetPathToGUID(p)).ToList();

            string[] allAssetPaths = AssetDatabase.GetAllAssetPaths();
            List<string> searchablePaths = allAssetPaths.Where(p => 
                p.StartsWith("Assets/") && !AssetDatabase.IsValidFolder(p)).ToList();

            for (int i = 0; i < searchablePaths.Count; i++)
            {
                string path = searchablePaths[i];

                if (i % 50 == 0)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Scanning Project Assets", 
                        $"Scanning: {Path.GetFileName(path)}", (float)i / searchablePaths.Count))
                    {
                        break;
                    }
                }

                string[] deps = AssetDatabase.GetDependencies(path, false);

                for (int e = 0; e < projectEntries.Count; e++)
                {
                    string tPath = targetPaths[e];
                    if (path == tPath) continue;

                    if (deps.Contains(tPath))
                    {
                        projectEntries[e].referencePaths.Add(path);
                    }
                }
            }
        }

        private void FindSceneObjectUsedAssets(List<RefLookingEntry> sceneEntries)
        {
            for (int e = 0; e < sceneEntries.Count; e++)
            {
                var entry = sceneEntries[e];
                GameObject go = entry.targetAsset as GameObject;
                if (go == null) continue;

                EditorUtility.DisplayProgressBar("Scanning Scene Objects", 
                    $"Scanning: {go.name}", (float)e / sceneEntries.Count);

                var usedAssetPaths = new HashSet<string>();
                entry.referenceContexts.Clear();

                Component[] components = go.GetComponentsInChildren<Component>(true);

                foreach (var component in components)
                {
                    if (component == null) continue;

                    MonoScript script = FindScriptForComponent(component);
                    if (script != null)
                    {
                        string scriptPath = AssetDatabase.GetAssetPath(script);
                        if (!string.IsNullOrEmpty(scriptPath) && scriptPath.StartsWith("Assets/"))
                        {
                            usedAssetPaths.Add(scriptPath);
                            entry.referenceContexts.Add(new ReferenceContext { assetPath = scriptPath, contextInfo = $"Script: {component.GetType().Name}" });
                        }
                    }

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

                    var so = new SerializedObject(component);
                    var prop = so.GetIterator();

                    while (prop.NextVisible(true))
                    {
                        if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;
                        if (prop.objectReferenceValue == null) continue;

                        Object refObj = prop.objectReferenceValue;
                        if (!AssetDatabase.Contains(refObj)) continue;

                        string refPath = AssetDatabase.GetAssetPath(refObj);
                        if (!string.IsNullOrEmpty(refPath) && refPath.StartsWith("Assets/"))
                        {
                            usedAssetPaths.Add(refPath);
                            entry.referenceContexts.Add(new ReferenceContext { assetPath = refPath, contextInfo = $"{component.GetType().Name} → {prop.name}" });
                        }
                    }
                }

                var matPaths = usedAssetPaths.Where(p => p.EndsWith(".mat")).ToList();
                foreach (string matPath in matPaths)
                {
                    Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    if (mat == null) continue;

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

        private MonoScript FindScriptForComponent(Component component)
        {
            if (component is MonoBehaviour mb)
                return MonoScript.FromMonoBehaviour(mb);

            var scripts = Resources.FindObjectsOfTypeAll<MonoScript>();
            var componentType = component.GetType();

            foreach (var s in scripts)
            {
                if (s.GetClass() == componentType)
                    return s;
            }
            return null;
        }

        private void OnMoveResultsClicked()
        {
            var allFilteredPaths = new List<string>();
            foreach (var entry in _entries)
            {
                if (entry.targetAsset == null || entry.referencePaths.Count == 0) continue;
                allFilteredPaths.AddRange(GetFilteredPaths(entry.referencePaths));
            }
            allFilteredPaths = allFilteredPaths.Distinct().ToList();

            string targetFolderPath = AssetDatabase.GetAssetPath(_targetFolder);

            if (!AssetDatabase.IsValidFolder(targetFolderPath))
            {
                EditorUtility.DisplayDialog("Invalid Folder", $"\"{targetFolderPath}\" is not a valid folder.", "OK");
                return;
            }

            var assetsToMove = allFilteredPaths.Where(p => !p.StartsWith(targetFolderPath + "/")).Distinct().ToList();

            if (assetsToMove.Count == 0)
            {
                EditorUtility.DisplayDialog("Nothing to Move", "All result assets are already in the target folder.", "OK");
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog("Move Assets", $"Move {assetsToMove.Count} asset(s) to:\n{targetFolderPath}\n\nThis operation can be undone.", "Move", "Cancel");
            if (!confirmed) return;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Move Results to Folder");
            int undoGroup = Undo.GetCurrentGroup();

            int movedCount = 0;
            int failedCount = 0;
            var pathMapping = new Dictionary<string, string>();

            try
            {
                for (int i = 0; i < assetsToMove.Count; i++)
                {
                    string sourcePath = assetsToMove[i];
                    string fileName = Path.GetFileName(sourcePath);
                    string destPath = targetFolderPath + "/" + fileName;

                    EditorUtility.DisplayProgressBar("Moving Assets", $"Moving {fileName}... ({i + 1}/{assetsToMove.Count})", (float)i / assetsToMove.Count);

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
            if (failedCount > 0) message += $" ({failedCount} failed — see Console for details.)";
            Debug.Log(message);
            EditorUtility.DisplayDialog("Move Complete", message, "OK");

            RefreshResultsUI();
        }

        #endregion

        #region Helpers
        private List<string> GetFilteredPaths(List<string> paths)
        {
            IEnumerable<string> result = paths;

            if (_filterTypeMask != AssetTypeFilter.All && _filterTypeMask != 0)
                result = result.Where(p => MatchesTypeMask(p, _filterTypeMask));
            else if (_filterTypeMask == 0)
                return new List<string>();

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
        #endregion
    }
    
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
#endif
}
