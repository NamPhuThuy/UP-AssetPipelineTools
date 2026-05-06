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

    public class Window_AssetNaming : EditorWindow
    {
        private Vector2 _scrollPos;
        private Vector2 _filesScrollPos;
        private GUIStyle _centeredButtonStyle;
        private GUIStyle _headerStyle;

        [SerializeField] private NamingRule _globalRule = new NamingRule();
        [SerializeField] private List<RenameRecord> _records = new List<RenameRecord>();

        // Replace connect-character state
        private int _replaceFromIndex = 1; // Default to "_"
        private string _replaceFromCustom = "";
        private int _replaceToIndex = 2;   // Default to "-"
        private string _replaceToCustom = "";

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

        [MenuItem("NamPhuThuy/Assets Pipeline/Asset Naming")]
        public static void ShowWindow()
        {
            var window = GetWindow<Window_AssetNaming>("Asset Naming");
            window.minSize = new Vector2(900, 600);
            window.Show();
        }

        private void OnEnable()
        {
            if (_globalRule.prefixes.Count == 0 && _globalRule.suffixes.Count == 0)
            {
                _globalRule.prefixes.Add(new NamePart { valueIndex = 0, connectIndex = 1 });
                _globalRule.suffixes.Add(new NamePart { valueIndex = 0, connectIndex = 1 });
            }
        }

        private void OnGUI()
        {
            InitializeStyles();

            GUILayout.Space(10);
            GUILayout.Label("Batch Asset Renaming Tool", _headerStyle);
            EditorGUILayout.HelpBox("Set a Global Template here, or edit each file individually below.\nCategories (Pipeline/Color) are sorted in the dropdown menus.", MessageType.Info);
            GUILayout.Space(10);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            EditorGUI.BeginChangeCheck();

            // === GLOBAL NAMING TEMPLATE SECTION ===
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
            // ==================================

            GUILayout.Space(10);

            // === TARGET ASSETS SECTION ===
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
            if (GUILayout.Button("Clear All", GUILayout.Width(80)))
            {
                Undo.RecordObject(this, "Clear Assets");
                _records.Clear();
            }
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
            // ==================================

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(this);
            }

            EditorGUILayout.EndScrollView();

            GUILayout.Space(10);

            GUI.enabled = _records.Count > 0 && _records.Any(r => r.targetAsset != null);
            if (GUILayout.Button("Rename All Assets", _centeredButtonStyle, GUILayout.Height(40)))
            {
                RenameAll();
            }
            GUI.enabled = true;
            GUILayout.Space(10);
        }

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

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            int replacedCount = 0;

            foreach (var record in validRecords)
            {
                string originalName = record.targetAsset.name;
                if (!originalName.Contains(fromChar)) continue;

                string newName = originalName.Replace(fromChar, toChar ?? "");

                string assetPath = AssetDatabase.GetAssetPath(record.targetAsset);
                if (string.IsNullOrEmpty(assetPath)) continue;

                Undo.RecordObject(record.targetAsset, "Replace Connect Char");
                string result = AssetDatabase.RenameAsset(assetPath, newName);

                if (string.IsNullOrEmpty(result))
                {
                    replacedCount++;
                }
                else
                {
                    Debug.LogWarning($"Failed to replace connect char for {assetPath}: {result}");
                }
            }

            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log($"Replace Connect Char Complete! Updated {replacedCount} asset name(s). ('{fromChar}' → '{toChar}')");
        }

        private void ClearWhitespaceAll()
        {
            var validRecords = _records.Where(r => r.targetAsset != null).ToList();
            if (validRecords.Count == 0) return;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            int cleanedCount = 0;

            foreach (var record in validRecords)
            {
                string originalName = record.targetAsset.name;
                string cleanedName = System.Text.RegularExpressions.Regex.Replace(originalName, @"\s+", "");

                if (originalName == cleanedName) continue;

                string assetPath = AssetDatabase.GetAssetPath(record.targetAsset);
                if (string.IsNullOrEmpty(assetPath)) continue;

                Undo.RecordObject(record.targetAsset, "Clear Whitespace");
                string result = AssetDatabase.RenameAsset(assetPath, cleanedName);

                if (string.IsNullOrEmpty(result))
                {
                    cleanedCount++;
                }
                else
                {
                    Debug.LogWarning($"Failed to clear whitespace for {assetPath}: {result}");
                }
            }

            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log($"Clear Whitespace Complete! Cleaned {cleanedCount} asset name(s).");
        }

        private void RenameAll()
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            int renamedCount = 0;
            int skippedCount = 0;
                
            
            Debug.Log(message:$"_records count: {_records.Count}");
            foreach (var record in _records)
            {
                if (record.targetAsset == null)
                {
                    Debug.LogError(message:$"record.targetAsset is null");
                    continue;
                }

                string newName = GetPreviewName(record.rule, record.targetAsset);
                if (string.IsNullOrEmpty(newName) || GetAssetFileName(record.targetAsset) == newName)
                {
                    skippedCount++;
                    continue;
                }

                string assetPath = AssetDatabase.GetAssetPath(record.targetAsset);
                if (string.IsNullOrEmpty(assetPath)) continue;

                Undo.RecordObject(record.targetAsset, "Rename Asset");
                string result = AssetDatabase.RenameAsset(assetPath, newName);
                
                if (string.IsNullOrEmpty(result))
                {
                    renamedCount++;
                }
                else
                {
                    Debug.LogWarning($"Failed to rename {assetPath}: {result}");
                }
            }

            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(undoGroup);
            
            if (renamedCount == 0 && skippedCount > 0)
            {
                Debug.Log($"Batch Renaming: No assets renamed. {skippedCount} asset(s) skipped (name unchanged or empty).");
            }
            else
            {
                Debug.Log($"Batch Renaming Complete! Renamed {renamedCount} asset(s), skipped {skippedCount}.");
            }
        }
    }
#endif
}
