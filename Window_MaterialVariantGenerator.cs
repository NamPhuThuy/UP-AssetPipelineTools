// ───────────────────────────────────────────────────────────────────────
// RULES:
// 1. PROCESS: Use Debug.Log for trace steps.
// 2. SAFETY: Use Debug.LogError in null/boundary checks.
// 3. ENUM FORMAT: If used enum, use the format:
//    public enum Type
//    {
//        NONE = 0, TYPE_1 = 1, TYPE_2 = 2
//    }
// 4. STRINGS: Use 'private const string' for resource paths, settings keys, and default folder paths.
// 5. DIALOGS: Use Debug.LogError (or Debug.LogWarning) instead of EditorUtility.DisplayDialog for editor errors/warnings.
// 6. FOLDERS: For fields representing folder paths, use 'DefaultAsset' fields to allow dragging and dropping folders instead of using simple string fields.
// 7. CACHING: Provide a 'Reset to Defaults' button in the options panel calling a method named 'ResetToDefaults()' to clear/override cached or persisted EditorPrefs values that might become stale or invalid.
// 8. LISTS: When resetting list fields, avoid re-instantiating them if they are not null. Clear them instead to prevent issues with serialized property bindings.
// 9. NOTIFICATIONS: Reduce to use addition window to notify information, just Debug.Log it with color and method name prefix.
// 10. LOGGING CONCISENESS: Keep Debug.Log text short and focused mainly on keywords (e.g., "OnEnable", "Action 1: Start", "Success", "ResetToDefaults") to ensure maximum readability and zero clutter.
// 11. IN-MEMORY RESET: When resetting cached keys in ResetToDefaults(), ensure you also clear or re-initialize the corresponding in-memory fields (e.g., set to default asset or null). Otherwise, OnDisable() will re-save the old in-memory values back to EditorPrefs when the window closes to reload.
// ───────────────────────────────────────────────────────────────────────

using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.IO;
using NamPhuThuy.Common;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
#endif

namespace NamPhuThuy.AssetPipelineTools
{
#if UNITY_EDITOR
    public class Window_MaterialVariantGenerator : EditorWindow
    {
        #region Enums (Rule 3)
        public enum TabType
        {
            NONE = 0,
            GENERATOR = 1,
            SCENE_TEST = 2
        }

        public enum GridMode
        {
            NONE = 0,
            FIXED_ROW_NUM = 1,
            FIXED_COLUMN_NUM = 2
        }
        #endregion

        #region Private Fields
        // Variant Generator Settings
        [SerializeField] private Material _baseMaterial;
        [SerializeField] private string _texturePropertyName = DEFAULT_PROP_NAME;
        [SerializeField] private DefaultAsset _targetFolder;
        [SerializeField] private List<Texture2D> _textures = new List<Texture2D>();

        // Scene Testing Settings
        [SerializeField] private GameObject _testPrefab;
        [SerializeField] private List<Material> _materialsToTest = new List<Material>();
        [SerializeField] private GridMode _gridMode = GridMode.FIXED_COLUMN_NUM;
        [SerializeField] private int _rowCount = 2;
        [SerializeField] private int _columnCount = 4;
        [SerializeField] private float _spacingX = DEFAULT_SPACING;
        [SerializeField] private float _spacingY = DEFAULT_SPACING;
        [SerializeField] private bool _showMaterialList = true;

        // Default constants
        private const string DEFAULT_PROP_NAME = "_MainTex";
        private const float DEFAULT_SPACING = 2.0f;

        // EditorPrefs Keys
        private const string PREF_KEY_BASE_MAT = "NamPhuThuy_MatVarGen_BaseMat";
        private const string PREF_KEY_PROP_NAME = "NamPhuThuy_MatVarGen_PropName";
        private const string PREF_KEY_FOLDER = "NamPhuThuy_MatVarGen_Folder";
        private const string PREF_KEY_TEST_PREFAB = "NamPhuThuy_MatVarGen_TestPrefab";
        private const string PREF_KEY_GRID_MODE = "NamPhuThuy_MatVarGen_GridMode";
        private const string PREF_KEY_ROW_COUNT = "NamPhuThuy_MatVarGen_RowCount";
        private const string PREF_KEY_COLUMN_COUNT = "NamPhuThuy_MatVarGen_ColumnCount";
        private const string PREF_KEY_SPACING_X = "NamPhuThuy_MatVarGen_SpacingX";
        private const string PREF_KEY_SPACING_Y = "NamPhuThuy_MatVarGen_SpacingY";
        private const string PREF_KEY_ACTIVE_TAB = "NamPhuThuy_MatVarGen_ActiveTab";
        private const string PREF_KEY_SHOW_MAT_LIST = "NamPhuThuy_MatVarGen_ShowMatList";

        // Signature logo relative path
        private const string SIGNATURE_MARK_RELATIVE_PATH = "../UP-Common/nam_phu_thuy.png";

