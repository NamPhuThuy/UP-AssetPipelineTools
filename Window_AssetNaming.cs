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
    public class NamePart
    {
        public string value = "";
        public int valueIndex = 0;
        
        public string connectChar = "_";
        public int connectIndex = 1; // Default to "_"

        public NamePart Clone()
        {
            return new NamePart {
                value = this.value,
                valueIndex = this.valueIndex,
                connectChar = this.connectChar,
                connectIndex = this.connectIndex
            };
        }
    }

    [System.Serializable]
    public class NamingRule
    {
        public List<NamePart> prefixes = new List<NamePart>();
        
        public int mainNameIndex = 1; // Default to Original Name
        public string mainName = "";
        
        public int mainConnectIndex = 1; // Default to "_"
        public string mainConnectChar = "_";
        
        public List<NamePart> suffixes = new List<NamePart>();

        public NamingRule Clone()
        {
            var clone = new NamingRule();
            clone.prefixes = this.prefixes.Select(p => p.Clone()).ToList();
            clone.suffixes = this.suffixes.Select(s => s.Clone()).ToList();
            clone.mainNameIndex = this.mainNameIndex;
            clone.mainName = this.mainName;
            clone.mainConnectIndex = this.mainConnectIndex;
            clone.mainConnectChar = this.mainConnectChar;
            return clone;
        }
    }

    [System.Serializable]
    public class RenameRecord
    {
        public Object targetAsset;
        public NamingRule rule = new NamingRule();
    }

    [System.Serializable]
    public class RenameHistoryEntry
    {
        public string assetGuid;
        public string oldName;
        public string newName;
    }

    [System.Serializable]
    public class RenameHistoryBatch
    {
        public string operationName;
        public List<RenameHistoryEntry> entries = new List<RenameHistoryEntry>();
    }

    public class Window_AssetNaming : EditorWindow
    {
        #region Private Fields
        private Vector2 _scrollPos;
        private Vector2 _filesScrollPos;
        private GUIStyle _centeredButtonStyle;
        private GUIStyle _centeredLabelStyle;

        [SerializeField] private NamingRule _globalRule = new NamingRule();
        [SerializeField] private List<RenameRecord> _records = new List<RenameRecord>();

        // Self-managed undo/redo stacks for file renames (Unity's Undo cannot reverse AssetDatabase.RenameAsset)
        [System.NonSerialized] private List<RenameHistoryBatch> _undoStack = new List<RenameHistoryBatch>();
        [System.NonSerialized] private List<RenameHistoryBatch> _redoStack = new List<RenameHistoryBatch>();

        // Replace connect-character state
        private int _replaceFromIndex = 1; // Default to "_"
        private string _replaceFromCustom = "";
        private int _replaceToIndex = 2;   // Default to "-"
        private string _replaceToCustom = "";

        // Clear substring state
        private string _clearSubstring = "";
        private int _clearCount = 0;       // 0 = All occurrences
        private bool _clearFromRight = false; // false = left-to-right, true = right-to-left

        // Separated into categories using '/' for IMGUI popup submenus
        private readonly string[] _partOptions = { 
            "", 
            "URP", "BIRP", "SRP", 
            "red", "green", "magenta", "cyan", 
            "Custom" 
        };
        private readonly string[] _partDisplay = { 
            "<Empty>", 
            "Pipeline/URP", "Pipeline/BIRP", "Pipeline/SRP", 
            "Color/red", "Color/green", "Color/magenta", "Color/cyan", "Color/yellow", "Color/blue",
            "Manual Entry" 
        };

        private readonly string[] _mainNameOptions = { "", "Original Name", "Custom" };
        private readonly string[] _mainNameDisplay = { "<Empty>", "Original Name", "Manual Entry" };

        private readonly string[] _connectOptions = { "", "_", "-", ".", " ", "Custom" };
        private readonly string[] _connectDisplay = { "<Empty>", "_ (underscore)", "- (dash)", ". (dot)", "  (space)", "Manual Entry" };
        #endregion

        #region Menu Item
        [MenuItem("NamPhuThuy/Assets Pipeline/Window - Asset Naming")]
        public static void ShowWindow()
        {
            var window = GetWindow<Window_AssetNaming>("Asset Naming");
            window.minSize = new Vector2(900, 600);
            window.Show();
        }
        #endregion

        #region Unity Callbacks
        private void OnEnable()
        {
            if (_globalRule.prefixes.Count == 0 && _globalRule.suffixes.Count == 0)
            {
                _globalRule.prefixes.Add(new NamePart { valueIndex = 0, connectIndex = 1 });
                _globalRule.suffixes.Add(new NamePart { valueIndex = 0, connectIndex = 1 });
            }
        }

        private void OnDisable()
        {
            // Cleanup when window closes
        }

        private void OnGUI()
        {
            InitializeStyles();

            float padding = 20f;
            Rect areaRect = new Rect(padding, padding, position.width - 2 * padding, position.height - 2 * padding);

            GUILayout.BeginArea(areaRect);

            // Main scroll view that wraps everything
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            DrawHeader();
            GUILayout.Space(10);
            DrawContent();
            GUILayout.Space(10);
            DrawButtons();

            EditorGUILayout.EndScrollView();

            GUILayout.EndArea();
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
            if (_centeredLabelStyle == null)
            {
                _centeredLabelStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 16
                };
            }
        }

        private void DrawHeader()
        {
            GUILayout.Label("Batch Asset Renaming Tool", _centeredLabelStyle);
            EditorGUILayout.HelpBox("Set a Global Template here, or edit each file individually below.\nCategories (Pipeline/Color) are sorted in the dropdown menus.", MessageType.Info);
        }

        private void DrawContent()
        {
            EditorGUI.BeginChangeCheck();

            // === GLOBAL NAMING TEMPLATE SECTION ===
            DrawGlobalTemplateSection();

            GUILayout.Space(10);

            // === TARGET ASSETS SECTION ===
            DrawTargetAssetsSection();

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(this);
            }
        }

        private void DrawButtons()
        {
            EditorGUILayout.BeginHorizontal();

            GUI.enabled = _records.Count > 0 && _records.Any(r => r.targetAsset != null);
            if (GUILayout.Button("Rename All Assets", _centeredButtonStyle, GUILayout.Height(40)))
            {
                RenameAll();
            }
            GUI.enabled = true;

            GUILayout.Space(10);

            GUI.enabled = _undoStack.Count > 0;
            if (GUILayout.Button("↩ Undo", GUILayout.Height(40), GUILayout.Width(80)))
            {
                UndoLastRename();
            }
            GUI.enabled = true;

            GUILayout.Space(4);

            GUI.enabled = _redoStack.Count > 0;
            if (GUILayout.Button("Redo ↪", GUILayout.Height(40), GUILayout.Width(80)))
            {
                RedoLastRename();
            }
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
        }
        #endregion

        #region Private Methods
        private void DrawGlobalTemplateSection()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Global Naming Template", EditorStyles.boldLabel);
            GUILayout.Space(5);
            
            EditorGUILayout.BeginHorizontal();
            DrawRuleEditor(_globalRule);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Apply Template to All Below", GUILayout.Width(250), GUILayout.Height(30)))
            {
                Undo.RecordObject(this, "Apply Global Rule");
                foreach (var record in _records)
                {
                    record.rule = _globalRule.Clone();
                }
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawTargetAssetsSection()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"Target Assets ({_records.Count})", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Add Selected", GUILayout.Width(100)))
            {
                Undo.RecordObject(this, "Add Selected Assets");
                foreach (var obj in Selection.objects)
                {
                    if (AssetDatabase.Contains(obj) && !_records.Any(r => r.targetAsset == obj))
                    {
                        var record = new RenameRecord { targetAsset = obj, rule = _globalRule.Clone() };
                        _records.Add(record);
                    }
                }
            }
            if (GUILayout.Button("Clear Whitespace", GUILayout.Width(120)))
            {
                ClearWhitespaceAll();
            }
            if (GUILayout.Button("Clear All Assets", GUILayout.Width(120)))
            {
                Undo.RecordObject(this, "Clear Assets");
                _records.Clear();
            }
            EditorGUILayout.EndHorizontal();

            // Clear substring row
            GUILayout.Space(3);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Remove", EditorStyles.miniLabel, GUILayout.Width(48));
            _clearSubstring = EditorGUILayout.TextField(_clearSubstring, GUILayout.Width(150));
            GUILayout.Label("Count", EditorStyles.miniLabel, GUILayout.Width(38));
            _clearCount = EditorGUILayout.IntField(_clearCount, GUILayout.Width(30));
            GUILayout.Label(_clearCount == 0 ? "(All)" : "", EditorStyles.miniLabel, GUILayout.Width(28));
            _clearFromRight = GUILayout.Toggle(_clearFromRight, _clearFromRight ? "← R-to-L" : "L-to-R →", EditorStyles.miniButton, GUILayout.Width(60));
            GUI.enabled = !string.IsNullOrEmpty(_clearSubstring) && _records.Count > 0;
            if (GUILayout.Button("Clear Substring", GUILayout.Width(120)))
            {
                ClearSubstringAll();
            }
            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // Replace connect-character row
            GUILayout.Space(3);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Replace", EditorStyles.miniLabel, GUILayout.Width(48));

            _replaceFromIndex = EditorGUILayout.Popup(_replaceFromIndex, _connectDisplay, GUILayout.Width(100));
            if (_replaceFromIndex == _connectOptions.Length - 1)
                _replaceFromCustom = EditorGUILayout.TextField(_replaceFromCustom, GUILayout.Width(40));

            GUILayout.Label("→", EditorStyles.miniLabel, GUILayout.Width(16));

            _replaceToIndex = EditorGUILayout.Popup(_replaceToIndex, _connectDisplay, GUILayout.Width(100));
            if (_replaceToIndex == _connectOptions.Length - 1)
                _replaceToCustom = EditorGUILayout.TextField(_replaceToCustom, GUILayout.Width(40));

            if (GUILayout.Button("Replace Connect Char", GUILayout.Width(150)))
            {
                ReplaceConnectCharAll();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // Change case row
            GUILayout.Space(3);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Case", EditorStyles.miniLabel, GUILayout.Width(48));
            GUI.enabled = _records.Count > 0 && _records.Any(r => r.targetAsset != null);
            if (GUILayout.Button("UPPER", EditorStyles.miniButton, GUILayout.Width(55)))
                ChangeCaseAll(0);
            if (GUILayout.Button("lower", EditorStyles.miniButton, GUILayout.Width(55)))
                ChangeCaseAll(1);
            if (GUILayout.Button("Title Case", EditorStyles.miniButton, GUILayout.Width(70)))
                ChangeCaseAll(2);
            if (GUILayout.Button("camelCase", EditorStyles.miniButton, GUILayout.Width(70)))
                ChangeCaseAll(3);
            if (GUILayout.Button("PascalCase", EditorStyles.miniButton, GUILayout.Width(75)))
                ChangeCaseAll(4);
            if (GUILayout.Button("snake_case", EditorStyles.miniButton, GUILayout.Width(75)))
                ChangeCaseAll(5);
            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(5);

            _filesScrollPos = EditorGUILayout.BeginScrollView(_filesScrollPos, GUILayout.Height(250));
            for (int i = 0; i < _records.Count; i++)
            {
                DrawRecordInline(i);
            }
            EditorGUILayout.EndScrollView();

            // Drop area for adding files
            GUILayout.Space(5);
            Rect dropRect = GUILayoutUtility.GetRect(0, 40, GUILayout.ExpandWidth(true));
            GUI.Box(dropRect, "Drag & Drop Assets Here", _centeredButtonStyle);
            HandleDragAndDrop(dropRect);

            EditorGUILayout.EndVertical();
        }

        private void DrawRuleEditor(NamingRule rule)
        {
            // Prefixes
            EditorGUILayout.BeginVertical(GUILayout.Width(250));
            if (GUILayout.Button("+ Add Prefix", GUILayout.Height(25)))
            {
                Undo.RecordObject(this, "Add Prefix");
                rule.prefixes.Add(new NamePart { connectIndex = 1, connectChar = "_" });
            }
            for (int i = 0; i < rule.prefixes.Count; i++)
            {
                DrawNamePartFull(rule.prefixes[i], true, () => {
                    Undo.RecordObject(this, "Remove Prefix");
                    rule.prefixes.RemoveAt(i);
                    GUIUtility.ExitGUI();
                });
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);

            // Main Name
            EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(220));
            GUILayout.Label("Main Name", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            
            rule.mainNameIndex = EditorGUILayout.Popup(rule.mainNameIndex, _mainNameDisplay, GUILayout.Width(100));
            if (rule.mainNameIndex == _mainNameOptions.Length - 1)
            {
                rule.mainName = EditorGUILayout.TextField(rule.mainName, GUILayout.Width(100));
            }
            else if (rule.mainNameIndex == 1)
            {
                GUI.enabled = false;
                EditorGUILayout.TextField("(Original Name)", GUILayout.Width(100));
                GUI.enabled = true;
            }
            else
            {
                rule.mainName = _mainNameOptions[rule.mainNameIndex];
            }
            
            rule.mainConnectIndex = EditorGUILayout.Popup(rule.mainConnectIndex, _connectDisplay, GUILayout.Width(60));
            if (rule.mainConnectIndex == _connectOptions.Length - 1)
                rule.mainConnectChar = EditorGUILayout.TextField(rule.mainConnectChar, GUILayout.Width(40));
            else
                rule.mainConnectChar = _connectOptions[rule.mainConnectIndex];
            
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);

            // Suffixes
            EditorGUILayout.BeginVertical(GUILayout.Width(250));
            if (GUILayout.Button("+ Add Suffix", GUILayout.Height(25)))
            {
                Undo.RecordObject(this, "Add Suffix");
                rule.suffixes.Add(new NamePart { connectIndex = 1, connectChar = "_" });
            }
            for (int i = 0; i < rule.suffixes.Count; i++)
            {
                DrawNamePartFull(rule.suffixes[i], false, () => {
                    Undo.RecordObject(this, "Remove Suffix");
                    rule.suffixes.RemoveAt(i);
                    GUIUtility.ExitGUI();
                });
            }
            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();
        }

        private void DrawNamePartFull(NamePart part, bool isPrefix, System.Action onRemove)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();
            
            GUILayout.Label(isPrefix ? "Prefix" : "Suffix", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("x", EditorStyles.miniButton, GUILayout.Width(20)))
            {
                onRemove?.Invoke();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            // Value
            part.valueIndex = EditorGUILayout.Popup(part.valueIndex, _partDisplay, GUILayout.Width(100));
            if (part.valueIndex == _partOptions.Length - 1)
                part.value = EditorGUILayout.TextField(part.value, GUILayout.Width(100));
            else
                part.value = _partOptions[part.valueIndex];

            // Connect Char
            part.connectIndex = EditorGUILayout.Popup(part.connectIndex, _connectDisplay, GUILayout.Width(60));
            if (part.connectIndex == _connectOptions.Length - 1)
                part.connectChar = EditorGUILayout.TextField(part.connectChar, GUILayout.Width(40));
            else
                part.connectChar = _connectOptions[part.connectIndex];

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        // Inline drawing for individual records
        private void DrawRecordInline(int index)
        {
            var record = _records[index];
            var rule = record.rule;

            EditorGUILayout.BeginHorizontal(GUI.skin.box);
            
            // 1. Remove Button
            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                Undo.RecordObject(this, "Remove Record");
                _records.RemoveAt(index);
                GUIUtility.ExitGUI();
            }

            // 2. Object Field
            record.targetAsset = EditorGUILayout.ObjectField(record.targetAsset, typeof(Object), false, GUILayout.Width(150));

            // 3. Inline Rule Editing
            GUILayout.Space(10);
            
            // Prefixes
            for (int i = 0; i < rule.prefixes.Count; i++)
            {
                DrawNamePartInline(rule.prefixes[i], () => {
                    Undo.RecordObject(this, "Remove Prefix");
                    rule.prefixes.RemoveAt(i);
                    GUIUtility.ExitGUI();
                });
            }
            if (GUILayout.Button("+ Pre", EditorStyles.miniButton, GUILayout.Width(40)))
            {
                Undo.RecordObject(this, "Add Prefix");
                rule.prefixes.Add(new NamePart { connectIndex = 1, connectChar = "_" });
            }

            GUILayout.Space(5);
            
            // Main Name
            EditorGUILayout.BeginHorizontal(GUI.skin.box);
            rule.mainNameIndex = EditorGUILayout.Popup(rule.mainNameIndex, _mainNameDisplay, GUILayout.Width(80));
            if (rule.mainNameIndex == _mainNameOptions.Length - 1)
                rule.mainName = EditorGUILayout.TextField(rule.mainName, GUILayout.Width(60));
            else if (rule.mainNameIndex == 1 && record.targetAsset != null)
            {
                GUI.enabled = false;
                EditorGUILayout.TextField(GetAssetFileName(record.targetAsset), GUILayout.Width(80));
                GUI.enabled = true;
            }
            else
                rule.mainName = _mainNameOptions[rule.mainNameIndex];

            rule.mainConnectIndex = EditorGUILayout.Popup(rule.mainConnectIndex, _connectDisplay, GUILayout.Width(40));
            if (rule.mainConnectIndex == _connectOptions.Length - 1)
                rule.mainConnectChar = EditorGUILayout.TextField(rule.mainConnectChar, GUILayout.Width(20));
            else
                rule.mainConnectChar = _connectOptions[rule.mainConnectIndex];
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(5);

            // Suffixes
            for (int i = 0; i < rule.suffixes.Count; i++)
            {
                DrawNamePartInline(rule.suffixes[i], () => {
                    Undo.RecordObject(this, "Remove Suffix");
                    rule.suffixes.RemoveAt(i);
                    GUIUtility.ExitGUI();
                });
            }
            if (GUILayout.Button("+ Suf", EditorStyles.miniButton, GUILayout.Width(40)))
            {
                Undo.RecordObject(this, "Add Suffix");
                rule.suffixes.Add(new NamePart { connectIndex = 1, connectChar = "_" });
            }

            GUILayout.FlexibleSpace();

            // Preview
            string preview = GetPreviewName(rule, record.targetAsset);
            GUILayout.Label(preview, EditorStyles.boldLabel);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawNamePartInline(NamePart part, System.Action onRemove)
        {
            EditorGUILayout.BeginHorizontal(GUI.skin.box);
            
            part.valueIndex = EditorGUILayout.Popup(part.valueIndex, _partDisplay, GUILayout.Width(80));
            if (part.valueIndex == _partOptions.Length - 1)
                part.value = EditorGUILayout.TextField(part.value, GUILayout.Width(60));
            else
                part.value = _partOptions[part.valueIndex];

            part.connectIndex = EditorGUILayout.Popup(part.connectIndex, _connectDisplay, GUILayout.Width(40));
            if (part.connectIndex == _connectOptions.Length - 1)
                part.connectChar = EditorGUILayout.TextField(part.connectChar, GUILayout.Width(20));
            else
                part.connectChar = _connectOptions[part.connectIndex];

            if (GUILayout.Button("x", EditorStyles.miniButton, GUILayout.Width(15)))
                onRemove?.Invoke();

            EditorGUILayout.EndHorizontal();
        }

        private string GetPreviewName(NamingRule rule, Object asset)
        {
            List<(string value, string conn)> validParts = new List<(string value, string conn)>();

            // Collect Prefixes
            foreach (var p in rule.prefixes)
            {
                if (!string.IsNullOrEmpty(p.value)) validParts.Add((p.value, p.connectChar));
            }
            
            // Collect Main Name
            string main = rule.mainName;
            if (rule.mainNameIndex == 1 && asset != null)
            {
                main = GetAssetFileName(asset);
            }
            else if (rule.mainNameIndex == 1 && asset == null)
            {
                main = "(Original Name)";
            }

            if (!string.IsNullOrEmpty(main)) validParts.Add((main, rule.mainConnectChar));

            // Collect Suffixes
            foreach (var s in rule.suffixes)
            {
                if (!string.IsNullOrEmpty(s.value)) validParts.Add((s.value, s.connectChar));
            }

            // Join them
            string result = "";
            for (int i = 0; i < validParts.Count; i++)
            {
                result += validParts[i].value;
                if (i < validParts.Count - 1) // don't add connect char after the very last element
                {
                    result += validParts[i].conn;
                }
            }

            return result;
        }

        /// <summary>
        /// Returns the actual file name (without extension) from the asset's path on disk.
        /// Unlike Object.name, this is immune to assets that override their name
        /// (e.g. Shaders use the internal Shader "path/name" declaration).
        /// </summary>
        private string GetAssetFileName(Object asset)
        {
            string assetPath = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(assetPath))
                return asset.name; // fallback for non-persistent assets
            return System.IO.Path.GetFileNameWithoutExtension(assetPath);
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
                            if (AssetDatabase.Contains(obj) && !_records.Any(r => r.targetAsset == obj))
                            {
                                _records.Add(new RenameRecord { targetAsset = obj, rule = _globalRule.Clone() });
                            }
                        }
                        currentEvent.Use();
                    }
                }
            }
        }
        /// <summary>
        /// Performs a batch rename and pushes to the undo stack. Returns the count of renamed assets.
        /// </summary>
        private int PerformBatchRename(string operationName, List<(Object asset, string newName)> renamePairs)
        {
            if (renamePairs.Count == 0) return 0;

            var batch = new RenameHistoryBatch { operationName = operationName };
            int successCount = 0;

            foreach (var (asset, newName) in renamePairs)
            {
                string assetPath = AssetDatabase.GetAssetPath(asset);
                if (string.IsNullOrEmpty(assetPath)) continue;

                string oldName = GetAssetFileName(asset);
                string guid = AssetDatabase.AssetPathToGUID(assetPath);

                string result = AssetDatabase.RenameAsset(assetPath, newName);

                if (string.IsNullOrEmpty(result))
                {
                    batch.entries.Add(new RenameHistoryEntry
                    {
                        assetGuid = guid,
                        oldName = oldName,
                        newName = newName
                    });
                    successCount++;
                }
                else
                {
                    Debug.LogWarning($"[{operationName}] Failed to rename {assetPath}: {result}");
                }
            }

            if (batch.entries.Count > 0)
            {
                _undoStack.Add(batch);
                _redoStack.Clear(); // new action invalidates redo
            }

            AssetDatabase.SaveAssets();
            return successCount;
        }

        private void UndoLastRename()
        {
            if (_undoStack.Count == 0)
            {
                Debug.LogWarning("Nothing to undo.");
                return;
            }

            var batch = _undoStack[_undoStack.Count - 1];
            _undoStack.RemoveAt(_undoStack.Count - 1);

            // Reverse in reverse order
            for (int i = batch.entries.Count - 1; i >= 0; i--)
            {
                var entry = batch.entries[i];
                string path = AssetDatabase.GUIDToAssetPath(entry.assetGuid);
                if (string.IsNullOrEmpty(path)) continue;
                AssetDatabase.RenameAsset(path, entry.oldName);
            }

            _redoStack.Add(batch);
            AssetDatabase.SaveAssets();
            Debug.Log($"Undo: Reverted '{batch.operationName}' ({batch.entries.Count} asset(s))");
            Repaint();
        }

        private void RedoLastRename()
        {
            if (_redoStack.Count == 0)
            {
                Debug.LogWarning("Nothing to redo.");
                return;
            }

            var batch = _redoStack[_redoStack.Count - 1];
            _redoStack.RemoveAt(_redoStack.Count - 1);

            foreach (var entry in batch.entries)
            {
                string path = AssetDatabase.GUIDToAssetPath(entry.assetGuid);
                if (string.IsNullOrEmpty(path)) continue;
                AssetDatabase.RenameAsset(path, entry.newName);
            }

            _undoStack.Add(batch);
            AssetDatabase.SaveAssets();
            Debug.Log($"Redo: Re-applied '{batch.operationName}' ({batch.entries.Count} asset(s))");
            Repaint();
        }

        private void ReplaceConnectCharAll()
        {
            string fromChar = _replaceFromIndex == _connectOptions.Length - 1
                ? _replaceFromCustom
                : _connectOptions[_replaceFromIndex];
            string toChar = _replaceToIndex == _connectOptions.Length - 1
                ? _replaceToCustom
                : _connectOptions[_replaceToIndex];

            if (string.IsNullOrEmpty(fromChar))
            {
                Debug.LogWarning("Replace Connect Char: 'From' character is empty. Nothing to replace.");
                return;
            }
            if (fromChar == toChar)
            {
                Debug.LogWarning("Replace Connect Char: 'From' and 'To' characters are the same. Nothing to do.");
                return;
            }

            var validRecords = _records.Where(r => r.targetAsset != null).ToList();
            if (validRecords.Count == 0) return;

            var renamePairs = new List<(Object asset, string newName)>();
            foreach (var record in validRecords)
            {
                string originalName = GetAssetFileName(record.targetAsset);
                if (!originalName.Contains(fromChar)) continue;
                renamePairs.Add((record.targetAsset, originalName.Replace(fromChar, toChar ?? "")));
            }

            int count = PerformBatchRename("Asset Naming - Replace Connect Char", renamePairs);
            Debug.Log($"Replace Connect Char Complete! Updated {count} asset name(s). ('{fromChar}' → '{toChar}')");
        }

        private void ClearWhitespaceAll()
        {
            var validRecords = _records.Where(r => r.targetAsset != null).ToList();
            if (validRecords.Count == 0) return;

            var renamePairs = new List<(Object asset, string newName)>();
            foreach (var record in validRecords)
            {
                string originalName = GetAssetFileName(record.targetAsset);
                string cleanedName = System.Text.RegularExpressions.Regex.Replace(originalName, @"\s+", "");
                if (originalName != cleanedName)
                    renamePairs.Add((record.targetAsset, cleanedName));
            }

            int count = PerformBatchRename("Asset Naming - Clear Whitespace", renamePairs);
            Debug.Log($"Clear Whitespace Complete! Cleaned {count} asset name(s).");
        }

        private void ClearSubstringAll()
        {
            if (string.IsNullOrEmpty(_clearSubstring))
            {
                Debug.LogWarning("Clear Substring: Nothing to clear — the field is empty.");
                return;
            }

            var validRecords = _records.Where(r => r.targetAsset != null).ToList();
            if (validRecords.Count == 0) return;

            int removeCount = Mathf.Max(0, _clearCount); // 0 = all

            var renamePairs = new List<(Object asset, string newName)>();
            foreach (var record in validRecords)
            {
                string originalName = GetAssetFileName(record.targetAsset);
                if (!originalName.Contains(_clearSubstring)) continue;

                string cleanedName = RemoveSubstringOccurrences(originalName, _clearSubstring, removeCount, _clearFromRight);
                if (originalName == cleanedName) continue;
                if (string.IsNullOrEmpty(cleanedName))
                {
                    Debug.LogWarning($"Skipping {originalName}: removing '{_clearSubstring}' would leave an empty name.");
                    continue;
                }
                renamePairs.Add((record.targetAsset, cleanedName));
            }

            int count = PerformBatchRename("Asset Naming - Clear Substring", renamePairs);
            string dirLabel = _clearFromRight ? "R-to-L" : "L-to-R";
            string countLabel = removeCount == 0 ? "all" : removeCount.ToString();
            Debug.Log($"Clear Substring Complete! Removed '{_clearSubstring}' ({countLabel}, {dirLabel}) from {count} asset name(s).");
        }

        /// <summary>
        /// Changes the case of all asset names.
        /// mode: 0=UPPER, 1=lower, 2=Title Case, 3=camelCase, 4=PascalCase, 5=snake_case
        /// </summary>
        private void ChangeCaseAll(int mode)
        {
            var validRecords = _records.Where(r => r.targetAsset != null).ToList();
            if (validRecords.Count == 0) return;

            string[] modeNames = { "UPPERCASE", "lowercase", "Title Case", "camelCase", "PascalCase", "snake_case" };

            var renamePairs = new List<(Object asset, string newName)>();
            foreach (var record in validRecords)
            {
                string originalName = GetAssetFileName(record.targetAsset);
                string newName;
                switch (mode)
                {
                    case 0: newName = originalName.ToUpperInvariant(); break;
                    case 1: newName = originalName.ToLowerInvariant(); break;
                    case 2: newName = ToTitleCase(originalName); break;
                    case 3: newName = ToCamelCase(originalName); break;
                    case 4: newName = ToPascalCase(originalName); break;
                    case 5: newName = ToSnakeCase(originalName); break;
                    default: continue;
                }
                if (originalName != newName)
                    renamePairs.Add((record.targetAsset, newName));
            }

            int renamed = PerformBatchRename($"Asset Naming - {modeNames[mode]}", renamePairs);
            Debug.Log($"Change Case Complete! Converted {renamed} asset name(s) to {modeNames[mode]}.");
        }

        /// <summary>
        /// Splits a name into word segments by common separators and casing boundaries.
        /// </summary>
        private static List<string> SplitIntoWords(string input)
        {
            var words = new List<string>();
            if (string.IsNullOrEmpty(input)) return words;

            // First split by separators
            char[] separators = { '_', '-', '.', ' ' };
            var rawParts = input.Split(separators, System.StringSplitOptions.RemoveEmptyEntries);

            // Then split each part by casing boundaries (e.g. "camelCase" -> "camel", "Case")
            foreach (var part in rawParts)
            {
                int start = 0;
                for (int i = 1; i < part.Length; i++)
                {
                    if (char.IsUpper(part[i]) && !char.IsUpper(part[i - 1]))
                    {
                        words.Add(part.Substring(start, i - start));
                        start = i;
                    }
                    else if (char.IsUpper(part[i]) && i + 1 < part.Length && char.IsUpper(part[i - 1]) && !char.IsUpper(part[i + 1]))
                    {
                        // Handle "HTMLParser" -> "HTML", "Parser"
                        words.Add(part.Substring(start, i - start));
                        start = i;
                    }
                }
                words.Add(part.Substring(start));
            }

            return words;
        }

        /// <summary>
        /// Title Case: capitalizes the first letter of each segment, preserving original separators.
        /// </summary>
        private static string ToTitleCase(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            char[] separators = { '_', '-', '.', ' ' };
            var chars = input.ToCharArray();
            bool capitalizeNext = true;

            for (int i = 0; i < chars.Length; i++)
            {
                if (System.Array.IndexOf(separators, chars[i]) >= 0)
                {
                    capitalizeNext = true;
                }
                else if (capitalizeNext)
                {
                    chars[i] = char.ToUpperInvariant(chars[i]);
                    capitalizeNext = false;
                }
                else
                {
                    chars[i] = char.ToLowerInvariant(chars[i]);
                }
            }

            return new string(chars);
        }

        /// <summary>
        /// camelCase: first word lowercase, subsequent words capitalized, no separators.
        /// </summary>
        private static string ToCamelCase(string input)
        {
            var words = SplitIntoWords(input);
            if (words.Count == 0) return input;

            for (int i = 0; i < words.Count; i++)
            {
                if (i == 0)
                    words[i] = words[i].ToLowerInvariant();
                else
                    words[i] = char.ToUpperInvariant(words[i][0]) + words[i].Substring(1).ToLowerInvariant();
            }
            return string.Concat(words);
        }

        /// <summary>
        /// PascalCase: every word capitalized, no separators.
        /// </summary>
        private static string ToPascalCase(string input)
        {
            var words = SplitIntoWords(input);
            if (words.Count == 0) return input;

            for (int i = 0; i < words.Count; i++)
            {
                words[i] = char.ToUpperInvariant(words[i][0]) + words[i].Substring(1).ToLowerInvariant();
            }
            return string.Concat(words);
        }

        /// <summary>
        /// snake_case: all lowercase, words joined by underscores.
        /// </summary>
        private static string ToSnakeCase(string input)
        {
            var words = SplitIntoWords(input);
            if (words.Count == 0) return input;

            for (int i = 0; i < words.Count; i++)
            {
                words[i] = words[i].ToLowerInvariant();
            }
            return string.Join("_", words);
        }

        /// <summary>
        /// Removes up to 'count' occurrences of 'sub' from 'source'.
        /// count=0 removes all. fromRight=true removes the last N occurrences instead of the first N.
        /// </summary>
        private static string RemoveSubstringOccurrences(string source, string sub, int count, bool fromRight)
        {
            if (string.IsNullOrEmpty(sub)) return source;

            // Find all occurrence indices
            var indices = new List<int>();
            int searchStart = 0;
            while (searchStart <= source.Length - sub.Length)
            {
                int idx = source.IndexOf(sub, searchStart, System.StringComparison.Ordinal);
                if (idx < 0) break;
                indices.Add(idx);
                searchStart = idx + sub.Length; // non-overlapping
            }

            if (indices.Count == 0) return source;

            // Select which occurrences to remove
            List<int> toRemove;
            if (count == 0 || count >= indices.Count)
            {
                toRemove = indices; // remove all
            }
            else if (fromRight)
            {
                toRemove = indices.Skip(indices.Count - count).ToList();
            }
            else
            {
                toRemove = indices.Take(count).ToList();
            }

            // Remove in reverse order to preserve earlier indices
            var result = source;
            for (int i = toRemove.Count - 1; i >= 0; i--)
            {
                result = result.Remove(toRemove[i], sub.Length);
            }

            return result;
        }

        private void RenameAll()
        {
            int skippedCount = 0;

            var renamePairs = new List<(Object asset, string newName)>();
            foreach (var record in _records)
            {
                if (record.targetAsset == null) continue;

                string newName = GetPreviewName(record.rule, record.targetAsset);
                if (string.IsNullOrEmpty(newName) || GetAssetFileName(record.targetAsset) == newName)
                {
                    skippedCount++;
                    continue;
                }
                renamePairs.Add((record.targetAsset, newName));
            }

            int renamedCount = PerformBatchRename("Asset Naming - Rename All", renamePairs);

            if (renamedCount == 0 && skippedCount > 0)
            {
                Debug.Log($"Batch Renaming: No assets renamed. {skippedCount} asset(s) skipped (name unchanged or empty).");
            }
            else
            {
                Debug.Log($"Batch Renaming Complete! Renamed {renamedCount} asset(s), skipped {skippedCount}.");
            }
        }
        #endregion
    }
#endif
}
