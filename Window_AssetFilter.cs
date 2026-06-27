#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;

namespace NamPhuThuy.AssetPipelineTools
{
    public class Window_AssetFilter : EditorWindow
    {
        #region Color Palette Constants
        private static Color COLOR_EDITOR_BG => new Color(0.22f, 0.22f, 0.22f, 1f);          // Unity Editor Default Grey
        private static Color COLOR_GREY_BOX => new Color(0.16f, 0.16f, 0.16f, 0.6f);          // Grey panel background
        private static Color COLOR_GREY_BORDER => new Color(0.26f, 0.26f, 0.26f, 0.8f);       // Grey panel border
        private static Color COLOR_OCEAN_BLUE => new Color(0.0f, 0.47f, 0.74f, 1f);          // Clickable Blue-Palette Primary (Water/Ocean)
        private static Color COLOR_SKY_BLUE => new Color(0.53f, 0.8f, 0.92f, 1f);            // Clickable Blue-Palette Highlight (Sky)
        private static Color COLOR_FOREST_MIST => new Color(0.8f, 0.8f, 0.8f, 1f);           // Neutral Text color
        private static Color COLOR_DANGER_BG => new Color(0.55f, 0.15f, 0.15f, 1f);          // Red background for danger actions
        private static Color COLOR_DANGER_BORDER => new Color(0.6f, 0.2f, 0.2f, 0.8f);        // Red border for danger actions
        #endregion

        #region Private Fields
        private const string PREF_KEY_FILTER_TEXT = "NamPhuThuy_AssetFilter_FilterText";
        private const string PREF_KEY_FILTER_MASK = "NamPhuThuy_AssetFilter_FilterMask";
        private const string SIGNATURE_MARK_RELATIVE_PATH = "../../UP_Common/nam_phu_thuy.png";
        private const string WINDOW_TITLE = "Asset Filter";

        [SerializeField] private List<DefaultAsset> _targetFolders = new List<DefaultAsset>();
        private List<string> _foundAssetPaths = new List<string>();

        [SerializeField] private string _filterText = "";
        [SerializeField] private AssetTypeFilter _filterTypeMask = AssetTypeFilter.All;
        [SerializeField] private DefaultAsset _moveTargetFolder;

        private SerializedObject _serializedObject;

        // UI references
        private VisualElement _resultsContainer;
        private Label _resultsCountLabel;
        private TextField _filterTextField;
        private ObjectField _moveFolderField;
        private Button _moveBtn;
        private List<(AssetTypeFilter flag, Toggle toggle)> _typeToggles = new List<(AssetTypeFilter, Toggle)>();
        #endregion

        #region Menu Item
        [MenuItem("NamPhuThuy/Assets Pipeline/Window - AssetFilter")]
        public static void ShowWindow()
        {
            var window = GetWindow<Window_AssetFilter>("Asset Filter");
            window.minSize = new Vector2(600, 500);
            window.Show();
        }
        #endregion

        #region Unity Callbacks
        private void OnEnable()
        {
            Debug.Log("<color=#3B82F6>[Window_AssetFilter]</color> OnEnable");
            _serializedObject = new SerializedObject(this);

            // Load persisted state
            _filterText = EditorPrefs.GetString(PREF_KEY_FILTER_TEXT, "");
            _filterTypeMask = (AssetTypeFilter)EditorPrefs.GetInt(PREF_KEY_FILTER_MASK, (int)AssetTypeFilter.All);
        }

        private void OnDisable()
        {
            Debug.Log("<color=#3B82F6>[Window_AssetFilter]</color> OnDisable");
            // Save persisted state
            EditorPrefs.SetString(PREF_KEY_FILTER_TEXT, _filterText);
            EditorPrefs.SetInt(PREF_KEY_FILTER_MASK, (int)_filterTypeMask);
        }

        public void CreateGUI()
        {
            Debug.Log("<color=#3B82F6>[Window_AssetFilter]</color> CreateGUI");
            var root = rootVisualElement;
            root.style.backgroundColor = COLOR_EDITOR_BG;
            root.style.paddingLeft = 14;
            root.style.paddingRight = 14;
            root.style.paddingTop = 14;
            root.style.paddingBottom = 14;

            // 1. Signature Header Row
            root.Add(BuildHeader());

            // Separator
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

            var helpBox = new HelpBox("Filter and batch move assets within selected folders.", HelpBoxMessageType.Info);
            root.Add(helpBox);

            // Scroll View
            var mainScroll = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1, marginTop = 10 } };
            root.Add(mainScroll);