        // UI Style Colors
        private static Color COLOR_GREY_BOX => new Color(0.16f, 0.16f, 0.16f, 0.6f);
        private static Color COLOR_GREY_BORDER => new Color(0.26f, 0.26f, 0.26f, 0.8f);
        private static Color COLOR_SKY_BLUE => new Color(0.53f, 0.8f, 0.92f, 1f);
        private static Color COLOR_FOREST_MIST => new Color(0.8f, 0.8f, 0.8f, 1f);
        private static Color COLOR_OCEAN_BLUE => new Color(0.0f, 0.47f, 0.74f, 1f);
        private static Color COLOR_TAB_INACTIVE_BG => new Color(0.16f, 0.16f, 0.16f, 1f);
        private static Color COLOR_TAB_INACTIVE_BORDER => new Color(0.11f, 0.11f, 0.11f, 1f);

        // Tab State
        private TabType _activeTab = TabType.GENERATOR;

        // UI References
        private ObjectField _baseMatField;
        private TextField _propNameField;
        private ObjectField _folderField;
        private VisualElement _listContainer;

        private ObjectField _testPrefabField;
        private EnumField _gridModeField;
        private IntegerField _rowCountField;
        private IntegerField _columnCountField;
        private FloatField _spacingXField;
        private FloatField _spacingYField;
        private VisualElement _materialListContainer;
        private VisualElement _materialListSectionWrapper;
        private Button _toggleListBtn;

        private VisualElement _tabHeaderContainer;
        private ScrollView _contentContainer;
        #endregion

        #region Menu Item
        [MenuItem("NamPhuThuy/Assets Pipeline/Window - Material Variant Generator")]
        public static void ShowWindow()
        {
            var window = GetWindow<Window_MaterialVariantGenerator>("Material Variant Generator");
            window.minSize = new Vector2(400, 750);
            window.Show();
        }
        #endregion

        #region Unity Callbacks
        private void OnEnable()
        {
            Debug.Log("[MatVarGen] OnEnable");
            
            // Load Variant settings
            string baseMatPath = EditorPrefs.GetString(PREF_KEY_BASE_MAT, "");
            if (!string.IsNullOrEmpty(baseMatPath))
            {
                _baseMaterial = AssetDatabase.LoadAssetAtPath<Material>(baseMatPath);
            }

            _texturePropertyName = EditorPrefs.GetString(PREF_KEY_PROP_NAME, DEFAULT_PROP_NAME);

            string folderPath = EditorPrefs.GetString(PREF_KEY_FOLDER, "");
            if (!string.IsNullOrEmpty(folderPath))
            {
                _targetFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folderPath);
            }

            // Load Scene Testing settings
            string testPrefabPath = EditorPrefs.GetString(PREF_KEY_TEST_PREFAB, "");
            if (!string.IsNullOrEmpty(testPrefabPath))
            {
                _testPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(testPrefabPath);
            }

            _gridMode = (GridMode)EditorPrefs.GetInt(PREF_KEY_GRID_MODE, (int)GridMode.FIXED_COLUMN_NUM);
            _rowCount = EditorPrefs.GetInt(PREF_KEY_ROW_COUNT, 2);
            _columnCount = EditorPrefs.GetInt(PREF_KEY_COLUMN_COUNT, 4);
            _spacingX = EditorPrefs.GetFloat(PREF_KEY_SPACING_X, DEFAULT_SPACING);
            _spacingY = EditorPrefs.GetFloat(PREF_KEY_SPACING_Y, DEFAULT_SPACING);
            _activeTab = (TabType)EditorPrefs.GetInt(PREF_KEY_ACTIVE_TAB, (int)TabType.GENERATOR);
            _showMaterialList = EditorPrefs.GetBool(PREF_KEY_SHOW_MAT_LIST, true);
        }

        private void OnDisable()
        {
            Debug.Log("[MatVarGen] OnDisable");
            
            // Save Variant settings
            if (_baseMaterial != null)
            {
                EditorPrefs.SetString(PREF_KEY_BASE_MAT, AssetDatabase.GetAssetPath(_baseMaterial));
            }
            else
            {
                EditorPrefs.DeleteKey(PREF_KEY_BASE_MAT);
            }

            EditorPrefs.SetString(PREF_KEY_PROP_NAME, _texturePropertyName);

            if (_targetFolder != null)
            {
                EditorPrefs.SetString(PREF_KEY_FOLDER, AssetDatabase.GetAssetPath(_targetFolder));
            }
            else
            {
                EditorPrefs.DeleteKey(PREF_KEY_FOLDER);
            }

            // Save Scene Testing settings
            if (_testPrefab != null)
            {
                EditorPrefs.SetString(PREF_KEY_TEST_PREFAB, AssetDatabase.GetAssetPath(_testPrefab));
            }
            else
            {
                EditorPrefs.DeleteKey(PREF_KEY_TEST_PREFAB);
            }

            EditorPrefs.SetInt(PREF_KEY_GRID_MODE, (int)_gridMode);
            EditorPrefs.SetInt(PREF_KEY_ROW_COUNT, _rowCount);
            EditorPrefs.SetInt(PREF_KEY_COLUMN_COUNT, _columnCount);
            EditorPrefs.SetFloat(PREF_KEY_SPACING_X, _spacingX);
            EditorPrefs.SetFloat(PREF_KEY_SPACING_Y, _spacingY);
            EditorPrefs.SetInt(PREF_KEY_ACTIVE_TAB, (int)_activeTab);
            EditorPrefs.SetBool(PREF_KEY_SHOW_MAT_LIST, _showMaterialList);
        }

