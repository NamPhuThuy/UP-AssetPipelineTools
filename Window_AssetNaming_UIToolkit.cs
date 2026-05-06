using System.Collections.Generic;
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
    public class Window_AssetNaming_UIToolkit : EditorWindow
    {
        // ── Persisted State ──────────────────────────────────────────────
        [SerializeField] private NamingRule _globalRule = new NamingRule();
        [SerializeField] private List<RenameRecord> _records = new List<RenameRecord>();

        // Replace connect-char state
        private int _replaceFromIndex = 1;
        private string _replaceFromCustom = "";
        private int _replaceToIndex = 2;
        private string _replaceToCustom = "";

        // ── Static Data ──────────────────────────────────────────────────
        private static readonly string[] PartOptions   = { "", "URP", "BIRP", "SRP", "red", "green", "magenta", "cyan", "Custom" };
        private static readonly string[] PartDisplay   = { "<Empty>", "Pipeline/URP", "Pipeline/BIRP", "Pipeline/SRP", "Color/red", "Color/green", "Color/magenta", "Color/cyan", "Color/yellow", "Color/blue", "Manual Entry" };
        private static readonly string[] MainNameOptions = { "", "Original Name", "Custom" };
        private static readonly string[] MainNameDisplay = { "<Empty>", "Original Name", "Manual Entry" };
        private static readonly string[] ConnectOptions  = { "", "_", "-", ".", " ", "Custom" };
        private static readonly string[] ConnectDisplay  = { "<Empty>", "_ (underscore)", "- (dash)", ". (dot)", "  (space)", "Manual Entry" };

        // ── Live UI Refs ─────────────────────────────────────────────────
        private VisualElement _recordsContainer;
        private Label _recordCountLabel;

        // Replace-char dropdowns
        private PopupField<string> _replaceFromPopup;
        private TextField _replaceFromCustomField;
        private PopupField<string> _replaceToPopup;
        private TextField _replaceToCustomField;

        // ─────────────────────────────────────────────────────────────────
        [MenuItem("NamPhuThuy/Assets Pipeline/Window_UIToolkit - Asset Naming")]
        public static void ShowWindow()
        {
            var window = GetWindow<Window_AssetNaming>("Asset Naming");
            window.minSize = new Vector2(960, 640);
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

        // ── CreateGUI (entry point for UI Toolkit) ───────────────────────
        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.AddToClassList("root");

            // Load stylesheet
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Assets/_Project/Module RPManage/UP-AssetPipelineTools/Window_AssetNaming.uss");
            if (uss != null) root.styleSheets.Add(uss);
            else ApplyInlineStyles(root);

            // ── Header ──
            var header = new Label("Batch Asset Renaming Tool");
            header.AddToClassList("header-label");
            root.Add(header);

            var helpBox = new HelpBox(
                "Set a Global Template here, or edit each file individually below.\n" +
                "Categories (Pipeline/Color) are sorted in the dropdown menus.",
                HelpBoxMessageType.Info);
            root.Add(helpBox);

            // ── Main scroll ──
            var mainScroll = new ScrollView(ScrollViewMode.Vertical);
            mainScroll.style.flexGrow = 1;
            root.Add(mainScroll);

            // ── Global Template Section ──
            mainScroll.Add(BuildGlobalTemplateSection());

            // ── Target Assets Section ──
            mainScroll.Add(BuildTargetAssetsSection());

            // ── Rename All Button ──
            var renameBtn = new Button(OnRenameAll) { text = "Rename All Assets" };
            renameBtn.AddToClassList("rename-all-btn");
            root.Add(renameBtn);

            RefreshRecordList();
        }

        // ═════════════════════════════════════════════════════════════════
        // SECTION BUILDERS
        // ═════════════════════════════════════════════════════════════════

        private VisualElement BuildGlobalTemplateSection()
        {
            var box = new VisualElement();
            box.AddToClassList("section-box");

            var title = new Label("Global Naming Template");
            title.AddToClassList("section-title");
            box.Add(title);

            var ruleRow = new VisualElement();
            ruleRow.AddToClassList("rule-row");
            BuildRuleEditorUI(_globalRule, ruleRow, null, true);
            box.Add(ruleRow);

            var applyBtn = new Button(() =>
            {
                Undo.RecordObject(this, "Apply Global Rule");
                foreach (var r in _records) r.rule = _globalRule.Clone();
                RefreshRecordList();
            })
            { text = "Apply Template to All Below" };
            applyBtn.AddToClassList("apply-btn");
            box.Add(applyBtn);

            return box;
        }

        private VisualElement BuildTargetAssetsSection()
        {
            var box = new VisualElement();
            box.AddToClassList("section-box");

            // ── Header row ──
            var headerRow = new VisualElement();
            headerRow.AddToClassList("toolbar-row");

            _recordCountLabel = new Label($"Target Assets ({_records.Count})");
            _recordCountLabel.AddToClassList("section-title");
            headerRow.Add(_recordCountLabel);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            headerRow.Add(spacer);

            var addSelBtn = new Button(OnAddSelected) { text = "Add Selected" };
            addSelBtn.AddToClassList("toolbar-btn");
            headerRow.Add(addSelBtn);

            var clearWsBtn = new Button(OnClearWhitespace) { text = "Clear Whitespace" };
            clearWsBtn.AddToClassList("toolbar-btn");
            headerRow.Add(clearWsBtn);

            var clearAllBtn = new Button(OnClearAll) { text = "Clear All" };
            clearAllBtn.AddToClassList("toolbar-btn");
            headerRow.Add(clearAllBtn);

            box.Add(headerRow);

            // ── Replace connect-char row ──
            box.Add(BuildReplaceCharRow());

            // ── Records scroll ──
            var recordScroll = new ScrollView(ScrollViewMode.Vertical);
            recordScroll.style.maxHeight = 300;
            recordScroll.style.minHeight = 80;

            _recordsContainer = new VisualElement();
            recordScroll.Add(_recordsContainer);
            box.Add(recordScroll);

            // ── Drop area ──
            var dropArea = new VisualElement();
            dropArea.AddToClassList("drop-area");
            var dropLabel = new Label("Drag & Drop Assets Here");
            dropLabel.AddToClassList("drop-label");
            dropArea.Add(dropLabel);

            dropArea.RegisterCallback<DragUpdatedEvent>(_ =>
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            });
            dropArea.RegisterCallback<DragPerformEvent>(_ =>
            {
                DragAndDrop.AcceptDrag();
                Undo.RecordObject(this, "Drag and Drop Assets");
                foreach (var obj in DragAndDrop.objectReferences)
                {
                    if (AssetDatabase.Contains(obj) && !_records.Any(r => r.targetAsset == obj))
                        _records.Add(new RenameRecord { targetAsset = obj, rule = _globalRule.Clone() });
                }
                RefreshRecordList();
            });

            box.Add(dropArea);
            return box;
        }

        private VisualElement BuildReplaceCharRow()
        {
            var row = new VisualElement();
            row.AddToClassList("replace-row");

            row.Add(new Label("Replace") { style = { unityFontStyleAndWeight = FontStyle.Bold } });

            // From
            _replaceFromPopup = new PopupField<string>(ConnectDisplay.ToList(), _replaceFromIndex);
            _replaceFromPopup.style.width = 110;
            _replaceFromPopup.RegisterValueChangedCallback(evt =>
            {
                _replaceFromIndex = ConnectDisplay.ToList().IndexOf(evt.newValue);
                _replaceFromCustomField.style.display =
                    _replaceFromIndex == ConnectOptions.Length - 1 ? DisplayStyle.Flex : DisplayStyle.None;
            });
            row.Add(_replaceFromPopup);

            _replaceFromCustomField = new TextField { value = _replaceFromCustom };
            _replaceFromCustomField.style.width = 50;
            _replaceFromCustomField.style.display =
                _replaceFromIndex == ConnectOptions.Length - 1 ? DisplayStyle.Flex : DisplayStyle.None;
            _replaceFromCustomField.RegisterValueChangedCallback(e => _replaceFromCustom = e.newValue);
            row.Add(_replaceFromCustomField);

            row.Add(new Label("→"));

            // To
            _replaceToPopup = new PopupField<string>(ConnectDisplay.ToList(), _replaceToIndex);
            _replaceToPopup.style.width = 110;
            _replaceToPopup.RegisterValueChangedCallback(evt =>
            {
                _replaceToIndex = ConnectDisplay.ToList().IndexOf(evt.newValue);
                _replaceToCustomField.style.display =
                    _replaceToIndex == ConnectOptions.Length - 1 ? DisplayStyle.Flex : DisplayStyle.None;
            });
            row.Add(_replaceToPopup);

            _replaceToCustomField = new TextField { value = _replaceToCustom };
            _replaceToCustomField.style.width = 50;
            _replaceToCustomField.style.display =
                _replaceToIndex == ConnectOptions.Length - 1 ? DisplayStyle.Flex : DisplayStyle.None;
            _replaceToCustomField.RegisterValueChangedCallback(e => _replaceToCustom = e.newValue);
            row.Add(_replaceToCustomField);

            var replaceBtn = new Button(OnReplaceConnectChar) { text = "Replace Connect Char" };
            replaceBtn.AddToClassList("toolbar-btn");
            row.Add(replaceBtn);

            return row;
        }

        // ═════════════════════════════════════════════════════════════════
        // RULE EDITOR UI
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Builds prefix | mainName | suffix columns into <paramref name="container"/>.
        /// Pass <paramref name="record"/> = null for the global rule panel.
        /// </summary>
        private void BuildRuleEditorUI(NamingRule rule, VisualElement container,
            RenameRecord record, bool isGlobal)
        {
            container.Clear();

            // ── Prefixes column ──
            var prefixCol = new VisualElement();
            prefixCol.AddToClassList("part-column");

            var addPrefixBtn = new Button(() =>
            {
                Undo.RecordObject(this, "Add Prefix");
                rule.prefixes.Add(new NamePart { connectIndex = 1, connectChar = "_" });
                RebuildRuleEditor(rule, container, record, isGlobal);
            })
            { text = "+ Add Prefix" };
            addPrefixBtn.AddToClassList("add-part-btn");
            prefixCol.Add(addPrefixBtn);

            for (int i = 0; i < rule.prefixes.Count; i++)
            {
                int idx = i;
                prefixCol.Add(BuildNamePartUI(rule.prefixes[idx], () =>
                {
                    Undo.RecordObject(this, "Remove Prefix");
                    rule.prefixes.RemoveAt(idx);
                    RebuildRuleEditor(rule, container, record, isGlobal);
                }, () => RebuildRuleEditor(rule, container, record, isGlobal)));
            }

            container.Add(prefixCol);

            // ── Main Name column ──
            var mainCol = new VisualElement();
            mainCol.AddToClassList("main-name-col");

            var mainLabel = new Label("Main Name");
            mainLabel.AddToClassList("col-label");
            mainCol.Add(mainLabel);

            var mainNameRow = new VisualElement();
            mainNameRow.AddToClassList("part-row");

            var mainNamePopup = new PopupField<string>(MainNameDisplay.ToList(), rule.mainNameIndex);
            mainNamePopup.style.width = 110;

            var mainNameCustomField = new TextField { value = rule.mainName };
            mainNameCustomField.style.width = 110;
            mainNameCustomField.style.display =
                rule.mainNameIndex == MainNameOptions.Length - 1 ? DisplayStyle.Flex : DisplayStyle.None;
            mainNameCustomField.RegisterValueChangedCallback(e => rule.mainName = e.newValue);

            var mainNameReadonly = new TextField();
            mainNameReadonly.SetEnabled(false);
            mainNameReadonly.style.width = 110;
            mainNameReadonly.style.display =
                rule.mainNameIndex == 1 ? DisplayStyle.Flex : DisplayStyle.None;
            mainNameReadonly.value = record?.targetAsset != null
                ? GetAssetFileName(record.targetAsset)
                : "(Original Name)";

            mainNamePopup.RegisterValueChangedCallback(evt =>
            {
                rule.mainNameIndex = MainNameDisplay.ToList().IndexOf(evt.newValue);
                if (rule.mainNameIndex != MainNameOptions.Length - 1)
                    rule.mainName = MainNameOptions[rule.mainNameIndex];

                mainNameCustomField.style.display =
                    rule.mainNameIndex == MainNameOptions.Length - 1 ? DisplayStyle.Flex : DisplayStyle.None;
                mainNameReadonly.style.display =
                    rule.mainNameIndex == 1 ? DisplayStyle.Flex : DisplayStyle.None;

                if (record != null) UpdateRecordPreview(record);
            });
            mainNameRow.Add(mainNamePopup);
            mainNameRow.Add(mainNameCustomField);
            mainNameRow.Add(mainNameReadonly);

            // Connect char for main name
            var connPopup = new PopupField<string>(ConnectDisplay.ToList(), rule.mainConnectIndex);
            connPopup.style.width = 90;
            var connCustom = new TextField { value = rule.mainConnectChar };
            connCustom.style.width = 50;
            connCustom.style.display =
                rule.mainConnectIndex == ConnectOptions.Length - 1 ? DisplayStyle.Flex : DisplayStyle.None;
            connCustom.RegisterValueChangedCallback(e =>
            {
                rule.mainConnectChar = e.newValue;
                if (record != null) UpdateRecordPreview(record);
            });
            connPopup.RegisterValueChangedCallback(evt =>
            {
                rule.mainConnectIndex = ConnectDisplay.ToList().IndexOf(evt.newValue);
                rule.mainConnectChar = rule.mainConnectIndex < ConnectOptions.Length - 1
                    ? ConnectOptions[rule.mainConnectIndex] : rule.mainConnectChar;
                connCustom.style.display =
                    rule.mainConnectIndex == ConnectOptions.Length - 1 ? DisplayStyle.Flex : DisplayStyle.None;
                if (record != null) UpdateRecordPreview(record);
            });
            mainNameRow.Add(connPopup);
            mainNameRow.Add(connCustom);

            mainCol.Add(mainNameRow);
            container.Add(mainCol);

            // ── Suffixes column ──
            var suffixCol = new VisualElement();
            suffixCol.AddToClassList("part-column");

            var addSuffixBtn = new Button(() =>
            {
                Undo.RecordObject(this, "Add Suffix");
                rule.suffixes.Add(new NamePart { connectIndex = 1, connectChar = "_" });
                RebuildRuleEditor(rule, container, record, isGlobal);
            })
            { text = "+ Add Suffix" };
            addSuffixBtn.AddToClassList("add-part-btn");
            suffixCol.Add(addSuffixBtn);

            for (int i = 0; i < rule.suffixes.Count; i++)
            {
                int idx = i;
                suffixCol.Add(BuildNamePartUI(rule.suffixes[idx], () =>
                {
                    Undo.RecordObject(this, "Remove Suffix");
                    rule.suffixes.RemoveAt(idx);
                    RebuildRuleEditor(rule, container, record, isGlobal);
                }, () => RebuildRuleEditor(rule, container, record, isGlobal)));
            }

            container.Add(suffixCol);
        }

        private void RebuildRuleEditor(NamingRule rule, VisualElement container,
            RenameRecord record, bool isGlobal)
        {
            BuildRuleEditorUI(rule, container, record, isGlobal);
            if (record != null) UpdateRecordPreview(record);
        }

        private VisualElement BuildNamePartUI(NamePart part, System.Action onRemove, System.Action onChange)
        {
            var box = new VisualElement();
            box.AddToClassList("name-part-box");

            var row = new VisualElement();
            row.AddToClassList("part-row");

            // Value popup
            var valuePopup = new PopupField<string>(PartDisplay.ToList(), part.valueIndex);
            valuePopup.style.width = 110;

            var valueCustom = new TextField { value = part.value };
            valueCustom.style.width = 80;
            valueCustom.style.display =
                part.valueIndex == PartOptions.Length - 1 ? DisplayStyle.Flex : DisplayStyle.None;
            valueCustom.RegisterValueChangedCallback(e =>
            {
                part.value = e.newValue;
                onChange?.Invoke();
            });

            valuePopup.RegisterValueChangedCallback(evt =>
            {
                part.valueIndex = PartDisplay.ToList().IndexOf(evt.newValue);
                part.value = part.valueIndex < PartOptions.Length - 1
                    ? PartOptions[part.valueIndex] : part.value;
                valueCustom.style.display =
                    part.valueIndex == PartOptions.Length - 1 ? DisplayStyle.Flex : DisplayStyle.None;
                onChange?.Invoke();
            });

            row.Add(valuePopup);
            row.Add(valueCustom);

            // Connect popup
            var connPopup = new PopupField<string>(ConnectDisplay.ToList(), part.connectIndex);
            connPopup.style.width = 90;

            var connCustom = new TextField { value = part.connectChar };
            connCustom.style.width = 40;
            connCustom.style.display =
                part.connectIndex == ConnectOptions.Length - 1 ? DisplayStyle.Flex : DisplayStyle.None;
            connCustom.RegisterValueChangedCallback(e =>
            {
                part.connectChar = e.newValue;
                onChange?.Invoke();
            });

            connPopup.RegisterValueChangedCallback(evt =>
            {
                part.connectIndex = ConnectDisplay.ToList().IndexOf(evt.newValue);
                part.connectChar = part.connectIndex < ConnectOptions.Length - 1
                    ? ConnectOptions[part.connectIndex] : part.connectChar;
                connCustom.style.display =
                    part.connectIndex == ConnectOptions.Length - 1 ? DisplayStyle.Flex : DisplayStyle.None;
                onChange?.Invoke();
            });

            row.Add(connPopup);
            row.Add(connCustom);

            // Remove button
            var removeBtn = new Button(() => onRemove?.Invoke()) { text = "✕" };
            removeBtn.AddToClassList("remove-btn");
            row.Add(removeBtn);

            box.Add(row);
            return box;
        }

        // ═════════════════════════════════════════════════════════════════
        // RECORD LIST
        // ═════════════════════════════════════════════════════════════════

        private void RefreshRecordList()
        {
            _recordsContainer?.Clear();
            if (_recordCountLabel != null)
                _recordCountLabel.text = $"Target Assets ({_records.Count})";

            for (int i = 0; i < _records.Count; i++)
            {
                _recordsContainer?.Add(BuildRecordRow(i));
            }
        }

        private VisualElement BuildRecordRow(int index)
        {
            var record = _records[index];
            var row = new VisualElement();
            row.AddToClassList("record-row");

            // Remove
            var removeBtn = new Button(() =>
            {
                Undo.RecordObject(this, "Remove Record");
                _records.RemoveAt(index);
                RefreshRecordList();
            })
            { text = "✕" };
            removeBtn.AddToClassList("remove-btn");
            row.Add(removeBtn);

            // Object field
            var objField = new ObjectField { value = record.targetAsset, objectType = typeof(Object), allowSceneObjects = false };
            objField.style.width = 160;
            objField.RegisterValueChangedCallback(evt =>
            {
                record.targetAsset = evt.newValue;
                UpdateRecordPreview(record);
            });
            row.Add(objField);

            // Inline rule editor (compact)
            var ruleContainer = new VisualElement();
            ruleContainer.AddToClassList("inline-rule");
            BuildRuleEditorUI(record.rule, ruleContainer, record, false);
            row.Add(ruleContainer);

            // Preview label
            var previewLabel = new Label(GetPreviewName(record.rule, record.targetAsset));
            previewLabel.name = $"preview_{index}";
            previewLabel.AddToClassList("preview-label");
            row.Add(previewLabel);

            return row;
        }

        private void UpdateRecordPreview(RenameRecord record)
        {
            int index = _records.IndexOf(record);
            if (index < 0 || _recordsContainer == null) return;

            var row = _recordsContainer.ElementAt(index);
            var label = row?.Q<Label>($"preview_{index}");
            if (label != null)
                label.text = GetPreviewName(record.rule, record.targetAsset);
        }

        // ═════════════════════════════════════════════════════════════════
        // LOGIC (unchanged from original)
        // ═════════════════════════════════════════════════════════════════

        private void OnAddSelected()
        {
            Undo.RecordObject(this, "Add Selected Assets");
            foreach (var obj in Selection.objects)
            {
                if (AssetDatabase.Contains(obj) && !_records.Any(r => r.targetAsset == obj))
                    _records.Add(new RenameRecord { targetAsset = obj, rule = _globalRule.Clone() });
            }
            RefreshRecordList();
        }

        private void OnClearAll()
        {
            Undo.RecordObject(this, "Clear Assets");
            _records.Clear();
            RefreshRecordList();
        }

        private void OnReplaceConnectChar()
        {
            string fromChar = _replaceFromIndex == ConnectOptions.Length - 1
                ? _replaceFromCustom : ConnectOptions[_replaceFromIndex];
            string toChar = _replaceToIndex == ConnectOptions.Length - 1
                ? _replaceToCustom : ConnectOptions[_replaceToIndex];

            if (string.IsNullOrEmpty(fromChar)) { Debug.LogWarning("Replace Connect Char: 'From' is empty."); return; }
            if (fromChar == toChar) { Debug.LogWarning("Replace Connect Char: 'From' and 'To' are the same."); return; }

            var valid = _records.Where(r => r.targetAsset != null).ToList();
            if (valid.Count == 0) return;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            int count = 0;

            foreach (var record in valid)
            {
                string original = record.targetAsset.name;
                if (!original.Contains(fromChar)) continue;
                string newName = original.Replace(fromChar, toChar ?? "");
                string path = AssetDatabase.GetAssetPath(record.targetAsset);
                if (string.IsNullOrEmpty(path)) continue;
                Undo.RecordObject(record.targetAsset, "Replace Connect Char");
                string result = AssetDatabase.RenameAsset(path, newName);
                if (string.IsNullOrEmpty(result)) count++;
                else Debug.LogWarning($"Failed to rename {path}: {result}");
            }

            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Debug.Log($"Replace Connect Char Complete! Updated {count} asset(s). ('{fromChar}' → '{toChar}')");
            RefreshRecordList();
        }

        private void OnClearWhitespace()
        {
            var valid = _records.Where(r => r.targetAsset != null).ToList();
            if (valid.Count == 0) return;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            int count = 0;

            foreach (var record in valid)
            {
                string original = record.targetAsset.name;
                string cleaned = System.Text.RegularExpressions.Regex.Replace(original, @"\s+", "");
                if (original == cleaned) continue;
                string path = AssetDatabase.GetAssetPath(record.targetAsset);
                if (string.IsNullOrEmpty(path)) continue;
                Undo.RecordObject(record.targetAsset, "Clear Whitespace");
                string result = AssetDatabase.RenameAsset(path, cleaned);
                if (string.IsNullOrEmpty(result)) count++;
                else Debug.LogWarning($"Failed to clear whitespace for {path}: {result}");
            }

            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Debug.Log($"Clear Whitespace Complete! Cleaned {count} asset(s).");
            RefreshRecordList();
        }

        private void OnRenameAll()
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            int renamed = 0, skipped = 0;

            foreach (var record in _records)
            {
                if (record.targetAsset == null) { Debug.LogError("record.targetAsset is null"); continue; }

                string newName = GetPreviewName(record.rule, record.targetAsset);
                if (string.IsNullOrEmpty(newName) || GetAssetFileName(record.targetAsset) == newName)
                { skipped++; continue; }

                string path = AssetDatabase.GetAssetPath(record.targetAsset);
                if (string.IsNullOrEmpty(path)) continue;

                Undo.RecordObject(record.targetAsset, "Rename Asset");
                string result = AssetDatabase.RenameAsset(path, newName);
                if (string.IsNullOrEmpty(result)) renamed++;
                else Debug.LogWarning($"Failed to rename {path}: {result}");
            }

            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);

            Debug.Log(renamed == 0 && skipped > 0
                ? $"Batch Renaming: No assets renamed. {skipped} skipped."
                : $"Batch Renaming Complete! Renamed {renamed}, skipped {skipped}.");

            RefreshRecordList();
        }

        // ═════════════════════════════════════════════════════════════════
        // HELPERS
        // ═════════════════════════════════════════════════════════════════

        private string GetPreviewName(NamingRule rule, Object asset)
        {
            var parts = new List<(string value, string conn)>();

            foreach (var p in rule.prefixes)
                if (!string.IsNullOrEmpty(p.value)) parts.Add((p.value, p.connectChar));

            string main = rule.mainNameIndex == 1
                ? (asset != null ? GetAssetFileName(asset) : "(Original Name)")
                : rule.mainName;
            if (!string.IsNullOrEmpty(main)) parts.Add((main, rule.mainConnectChar));

            foreach (var s in rule.suffixes)
                if (!string.IsNullOrEmpty(s.value)) parts.Add((s.value, s.connectChar));

            string result = "";
            for (int i = 0; i < parts.Count; i++)
            {
                result += parts[i].value;
                if (i < parts.Count - 1) result += parts[i].conn;
            }
            return result;
        }

        private string GetAssetFileName(Object asset)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            return string.IsNullOrEmpty(path)
                ? asset.name
                : System.IO.Path.GetFileNameWithoutExtension(path);
        }

        // ═════════════════════════════════════════════════════════════════
        // INLINE STYLES FALLBACK (if .uss file is not found)
        // ═════════════════════════════════════════════════════════════════

        private void ApplyInlineStyles(VisualElement root)
        {
            root.style.paddingTop    = 8;
            root.style.paddingLeft   = 8;
            root.style.paddingRight  = 8;
            root.style.paddingBottom = 8;
            root.style.flexDirection = FlexDirection.Column;
        }
    }
#endif
}