            mainScroll.Add(BuildFoldersSection());
            mainScroll.Add(BuildFilterSection());

            var findBtn = new Button(FindAssets)
            {
                text = "Find Assets",
                style =
                {
                    height = 35,
                    marginTop = 4,
                    marginBottom = 12,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    backgroundColor = COLOR_OCEAN_BLUE,
                    color = Color.white,
                    borderTopLeftRadius = 4, borderTopRightRadius = 4, borderBottomLeftRadius = 4, borderBottomRightRadius = 4
                }
            };
            mainScroll.Add(findBtn);

            mainScroll.Add(BuildResultsSection());
            mainScroll.Add(BuildResetSection());

            // Initial UI sync
            RefreshResultsUI();
        }
        #endregion

        #region UI Builders
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

            var signatureMark = new VisualElement
            {
                style =
                {
                    width = 44,
                    height = 44,
                    marginRight = 12,
                    borderTopLeftRadius = 6, borderTopRightRadius = 6, borderBottomLeftRadius = 6, borderBottomRightRadius = 6
                }
            };

            string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
            string scriptDir = Path.GetDirectoryName(scriptPath);
            string combinedPath = Path.Combine(scriptDir, SIGNATURE_MARK_RELATIVE_PATH);
            string fullPath = Path.GetFullPath(combinedPath).Replace("\\", "/");
            string resolvedPath = "Assets" + fullPath.Substring(Application.dataPath.Length);

            var signatureTex = AssetDatabase.LoadAssetAtPath<Texture2D>(resolvedPath);
            if (signatureTex != null)
            {
                signatureMark.style.backgroundImage = signatureTex;
            }
            else
            {
                signatureMark.style.backgroundColor = COLOR_GREY_BOX;
            }
            headerRow.Add(signatureMark);

            var textColumn = new VisualElement { style = { flexGrow = 1 } };
            var mainTitle = new Label(WINDOW_TITLE)
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 16,
                    color = COLOR_SKY_BLUE
                }
            };
            var subTitle = new Label("Batch locate, filter, and organize project assets")
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

        private VisualElement BuildBox(string titleText = "")
        {
            var box = new VisualElement();
            box.style.borderTopWidth = 1; box.style.borderBottomWidth = 1; box.style.borderLeftWidth = 1; box.style.borderRightWidth = 1;
            box.style.borderTopColor = COLOR_GREY_BORDER; box.style.borderBottomColor = COLOR_GREY_BORDER;
            box.style.borderLeftColor = COLOR_GREY_BORDER; box.style.borderRightColor = COLOR_GREY_BORDER;
            box.style.borderTopLeftRadius = 4; box.style.borderTopRightRadius = 4;
            box.style.borderBottomLeftRadius = 4; box.style.borderBottomRightRadius = 4;
            box.style.paddingLeft = 12; box.style.paddingRight = 12; box.style.paddingTop = 12; box.style.paddingBottom = 12;
            box.style.backgroundColor = COLOR_GREY_BOX;
            box.style.marginBottom = 12;

            if (!string.IsNullOrEmpty(titleText))
            {
                var title = new Label(titleText)
                {
                    style =
                    {
                        unityFontStyleAndWeight = FontStyle.Bold,
                        fontSize = 12,
                        color = COLOR_SKY_BLUE,
                        marginBottom = 8
                    }
                };
                box.Add(title);
            }

            return box;
        }

        private VisualElement BuildFoldersSection()
        {
            if (_serializedObject == null) _serializedObject = new SerializedObject(this);
            return UITK_AssetPipelineHelper.BuildAssetListSection<DefaultAsset>(
                _serializedObject,
                "_targetFolders",
                "Search Folders",
                "Target Folders",
                _targetFolders,
                () => { }
            );
        }

        private VisualElement BuildFilterSection()
        {
            var box = BuildBox("Filters");

            var row1 = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            row1.Add(new Label("Filter") { style = { width = 45, color = COLOR_FOREST_MIST } });
            _filterTextField = new TextField { value = _filterText, style = { flexGrow = 1 } };
            _filterTextField.RegisterValueChangedCallback(e => { _filterText = e.newValue; RefreshResultsUI(); });
            row1.Add(_filterTextField);
            row1.Add(new Button(() => { _filterTextField.value = ""; _filterTypeMask = AssetTypeFilter.All; UpdateTypeToggles(); RefreshResultsUI(); }) 
            { 
                text = "Clear", 
                style = { 
                    width = 50,
                    backgroundColor = COLOR_GREY_BOX,
                    color = COLOR_FOREST_MIST,
                    borderTopWidth = 1, borderBottomWidth = 1, borderLeftWidth = 1, borderRightWidth = 1,
                    borderTopColor = COLOR_GREY_BORDER, borderBottomColor = COLOR_GREY_BORDER, borderLeftColor = COLOR_GREY_BORDER, borderRightColor = COLOR_GREY_BORDER,
                    borderTopLeftRadius = 4, borderTopRightRadius = 4, borderBottomLeftRadius = 4, borderBottomRightRadius = 4
                } 
            });
            box.Add(row1);

            var row2 = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, flexWrap = Wrap.Wrap, marginTop = 6 } };
            row2.Add(new Label("Types") { style = { width = 45, color = COLOR_FOREST_MIST } });
            
            row2.Add(new Button(() => { _filterTypeMask = AssetTypeFilter.All; UpdateTypeToggles(); RefreshResultsUI(); }) 
            { 
                text = "All",
                style = {
                    height = 20,
                    fontSize = 10,
                    backgroundColor = COLOR_GREY_BOX,
                    color = COLOR_FOREST_MIST,
                    borderTopWidth = 1, borderBottomWidth = 1, borderLeftWidth = 1, borderRightWidth = 1,
                    borderTopColor = COLOR_GREY_BORDER, borderBottomColor = COLOR_GREY_BORDER, borderLeftColor = COLOR_GREY_BORDER, borderRightColor = COLOR_GREY_BORDER,
                    borderTopLeftRadius = 4, borderTopRightRadius = 4, borderBottomLeftRadius = 4, borderBottomRightRadius = 4
                }
            });
            row2.Add(new Button(() => { _filterTypeMask = 0; UpdateTypeToggles(); RefreshResultsUI(); }) 
            { 
                text = "None",
                style = {
                    height = 20,
                    fontSize = 10,
                    backgroundColor = COLOR_GREY_BOX,
                    color = COLOR_FOREST_MIST,
                    borderTopWidth = 1, borderBottomWidth = 1, borderLeftWidth = 1, borderRightWidth = 1,
                    borderTopColor = COLOR_GREY_BORDER, borderBottomColor = COLOR_GREY_BORDER, borderLeftColor = COLOR_GREY_BORDER, borderRightColor = COLOR_GREY_BORDER,
                    borderTopLeftRadius = 4, borderTopRightRadius = 4, borderBottomLeftRadius = 4, borderBottomRightRadius = 4,
                    marginLeft = 4
                }
            });

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
            box.style.flexGrow = 1;

            var headerRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 6 } };
            _resultsCountLabel = new Label("Results (0)") { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 12, color = COLOR_SKY_BLUE } };
            headerRow.Add(_resultsCountLabel);
            headerRow.Add(new VisualElement { style = { flexGrow = 1 } });
            
            headerRow.Add(new Label("To Folder:") { style = { width = 70, unityTextAlign = TextAnchor.MiddleRight, marginRight = 5, color = COLOR_FOREST_MIST } });
            _moveFolderField = new ObjectField { objectType = typeof(DefaultAsset), allowSceneObjects = false, value = _moveTargetFolder, style = { width = 160 } };
            _moveFolderField.RegisterValueChangedCallback(e => { _moveTargetFolder = e.newValue as DefaultAsset; RefreshResultsUI(); });
            headerRow.Add(_moveFolderField);

            _moveBtn = new Button(() => MoveResultsToFolder(GetFilteredPaths(_foundAssetPaths))) 
            { 
                text = "Move Selected", 
                style = { 
                    width = 120, 
                    height = 24, 
                    unityFontStyleAndWeight = FontStyle.Bold,
                    backgroundColor = COLOR_OCEAN_BLUE,
                    color = Color.white,
                    borderTopLeftRadius = 4, borderTopRightRadius = 4, borderBottomLeftRadius = 4, borderBottomRightRadius = 4
                } 
            };
            headerRow.Add(_moveBtn);
            box.Add(headerRow);

            var scroll = new ScrollView { style = { flexGrow = 1, minHeight = 240 } };
            _resultsContainer = new VisualElement();
            scroll.Add(_resultsContainer);
            box.Add(scroll);

            return box;
        }

        private VisualElement BuildResetSection()
        {
            var resetBox = BuildBox("Danger Zone / Options");
            resetBox.style.borderTopColor = COLOR_DANGER_BORDER;
            resetBox.style.borderBottomColor = COLOR_DANGER_BORDER;
            resetBox.style.borderLeftColor = COLOR_DANGER_BORDER;
            resetBox.style.borderRightColor = COLOR_DANGER_BORDER;

            var resetBtn = new Button(ResetToDefaults)
            {
                text = "Reset Configurations to Defaults",
                style =
                {
                    height = 28,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    backgroundColor = COLOR_DANGER_BG,
                    borderTopLeftRadius = 4, borderTopRightRadius = 4, borderBottomLeftRadius = 4, borderBottomRightRadius = 4
                }
            };
            resetBox.Add(resetBtn);
            return resetBox;
        }
        #endregion

        #region Core Actions
        private void FindAssets()
        {
            _foundAssetPaths.Clear();
            var validFolderPaths = _targetFolders
                .Where(f => f != null)
                .Select(f => AssetDatabase.GetAssetPath(f))
                .Where(p => AssetDatabase.IsValidFolder(p))
                .Distinct()
                .ToArray();

            if (validFolderPaths.Length == 0)
            {
                Debug.LogWarning("<color=orange>[Window_AssetFilter]</color> Search folders target list is empty.");
                RefreshResultsUI();
                return;
            }

            EditorUtility.DisplayProgressBar("Scanning", "Scanning target folders...", 0.5f);

            string[] guids = AssetDatabase.FindAssets("", validFolderPaths);
            
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(path)) continue; 
                _foundAssetPaths.Add(path);
            }

            _foundAssetPaths = _foundAssetPaths.Distinct().ToList();

            EditorUtility.ClearProgressBar();
            
            Debug.Log($"<color=#3B82F6>[Window_AssetFilter]</color> Success: Scanned and found {_foundAssetPaths.Count} assets.");
            
            RefreshResultsUI();
        }

        private void RefreshResultsUI()
        {
            if (_resultsContainer == null) return;
            _resultsContainer.Clear();

            var filteredPaths = GetFilteredPaths(_foundAssetPaths);
            _resultsCountLabel.text = $"Results ({filteredPaths.Count})";

            bool canMove = _moveTargetFolder != null && filteredPaths.Count > 0
                           && AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(_moveTargetFolder));
            _moveBtn.SetEnabled(canMove);

            if (filteredPaths.Count == 0)
            {
                _resultsContainer.Add(new Label("(No matching assets found)") { style = { color = Color.gray, unityFontStyleAndWeight = FontStyle.Italic, marginTop = 10 } });
                return;
            }

            for (int i = 0; i < filteredPaths.Count; i++)
            {
                string refPath = filteredPaths[i];
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 4 } };

                Object refAsset = AssetDatabase.LoadMainAssetAtPath(refPath);
                if (refAsset != null)
                {
                    Texture icon = AssetDatabase.GetCachedIcon(refPath);
                    var iconEl = new VisualElement { style = { width = 16, height = 16, backgroundImage = icon as Texture2D, marginRight = 6 } };
                    iconEl.RegisterCallback<MouseDownEvent>(e => { EditorGUIUtility.PingObject(refAsset); });
                    row.Add(iconEl);

                    var field = new ObjectField { objectType = typeof(Object), value = refAsset, style = { flexGrow = 1 } };
                    field.SetEnabled(false);
                    row.Add(field);

                    row.Add(new Button(() => { Selection.activeObject = refAsset; EditorGUIUtility.PingObject(refAsset); }) 
                    { 
                        text = "Select", 
                        style = { 
                            width = 50,
                            backgroundColor = COLOR_GREY_BOX,
                            color = COLOR_FOREST_MIST,
                            borderTopWidth = 1, borderBottomWidth = 1, borderLeftWidth = 1, borderRightWidth = 1,
                            borderTopColor = COLOR_GREY_BORDER, borderBottomColor = COLOR_GREY_BORDER, borderLeftColor = COLOR_GREY_BORDER, borderRightColor = COLOR_GREY_BORDER,
                            borderTopLeftRadius = 4, borderTopRightRadius = 4, borderBottomLeftRadius = 4, borderBottomRightRadius = 4
                        } 
                    });
                }
                else
                {
                    row.Add(new Label(refPath) { style = { color = Color.gray, flexGrow = 1 } });
                }

                _resultsContainer.Add(row);
            }
        }

        private void MoveResultsToFolder(List<string> pathsToMove)
        {
            string targetFolderPath = AssetDatabase.GetAssetPath(_moveTargetFolder);

            if (!AssetDatabase.IsValidFolder(targetFolderPath))
            {
                Debug.LogError("<color=red>[Window_AssetFilter]</color> Selection Null: Invalid target folder.");
                return;
            }

            var assetsToMove = pathsToMove
                .Where(p => !p.StartsWith(targetFolderPath + "/"))
                .Distinct()
                .ToList();

            if (assetsToMove.Count == 0)
            {
                Debug.LogWarning("<color=orange>[Window_AssetFilter]</color> Already in folder.");
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "Confirm Move",
                $"Are you sure you want to move {assetsToMove.Count} assets to '{targetFolderPath}'?",
                "Move", "Cancel");

            if (!confirmed) return;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Move Assets to Folder");
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
                        $"Moving {i + 1}/{assetsToMove.Count}...",
                        (float)i / assetsToMove.Count);

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
                    if (!string.IsNullOrEmpty(error))
                    {
                        failedCount++;
                        Debug.LogWarning($"Failed to move {sourcePath} → {destPath}: {error}");
                        continue;
                    }

                    movedCount++;
                    pathMapping[sourcePath] = destPath;
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Undo.CollapseUndoOperations(undoGroup);

            // Update stored paths so the results panel stays accurate
            for (int i = 0; i < _foundAssetPaths.Count; i++)
            {
                if (pathMapping.TryGetValue(_foundAssetPaths[i], out string newPath))
                {
                    _foundAssetPaths[i] = newPath;
                }
            }

            Debug.Log($"<color=green>[Window_AssetFilter]</color> Success: Batch moved {movedCount} asset(s) to '{targetFolderPath}' (Failed: {failedCount})");
            RefreshResultsUI();
        }

        private void ResetToDefaults()
        {
            Debug.Log("<color=red>[Window_AssetFilter]</color> ResetToDefaults");

            // Clear persisted cache
            EditorPrefs.DeleteKey(PREF_KEY_FILTER_TEXT);
            EditorPrefs.DeleteKey(PREF_KEY_FILTER_MASK);

            // Re-init variables in memory
            _filterText = "";
            _filterTypeMask = AssetTypeFilter.All;
            _moveTargetFolder = null;
            
            if (_targetFolders != null) _targetFolders.Clear();
            if (_foundAssetPaths != null) _foundAssetPaths.Clear();

            Close();
            ShowWindow();
        }
        #endregion

        #region Helpers
        private List<string> GetFilteredPaths(List<string> paths)
        {
            if (_filterTypeMask == 0) return new List<string>();

            IEnumerable<string> result = paths;

            if (_filterTypeMask != AssetTypeFilter.All)
            {
                result = result.Where(p => MatchesTypeMask(p, _filterTypeMask));
            }

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

        private void UpdateTypeToggles()
        {
            foreach (var t in _typeToggles)
            {
                t.toggle.SetValueWithoutNotify((_filterTypeMask & t.flag) != 0);
            }
        }
        #endregion
    }
}
#endif