        public void CreateGUI()
        {
            Debug.Log("[MatVarGen] CreateGUI");
            var root = rootVisualElement;
            root.style.paddingLeft = 14;
            root.style.paddingRight = 14;
            root.style.paddingTop = 14;
            root.style.paddingBottom = 14;

            // 1. Header Branding Section
            root.Add(BuildHeader());

            // 2. Navigation Tab Bar
            _tabHeaderContainer = BuildNavigation();
            root.Add(_tabHeaderContainer);

            // Separator line
            var separator = new VisualElement
            {
                style =
                {
                    height = 2,
                    backgroundColor = COLOR_GREY_BORDER,
                    marginTop = 4,
                    marginBottom = 12
                }
            };
            root.Add(separator);

            // 3. Dynamic content container
            _contentContainer = new ScrollView(ScrollViewMode.Vertical)
            {
                style = { flexGrow = 1 }
            };
            root.Add(_contentContainer);

            // Render active tab content
            RefreshRegion();

            // 4. Global Footer (Reset button)
            var buttonRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 10 } };
            var resetBtn = new Button(ResetToDefaults) 
            { 
                text = "Reset Defaults", 
                style = { flexGrow = 1, height = 30, unityFontStyleAndWeight = FontStyle.Bold } 
            };
            buttonRow.Add(resetBtn);
            root.Add(buttonRow);
        }
        #endregion

        #region Layout Builders
        /// <summary>
        /// Builds the top branding header containing the signature mark image.
        /// </summary>
        private VisualElement BuildHeader()
        {
            var headerRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingBottom = 10,
                    marginBottom = 8,
                    borderBottomWidth = 1,
                    borderBottomColor = COLOR_GREY_BORDER
                }
            };

            // Signature mark visual element (nam_phu_thuy.png)
            var signatureMark = new VisualElement
            {
                style =
                {
                    width = 44,
                    height = 44,
                    marginRight = 12,
                    borderTopLeftRadius = 6,
                    borderTopRightRadius = 6,
                    borderBottomLeftRadius = 6,
                    borderBottomRightRadius = 6
                }
            };

            // Resolve relative path to absolute asset path
            string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
            string scriptDir = Path.GetDirectoryName(scriptPath);
            string combinedPath = Path.Combine(scriptDir, SIGNATURE_MARK_RELATIVE_PATH);
            string fullPath = Path.GetFullPath(combinedPath).Replace("\\", "/");
            string resolvedPath = "Assets" + fullPath.Substring(Application.dataPath.Length);

            // Loading texture dynamically
            var signatureTex = AssetDatabase.LoadAssetAtPath<Texture2D>(resolvedPath);
            if (signatureTex != null)
            {
                signatureMark.style.backgroundImage = signatureTex;
            }
            else
            {
                Debug.LogWarning($"<color=orange>[Window_MaterialVariantGenerator]</color> Missing Logo: {resolvedPath}");
                signatureMark.style.backgroundColor = COLOR_GREY_BOX;
            }
            headerRow.Add(signatureMark);

            // Titles
            var textColumn = new VisualElement { style = { flexGrow = 1 } };
            var mainTitle = new Label("Material Variant Generator")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 16,
                    color = COLOR_SKY_BLUE
                }
            };
            var subTitle = new Label("Assets Pipeline Tools")
            {
                style =
                {
                    fontSize = 11,
                    color = COLOR_FOREST_MIST,
                    unityFontStyleAndWeight = FontStyle.Normal
                }
            };
            textColumn.Add(mainTitle);
            textColumn.Add(subTitle);
            headerRow.Add(textColumn);

            return headerRow;
        }

        /// <summary>
        /// Builds the navigation tabs bar.
        /// </summary>
        private VisualElement BuildNavigation()
        {
            var bar = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.FlexStart
                }
            };

            bar.Add(CreateNavigationButton("Generator", TabType.GENERATOR));
            bar.Add(CreateNavigationButton("Scene Test", TabType.SCENE_TEST));

            return bar;
        }

        /// <summary>
        /// Instantiates a styled tab button with active states.
        /// </summary>
        private Button CreateNavigationButton(string label, TabType tab)
        {
            bool isActive = _activeTab == tab;
            var btn = new Button(() => SwitchRegion(tab))
            {
                text = label,
                style =
                {
                    flexGrow = 1,
                    height = 28,
                    fontSize = 12,
                    marginLeft = 2,
                    marginRight = 2,
                    unityFontStyleAndWeight = isActive ? FontStyle.Bold : FontStyle.Normal,
                    backgroundColor = isActive ? COLOR_OCEAN_BLUE : COLOR_TAB_INACTIVE_BG,
                    color = isActive ? Color.white : COLOR_FOREST_MIST,
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderTopColor = isActive ? COLOR_SKY_BLUE : COLOR_TAB_INACTIVE_BORDER,
                    borderBottomColor = isActive ? COLOR_SKY_BLUE : COLOR_TAB_INACTIVE_BORDER,
                    borderLeftColor = isActive ? COLOR_SKY_BLUE : COLOR_TAB_INACTIVE_BORDER,
                    borderRightColor = isActive ? COLOR_SKY_BLUE : COLOR_TAB_INACTIVE_BORDER,
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4
                }
            };

            return btn;
        }

        /// <summary>
        /// Updates the current active subpage.
        /// </summary>
        private void SwitchRegion(TabType newTab)
        {
            if (_activeTab == newTab) return;
            _activeTab = newTab;

            // Rebuild tab headers to display active/inactive states
            var parent = _tabHeaderContainer.parent;
            int siblingIdx = parent.IndexOf(_tabHeaderContainer);
            parent.Remove(_tabHeaderContainer);
            _tabHeaderContainer = BuildNavigation();
            parent.Insert(siblingIdx, _tabHeaderContainer);

            RefreshRegion();
        }

        /// <summary>
        /// Refreshes the content inside the scroll area based on active tab selection.
        /// </summary>
        private void RefreshRegion()
        {
            _contentContainer.Clear();

            switch (_activeTab)
            {
                case TabType.GENERATOR:
                    BuildGeneratorPage(_contentContainer);
                    break;
                case TabType.SCENE_TEST:
                    BuildSceneTestPage(_contentContainer);
                    break;
            }
        }
        #endregion

        #region Page Content Builders
        private void BuildGeneratorPage(VisualElement container)
        {
            var helpBox = new HelpBox(
                "Generates multiple material variants from a Base Material template.\n" +
                "Specify a base material, target texture property (e.g. _MainTex or _DissolveTex), and output folder. The tool will clone the material for each texture in the list.",
                HelpBoxMessageType.Info);
            container.Add(helpBox);

            container.Add(BuildSettingsSection());
            container.Add(BuildTexturesListSection());

            var generateBtn = new Button(GenerateVariants) 
            { 
                text = "Generate Materials", 
                style = { height = 30, marginTop = 10, unityFontStyleAndWeight = FontStyle.Bold, backgroundColor = COLOR_OCEAN_BLUE, color = Color.white } 
            };
            container.Add(generateBtn);
        }

        private void BuildSceneTestPage(VisualElement container)
        {
            var helpBox = new HelpBox(
                "Instantiates prefab variants in the scene spaced in a 2D layout grid, applying the configured test materials.",
                HelpBoxMessageType.Info);
            container.Add(helpBox);

            container.Add(BuildTestSection());
        }

        private VisualElement BuildSettingsSection()
        {
            var box = UITKEditorHelper.BuildBox("Configuration");

            _baseMatField = new ObjectField("Base Material")
            {
                objectType = typeof(Material),
                value = _baseMaterial
            };
            _baseMatField.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(this, "Change Base Material");
                _baseMaterial = e.newValue as Material;
            });
            box.Add(_baseMatField);

            _propNameField = new TextField("Texture Property") { value = _texturePropertyName };
            _propNameField.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(this, "Change Texture Property Name");
                _texturePropertyName = e.newValue;
            });
            box.Add(_propNameField);

            _folderField = new ObjectField("Output Folder")
            {
                objectType = typeof(DefaultAsset),
                value = _targetFolder
            };
            _folderField.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(this, "Change Output Folder");
                _targetFolder = e.newValue as DefaultAsset;
            });
            box.Add(_folderField);

            return box;
        }

        private VisualElement BuildTexturesListSection()
        {
            var box = UITKEditorHelper.BuildBox("Source Textures");

            var buttonRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 5 } };

            var addBtn = new Button(AddTextureField) 
            { 
                text = "Add Slot", 
                style = { flexGrow = 1, marginRight = 2 } 
            };
            buttonRow.Add(addBtn);

            var addSelectedBtn = new Button(AddSelectedTextures) 
            { 
                text = "Add Selected", 
                style = { flexGrow = 1, marginLeft = 2, marginRight = 2 } 
            };
            buttonRow.Add(addSelectedBtn);

            var clearBtn = new Button(ClearTexturesList) 
            { 
                text = "Clear", 
                style = { flexGrow = 1, marginLeft = 2, backgroundColor = new Color(0.55f, 0.15f, 0.15f, 1f), color = Color.white } 
            };
            buttonRow.Add(clearBtn);

            box.Add(buttonRow);

            var scroll = new ScrollView { style = { maxHeight = 200, minHeight = 100 } };
            _listContainer = new VisualElement();
            scroll.Add(_listContainer);
            box.Add(scroll);

            RefreshTexturesListUI();

            return box;
        }

        private VisualElement BuildTestSection()
        {
            var box = UITKEditorHelper.BuildBox("Test in Scene");

            _testPrefabField = new ObjectField("Test Prefab")
            {
                objectType = typeof(GameObject),
                value = _testPrefab
            };
            _testPrefabField.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(this, "Change Test Prefab");
                _testPrefab = e.newValue as GameObject;
            });
            box.Add(_testPrefabField);

            _gridModeField = new EnumField("Grid Mode", _gridMode);
            _gridModeField.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(this, "Change Grid Mode");
                _gridMode = (GridMode)e.newValue;
                UpdateGridFieldsVisibility();
            });
            box.Add(_gridModeField);

            _rowCountField = new IntegerField("Row Count") { value = _rowCount };
            _rowCountField.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(this, "Change Row Count");
                _rowCount = Mathf.Max(1, e.newValue);
            });
            box.Add(_rowCountField);

            _columnCountField = new IntegerField("Column Count") { value = _columnCount };
            _columnCountField.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(this, "Change Column Count");
                _columnCount = Mathf.Max(1, e.newValue);
            });
            box.Add(_columnCountField);

            _spacingXField = new FloatField("Spacing X") { value = _spacingX };
            _spacingXField.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(this, "Change Spacing X");
                _spacingX = e.newValue;
            });
            box.Add(_spacingXField);

            _spacingYField = new FloatField("Spacing Y") { value = _spacingY };
            _spacingYField.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(this, "Change Spacing Y");
                _spacingY = e.newValue;
            });
            box.Add(_spacingYField);

            // Auto-load generated variants
            var autoLoadBtn = new Button(AutoLoadGeneratedMaterials)
            {
                text = "Load Generated Variants",
                style = { marginBottom = 5 }
            };
            box.Add(autoLoadBtn);

            // Toggle material list button
            _toggleListBtn = new Button(ToggleMaterialList)
            {
                text = _showMaterialList ? "Hide Material List" : "Show Material List",
                style = { marginBottom = 5 }
            };
            box.Add(_toggleListBtn);

            // Material list section wrapper
            _materialListSectionWrapper = new VisualElement
            {
                style = { display = _showMaterialList ? DisplayStyle.Flex : DisplayStyle.None }
            };
            box.Add(_materialListSectionWrapper);

            // Material list section
            var listHeader = new Label("Materials to Test:") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 5, marginBottom = 5 } };
            _materialListSectionWrapper.Add(listHeader);

            var buttonRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 5 } };

            var addBtn = new Button(AddMaterialField) 
            { 
                text = "Add Slot", 
                style = { flexGrow = 1, marginRight = 2 } 
            };
            buttonRow.Add(addBtn);

            var addSelectedBtn = new Button(AddSelectedMaterials) 
            { 
                text = "Add Selected", 
                style = { flexGrow = 1, marginLeft = 2, marginRight = 2 } 
            };
            buttonRow.Add(addSelectedBtn);

            var clearBtn = new Button(ClearMaterialsList) 
            { 
                text = "Clear", 
                style = { flexGrow = 1, marginLeft = 2, backgroundColor = new Color(0.55f, 0.15f, 0.15f, 1f), color = Color.white } 
            };
            buttonRow.Add(clearBtn);

            _materialListSectionWrapper.Add(buttonRow);

            var scroll = new ScrollView { style = { maxHeight = 200, minHeight = 100 } };
            _materialListContainer = new VisualElement();
            scroll.Add(_materialListContainer);
            _materialListSectionWrapper.Add(scroll);

            // Instantiate button
            var instantiateBtn = new Button(InstantiateTestVariants)
            {
                text = "Instantiate Test Grid in Scene",
                style = { height = 28, marginTop = 8, unityFontStyleAndWeight = FontStyle.Bold, backgroundColor = COLOR_OCEAN_BLUE, color = Color.white }
            };
            box.Add(instantiateBtn);

            RefreshMaterialsListUI();
            UpdateGridFieldsVisibility();

            return box;
        }

        private void RefreshTexturesListUI()
        {
            _listContainer.Clear();
            if (_textures == null)
            {
                DebugLogger.LogError("Textures list null", context: this);
                return;
            }

            for (int i = 0; i < _textures.Count; i++)
            {
                int index = i;
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 2, alignItems = Align.Center } };

                row.Add(new Label($"Item [{index}]") { style = { width = 60 } });

                var field = new ObjectField 
                { 
                    objectType = typeof(Texture2D), 
                    value = _textures[index], 
                    style = { flexGrow = 1 } 
                };
                field.RegisterValueChangedCallback(e =>
                {
                    Undo.RecordObject(this, "Modify Texture Entry");
                    _textures[index] = e.newValue as Texture2D;
                });
                row.Add(field);

                var removeBtn = new Button(() => 
                {
                    Undo.RecordObject(this, "Remove Texture Slot");
                    _textures.RemoveAt(index);
                    RefreshTexturesListUI();
                }) 
                { 
                    text = "✕", 
                    style = { width = 25 } 
                };
                row.Add(removeBtn);

                _listContainer.Add(row);
            }
        }

        private void RefreshMaterialsListUI()
        {
            _materialListContainer.Clear();
            if (_materialsToTest == null)
            {
                DebugLogger.LogError("Materials list null", context: this);
                return;
            }

            for (int i = 0; i < _materialsToTest.Count; i++)
            {
                int index = i;
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 2, alignItems = Align.Center } };

                row.Add(new Label($"Item [{index}]") { style = { width = 60 } });

                var field = new ObjectField 
                { 
                    objectType = typeof(Material), 
                    value = _materialsToTest[index], 
                    style = { flexGrow = 1 } 
                };
                field.RegisterValueChangedCallback(e =>
                {
                    Undo.RecordObject(this, "Modify Material Entry");
                    _materialsToTest[index] = e.newValue as Material;
                });
                row.Add(field);

                var removeBtn = new Button(() => 
                {
                    Undo.RecordObject(this, "Remove Material Slot");
                    _materialsToTest.RemoveAt(index);
                    RefreshMaterialsListUI();
                }) 
                { 
                    text = "✕", 
                    style = { width = 25 } 
                };
                row.Add(removeBtn);

                _materialListContainer.Add(row);
            }
        }

        private void UpdateGridFieldsVisibility()
        {
            if (_rowCountField == null || _columnCountField == null) return;

            if (_gridMode == GridMode.FIXED_ROW_NUM)
            {
                _rowCountField.style.display = DisplayStyle.Flex;
                _columnCountField.style.display = DisplayStyle.None;
            }
            else if (_gridMode == GridMode.FIXED_COLUMN_NUM)
            {
                _rowCountField.style.display = DisplayStyle.None;
                _columnCountField.style.display = DisplayStyle.Flex;
            }
            else
            {
                _rowCountField.style.display = DisplayStyle.None;
                _columnCountField.style.display = DisplayStyle.None;
            }
        }
        #endregion

        #region Operations / Methods
        private void ClearTexturesList()
        {
            Undo.RecordObject(this, "Clear Textures List");
            if (_textures != null)
            {
                _textures.Clear();
            }
            RefreshTexturesListUI();
            Debug.Log("[MatVarGen] Textures list cleared.");
        }

        private void ClearMaterialsList()
        {
            Undo.RecordObject(this, "Clear Materials List");
            if (_materialsToTest != null)
            {
                _materialsToTest.Clear();
            }
            RefreshMaterialsListUI();
            Debug.Log("[MatVarGen] Materials list cleared.");
        }

        private void AddTextureField()
        {
            Undo.RecordObject(this, "Add Texture Slot");
            _textures.Add(null);
            RefreshTexturesListUI();
        }

        private void AddSelectedTextures()
        {
            var selectedObjects = Selection.objects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                Debug.LogWarning("[MatVarGen] No textures selected in project view.");
                return;
            }

            Undo.RecordObject(this, "Add Selected Textures");

            int addedCount = 0;
            foreach (var obj in selectedObjects)
            {
                if (obj is Texture2D tex)
                {
                    if (!_textures.Contains(tex))
                    {
                        _textures.Add(tex);
                        addedCount++;
                    }
                }
            }

            if (addedCount > 0)
            {
                Debug.Log($"[MatVarGen] Added {addedCount} selected textures.");
                RefreshTexturesListUI();
            }
            else
            {
                Debug.LogWarning("[MatVarGen] No new Texture2D assets found in selection.");
            }
        }

        private void ToggleMaterialList()
        {
            _showMaterialList = !_showMaterialList;
            Debug.Log($"[MatVarGen] Toggle Material List to {_showMaterialList}");

            if (_materialListSectionWrapper == null) return;
            _materialListSectionWrapper.style.display = _showMaterialList ? DisplayStyle.Flex : DisplayStyle.None;

            if (_toggleListBtn == null) return;
            _toggleListBtn.text = _showMaterialList ? "Hide Material List" : "Show Material List";
        }

        private void AddMaterialField()
        {
            Undo.RecordObject(this, "Add Material Slot");
            _materialsToTest.Add(null);
            RefreshMaterialsListUI();
        }

        private void AddSelectedMaterials()
        {
            var selectedObjects = Selection.objects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                Debug.LogWarning("[MatVarGen] No materials selected.");
                return;
            }

            Undo.RecordObject(this, "Add Selected Materials");

            int addedCount = 0;
            foreach (var obj in selectedObjects)
            {
                if (obj is Material mat)
                {
                    if (!_materialsToTest.Contains(mat))
                    {
                        _materialsToTest.Add(mat);
                        addedCount++;
                    }
                }
            }

            if (addedCount > 0)
            {
                Debug.Log($"[MatVarGen] Added {addedCount} selected materials.");
                RefreshMaterialsListUI();
            }
            else
            {
                Debug.LogWarning("[MatVarGen] No new Material assets found in selection.");
            }
        }

        private void GenerateVariants()
        {
            if (_baseMaterial == null)
            {
                DebugLogger.LogError("BaseMaterial null", context: this);
                return;
            }
            if (_targetFolder == null)
            {
                DebugLogger.LogError("TargetFolder null", context: this);
                return;
            }
            if (_textures == null || _textures.Count == 0)
            {
                DebugLogger.LogError("Textures empty", context: this);
                return;
            }

            string folderPath = AssetDatabase.GetAssetPath(_targetFolder);
            if (string.IsNullOrEmpty(folderPath))
            {
                DebugLogger.LogError("Folder path invalid", context: this);
                return;
            }

            Debug.Log($"[MatVarGen] Starting variant generation in: {folderPath}");

            int generatedCount = 0;
            for (int i = 0; i < _textures.Count; i++)
            {
                Texture2D tex = _textures[i];
                if (tex == null)
                {
                    DebugLogger.LogError("Texture null", context: this);
                    continue;
                }

                // Clone material as variant of base material
                Material newMat = new Material(_baseMaterial);
                newMat.parent = _baseMaterial;
                if (newMat.HasProperty(_texturePropertyName))
                {
                    newMat.SetTexture(_texturePropertyName, tex);
                }
                else
                {
                    Debug.LogWarning($"[MatVarGen] Shader {_baseMaterial.shader.name} lacks property {_texturePropertyName}. Applying anyway.");
                    newMat.SetTexture(_texturePropertyName, tex);
                }

                // Create unique asset path with cleaned names
                string cleanBaseName = _baseMaterial.name;
                if (cleanBaseName.StartsWith("PixelDissolve_"))
                {
                    cleanBaseName = cleanBaseName.Substring("PixelDissolve_".Length);
                }

                string cleanTexName = tex.name;
                if (cleanTexName.StartsWith("PixelDissolve_"))
                {
                    cleanTexName = cleanTexName.Substring("PixelDissolve_".Length);
                }
                if (cleanTexName.EndsWith(" - 256x256"))
                {
                    cleanTexName = cleanTexName.Substring(0, cleanTexName.Length - " - 256x256".Length);
                }

                string assetName = $"{cleanBaseName}_{cleanTexName}.mat";
                string assetPath = Path.Combine(folderPath, assetName).Replace("\\", "/");

                // Set internal object name explicitly to ensure it matches the file name (no prefix/suffix)
                newMat.name = Path.GetFileNameWithoutExtension(assetPath);

                // Save material
                AssetDatabase.CreateAsset(newMat, assetPath);
                generatedCount++;
                Debug.Log($"[MatVarGen] Generated material: {assetPath}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[MatVarGen] Completed. Generated {generatedCount} material variants.");
        }

        private void AutoLoadGeneratedMaterials()
        {
            if (_baseMaterial == null)
            {
                DebugLogger.LogError("BaseMaterial null", context: this);
                return;
            }
            if (_targetFolder == null)
            {
                DebugLogger.LogError("TargetFolder null", context: this);
                return;
            }
            if (_textures == null || _textures.Count == 0)
            {
                DebugLogger.LogError("Textures empty", context: this);
                return;
            }

            string folderPath = AssetDatabase.GetAssetPath(_targetFolder);
            if (string.IsNullOrEmpty(folderPath))
            {
                DebugLogger.LogError("Folder path invalid", context: this);
                return;
            }

            Undo.RecordObject(this, "Auto Load Materials");
            _materialsToTest.Clear();

            for (int i = 0; i < _textures.Count; i++)
            {
                Texture2D tex = _textures[i];
                if (tex == null)
                {
                    Debug.LogWarning("[MatVarGen] Texture slot null, skipped.");
                    continue;
                }

                string cleanBaseName = _baseMaterial.name;
                if (cleanBaseName.StartsWith("PixelDissolve_"))
                {
                    cleanBaseName = cleanBaseName.Substring("PixelDissolve_".Length);
                }

                string cleanTexName = tex.name;
                if (cleanTexName.StartsWith("PixelDissolve_"))
                {
                    cleanTexName = cleanTexName.Substring("PixelDissolve_".Length);
                }
                if (cleanTexName.EndsWith(" - 256x256"))
                {
                    cleanTexName = cleanTexName.Substring(0, cleanTexName.Length - " - 256x256".Length);
                }

                string assetName = $"{cleanBaseName}_{cleanTexName}.mat";
                string assetPath = Path.Combine(folderPath, assetName).Replace("\\", "/");
                var mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                if (mat != null)
                {
                    _materialsToTest.Add(mat);
                }
            }

            Debug.Log($"[MatVarGen] Auto-loaded {_materialsToTest.Count} generated materials.");
            RefreshMaterialsListUI();
        }

        private void InstantiateTestVariants()
        {
            if (_testPrefab == null)
            {
                DebugLogger.LogError("TestPrefab null", context: this);
                return;
            }
            if (_materialsToTest == null || _materialsToTest.Count == 0)
            {
                DebugLogger.LogError("Materials empty", context: this);
                return;
            }

            // Create a parent GameObject to keep the hierarchy clean
            GameObject groupParent = new GameObject($"{_testPrefab.name}_Variants_Test");
            Undo.RegisterCreatedObjectUndo(groupParent, "Create Variant Test Group");

            // Attach and configure test animator component
            var animator = groupParent.AddComponent<VariantTestAnimator>();
            if (_baseMaterial != null)
            {
                if (_baseMaterial.shader == null)
                {
                    DebugLogger.LogError("Shader null", context: this);
                }
                else if (_baseMaterial.shader.name.Contains("PixelGlass"))
                {
                    animator.Configure("_SheenProgress", -0.5f, 1.5f, 1.5f);
                }
                else if (_baseMaterial.shader.name.Contains("PixelDissolve"))
                {
                    animator.Configure("_Progress", 0f, 1f, 2.0f);
                }
                else
                {
                    animator.Configure("_Progress", 0f, 1f, 2.0f);
                }
            }

            // Determine columns and rows counts based on mode
            int columns = 1;
            int rows = 1;

            if (_gridMode == GridMode.FIXED_COLUMN_NUM)
            {
                columns = Mathf.Max(1, _columnCount);
                rows = Mathf.CeilToInt((float)_materialsToTest.Count / columns);
            }
            else if (_gridMode == GridMode.FIXED_ROW_NUM)
            {
                rows = Mathf.Max(1, _rowCount);
                columns = Mathf.CeilToInt((float)_materialsToTest.Count / rows);
            }

            int instantiatedCount = 0;
            for (int i = 0; i < _materialsToTest.Count; i++)
            {
                Material mat = _materialsToTest[i];
                if (mat == null)
                {
                    DebugLogger.LogError("Material null", context: this);
                    continue;
                }

                // Row-major index mapping (horizontal wrapping)
                int r = i / columns;
                int c = i % columns;

                // Successive rows spawn downwards along the negative Y axis
                Vector3 pos = new Vector3(c * _spacingX, -r * _spacingY, 0f);

                // Instantiate prefab
                GameObject instance = PrefabUtility.InstantiatePrefab(_testPrefab) as GameObject;
                if (instance == null)
                {
                    DebugLogger.LogError("Prefab instantiation failed", context: this);
                    continue;
                }

                instance.transform.position = pos;
                instance.transform.SetParent(groupParent.transform);
                instance.name = $"{_testPrefab.name}_{mat.name}";

                // Assign material to all renderers on the instance
                var renderers = instance.GetComponentsInChildren<Renderer>(true);
                for (int rd = 0; rd < renderers.Length; rd++)
                {
                    if (renderers[rd] == null)
                    {
                        DebugLogger.LogError("Renderer null", context: this);
                        continue;
                    }
                    renderers[rd].sharedMaterial = mat;
                }

                // Position label below prefab bounds dynamically
                float bottomY = -1.2f; // fallback
                if (renderers != null && renderers.Length > 0)
                {
                    bool boundsInitialized = false;
                    Bounds b = new Bounds();
                    for (int rd = 0; rd < renderers.Length; rd++)
                    {
                        Renderer rend = renderers[rd];
                        if (rend == null) continue;

                        if (!boundsInitialized)
                        {
                            b = rend.bounds;
                            boundsInitialized = true;
                        }
                        else
                        {
                            b.Encapsulate(rend.bounds);
                        }
                    }

                    if (boundsInitialized)
                    {
                        // Calculate relative min Y from the instance's transform position
                        bottomY = (b.min.y - instance.transform.position.y) - 0.4f;
                    }
                }

                // Create TMPro label object
                GameObject labelObj = new GameObject("Label_Text");
                labelObj.transform.SetParent(instance.transform);
                labelObj.transform.localPosition = new Vector3(0f, bottomY, 0f);

                var tmpText = labelObj.AddComponent<TMPro.TextMeshPro>();
                tmpText.text = mat.name;
                tmpText.fontSize = 3f;
                tmpText.alignment = TMPro.TextAlignmentOptions.Center;
                tmpText.color = Color.white;

                Undo.RegisterCreatedObjectUndo(instance, "Instantiate Test Variant");
                instantiatedCount++;
            }

            // Select the parent group in the scene
            Selection.activeGameObject = groupParent;

            Debug.Log($"[MatVarGen] Instantiated {instantiatedCount} test variants in scene.");
        }

        private void ResetToDefaults()
        {
            Debug.Log("[MatVarGen] Reset Start");

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Material Variant Generator - Reset Defaults");
            int undoGroup = Undo.GetCurrentGroup();

            Undo.RecordObject(this, "Reset to Defaults");

            _baseMaterial = null;
            _texturePropertyName = DEFAULT_PROP_NAME;
            _targetFolder = null;
            _testPrefab = null;
            _gridMode = GridMode.FIXED_COLUMN_NUM;
            _rowCount = 2;
            _columnCount = 4;
            _spacingX = DEFAULT_SPACING;
            _spacingY = DEFAULT_SPACING;
            _activeTab = TabType.GENERATOR;
            _showMaterialList = true;
            
            if (_textures != null)
            {
                _textures.Clear();
            }

            if (_materialsToTest != null)
            {
                _materialsToTest.Clear();
            }

            // Clear persisted EditorPrefs keys
            EditorPrefs.DeleteKey(PREF_KEY_BASE_MAT);
            EditorPrefs.DeleteKey(PREF_KEY_PROP_NAME);
            EditorPrefs.DeleteKey(PREF_KEY_FOLDER);
            EditorPrefs.DeleteKey(PREF_KEY_TEST_PREFAB);
            EditorPrefs.DeleteKey(PREF_KEY_GRID_MODE);
            EditorPrefs.DeleteKey(PREF_KEY_ROW_COUNT);
            EditorPrefs.DeleteKey(PREF_KEY_COLUMN_COUNT);
            EditorPrefs.DeleteKey(PREF_KEY_SPACING_X);
            EditorPrefs.DeleteKey(PREF_KEY_SPACING_Y);
            EditorPrefs.DeleteKey(PREF_KEY_ACTIVE_TAB);
            EditorPrefs.DeleteKey(PREF_KEY_SHOW_MAT_LIST);

            // Rebuild GUI
            var root = rootVisualElement;
            root.Clear();
            CreateGUI();

            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("[MatVarGen] Reset to defaults");
        }
        #endregion
    }
#endif
}
