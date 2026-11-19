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

#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEditorInternal;
using UnityEngine.SceneManagement;
using UGUIButton = UnityEngine.UI.Button;
using UGUIGraphic = UnityEngine.UI.Graphic;
using UGUISelectable = UnityEngine.UI.Selectable;
using UGUIText = UnityEngine.UI.Text;
#endif

namespace NamPhuThuy.AssetPipelineTools
{
#if UNITY_EDITOR
    /// <summary>
    /// Replaces button GameObjects (scene or open prefab) with instances of a button prefab, keeping their hierarchy setup:
    /// parent + sibling index, name, RectTransform layout, active / layer / tag, Button onClick / interactable / navigation,
    /// label text, components the prefab lacks, and every reference to the old button in the same scene / prefab.
    /// One Undo step (Ctrl+Z) reverts the whole batch.
    /// </summary>
    public class Window_ButtonPrefabReplacer : EditorWindow
    {
        #region Enums (Rule 3)
        public enum TabType
        {
            NONE = 0,
            REPLACE = 1,
            OPTIONS = 2
        }
        #endregion

        #region Private Fields
        [SerializeField] private GameObject _replacementPrefab;
        [SerializeField] private List<GameObject> _targets = new List<GameObject>();

        [SerializeField] private bool _keepName = DEFAULT_KEEP_NAME;
        [SerializeField] private bool _keepRectLayout = DEFAULT_KEEP_RECT_LAYOUT;
        [SerializeField] private bool _keepSize = DEFAULT_KEEP_SIZE;
        [SerializeField] private bool _keepActiveLayerTag = DEFAULT_KEEP_ACTIVE_LAYER_TAG;
        [SerializeField] private bool _copyButtonSetup = DEFAULT_COPY_BUTTON_SETUP;
        [SerializeField] private bool _copyLabelText = DEFAULT_COPY_LABEL_TEXT;
        [SerializeField] private bool _copyExtraComponents = DEFAULT_COPY_EXTRA_COMPONENTS;
        [SerializeField] private bool _moveOldChildren = DEFAULT_MOVE_OLD_CHILDREN;
        [SerializeField] private bool _remapReferences = DEFAULT_REMAP_REFERENCES;

        private const bool DEFAULT_KEEP_NAME = true;
        private const bool DEFAULT_KEEP_RECT_LAYOUT = true;
        private const bool DEFAULT_KEEP_SIZE = true;
        private const bool DEFAULT_KEEP_ACTIVE_LAYER_TAG = true;
        private const bool DEFAULT_COPY_BUTTON_SETUP = true;
        private const bool DEFAULT_COPY_LABEL_TEXT = true;
        private const bool DEFAULT_COPY_EXTRA_COMPONENTS = true;
        private const bool DEFAULT_MOVE_OLD_CHILDREN = false;
        private const bool DEFAULT_REMAP_REFERENCES = true;

        // EditorPrefs keys (Rule 4)
        private const string PREF_KEY_PREFAB_PATH = "NamPhuThuy_ButtonPrefabReplacer_PrefabPath";
        private const string PREF_KEY_ACTIVE_TAB = "NamPhuThuy_ButtonPrefabReplacer_ActiveTab";
        private const string PREF_KEY_KEEP_NAME = "NamPhuThuy_ButtonPrefabReplacer_KeepName";
        private const string PREF_KEY_KEEP_RECT_LAYOUT = "NamPhuThuy_ButtonPrefabReplacer_KeepRectLayout";
        private const string PREF_KEY_KEEP_SIZE = "NamPhuThuy_ButtonPrefabReplacer_KeepSize";
        private const string PREF_KEY_KEEP_ACTIVE_LAYER_TAG = "NamPhuThuy_ButtonPrefabReplacer_KeepActiveLayerTag";
        private const string PREF_KEY_COPY_BUTTON_SETUP = "NamPhuThuy_ButtonPrefabReplacer_CopyButtonSetup";
        private const string PREF_KEY_COPY_LABEL_TEXT = "NamPhuThuy_ButtonPrefabReplacer_CopyLabelText";
        private const string PREF_KEY_COPY_EXTRA_COMPONENTS = "NamPhuThuy_ButtonPrefabReplacer_CopyExtraComponents";
        private const string PREF_KEY_MOVE_OLD_CHILDREN = "NamPhuThuy_ButtonPrefabReplacer_MoveOldChildren";
        private const string PREF_KEY_REMAP_REFERENCES = "NamPhuThuy_ButtonPrefabReplacer_RemapReferences";

        // Paths, names & signature config (Rule 4)
        private const string SIGNATURE_MARK_RELATIVE_PATH = "../../UP_Common/nam_phu_thuy.png";
        private const string WINDOW_TITLE = "Button Prefab Replacer";
        private const string UNDO_NAME = "Replace Buttons With Prefab";
        private const string BUTTON_ON_CLICK_PROPERTY = "m_OnClick";
        private const string BUTTON_INTERACTABLE_PROPERTY = "m_Interactable";
        private const string BUTTON_NAVIGATION_PROPERTY = "m_Navigation";
        private const string LOG_TAG = "<color=#3B82F6>[ButtonPrefabReplacer]</color>";
        private const string LOG_TAG_SUCCESS = "<color=green>[ButtonPrefabReplacer]</color>";
        private const string LOG_TAG_WARNING = "<color=orange>[ButtonPrefabReplacer]</color>";
        private const string LOG_TAG_ERROR = "<color=red>[ButtonPrefabReplacer]</color>";

        private static Color COLOR_EDITOR_BG => EditorGUIUtility.isProSkin
            ? new Color(0.22f, 0.22f, 0.22f, 1f)
            : new Color(0.78f, 0.78f, 0.78f, 1f);

        private static Color COLOR_GREY_BOX => EditorGUIUtility.isProSkin
            ? new Color(0.16f, 0.16f, 0.16f, 0.6f)
            : new Color(0.72f, 0.72f, 0.72f, 0.4f);

        private static Color COLOR_GREY_BORDER => EditorGUIUtility.isProSkin
            ? new Color(0.26f, 0.26f, 0.26f, 0.8f)
            : new Color(0.60f, 0.60f, 0.60f, 0.8f);

        private static Color COLOR_OCEAN_BLUE => EditorGUIUtility.isProSkin
            ? new Color(0.0f, 0.47f, 0.74f, 1f)
            : new Color(0.05f, 0.42f, 0.70f, 1f);

        private static Color COLOR_SKY_BLUE => EditorGUIUtility.isProSkin
            ? new Color(0.53f, 0.80f, 0.92f, 1f)
            : new Color(0.08f, 0.45f, 0.72f, 1f);

        private static Color COLOR_FOREST_MIST => EditorGUIUtility.isProSkin
            ? new Color(0.8f, 0.8f, 0.8f, 1f)
            : new Color(0.18f, 0.18f, 0.18f, 1f);

        private static Color COLOR_TAB_INACTIVE_BG => EditorGUIUtility.isProSkin
            ? new Color(0.16f, 0.16f, 0.16f, 1f)
            : new Color(0.82f, 0.82f, 0.82f, 1f);

        private static Color COLOR_TAB_INACTIVE_BORDER => EditorGUIUtility.isProSkin
            ? new Color(0.11f, 0.11f, 0.11f, 1f)
            : new Color(0.65f, 0.65f, 0.65f, 1f);

        private static Color COLOR_DANGER_BG => EditorGUIUtility.isProSkin
            ? new Color(0.55f, 0.15f, 0.15f, 1f)
            : new Color(0.75f, 0.20f, 0.20f, 1f);

        private static Color COLOR_DANGER_BORDER => EditorGUIUtility.isProSkin
            ? new Color(0.6f, 0.2f, 0.2f, 0.8f)
            : new Color(0.65f, 0.18f, 0.18f, 0.8f);

        // Tab state
        private TabType _activeTab = TabType.REPLACE;

        // UI references
        private VisualElement _contentContainer;
        private VisualElement _tabHeaderContainer;
        private SerializedObject _serializedWindow;
        #endregion

        #region Menu Item
        [MenuItem("NamPhuThuy/Assets Pipeline/Window - Button Prefab Replacer")]
        public static void ShowWindow()
        {
            var window = GetWindow<Window_ButtonPrefabReplacer>(WINDOW_TITLE);
            window.minSize = new Vector2(500, 650);
            window.Show();
        }
        #endregion

        #region Unity Callbacks
        private void OnEnable()
        {
            Debug.Log($"{LOG_TAG} OnEnable");

            _activeTab = (TabType)EditorPrefs.GetInt(PREF_KEY_ACTIVE_TAB, (int)TabType.REPLACE);
            _keepName = EditorPrefs.GetBool(PREF_KEY_KEEP_NAME, DEFAULT_KEEP_NAME);
            _keepRectLayout = EditorPrefs.GetBool(PREF_KEY_KEEP_RECT_LAYOUT, DEFAULT_KEEP_RECT_LAYOUT);
            _keepSize = EditorPrefs.GetBool(PREF_KEY_KEEP_SIZE, DEFAULT_KEEP_SIZE);
            _keepActiveLayerTag = EditorPrefs.GetBool(PREF_KEY_KEEP_ACTIVE_LAYER_TAG, DEFAULT_KEEP_ACTIVE_LAYER_TAG);
            _copyButtonSetup = EditorPrefs.GetBool(PREF_KEY_COPY_BUTTON_SETUP, DEFAULT_COPY_BUTTON_SETUP);
            _copyLabelText = EditorPrefs.GetBool(PREF_KEY_COPY_LABEL_TEXT, DEFAULT_COPY_LABEL_TEXT);
            _copyExtraComponents = EditorPrefs.GetBool(PREF_KEY_COPY_EXTRA_COMPONENTS, DEFAULT_COPY_EXTRA_COMPONENTS);
            _moveOldChildren = EditorPrefs.GetBool(PREF_KEY_MOVE_OLD_CHILDREN, DEFAULT_MOVE_OLD_CHILDREN);
            _remapReferences = EditorPrefs.GetBool(PREF_KEY_REMAP_REFERENCES, DEFAULT_REMAP_REFERENCES);

            string savedPrefabPath = EditorPrefs.GetString(PREF_KEY_PREFAB_PATH, "");
            if (!string.IsNullOrEmpty(savedPrefabPath))
            {
                _replacementPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(savedPrefabPath);
            }
        }

        private void OnDisable()
        {
            Debug.Log($"{LOG_TAG} OnDisable");

            EditorPrefs.SetInt(PREF_KEY_ACTIVE_TAB, (int)_activeTab);
            EditorPrefs.SetBool(PREF_KEY_KEEP_NAME, _keepName);
            EditorPrefs.SetBool(PREF_KEY_KEEP_RECT_LAYOUT, _keepRectLayout);
            EditorPrefs.SetBool(PREF_KEY_KEEP_SIZE, _keepSize);
            EditorPrefs.SetBool(PREF_KEY_KEEP_ACTIVE_LAYER_TAG, _keepActiveLayerTag);
            EditorPrefs.SetBool(PREF_KEY_COPY_BUTTON_SETUP, _copyButtonSetup);
            EditorPrefs.SetBool(PREF_KEY_COPY_LABEL_TEXT, _copyLabelText);
            EditorPrefs.SetBool(PREF_KEY_COPY_EXTRA_COMPONENTS, _copyExtraComponents);
            EditorPrefs.SetBool(PREF_KEY_MOVE_OLD_CHILDREN, _moveOldChildren);
            EditorPrefs.SetBool(PREF_KEY_REMAP_REFERENCES, _remapReferences);
            EditorPrefs.SetString(PREF_KEY_PREFAB_PATH, _replacementPrefab != null ? AssetDatabase.GetAssetPath(_replacementPrefab) : "");
        }

        public void CreateGUI()
        {
            Debug.Log($"{LOG_TAG} CreateGUI");

            _serializedWindow = new SerializedObject(this);

            var root = rootVisualElement;
            root.style.backgroundColor = COLOR_EDITOR_BG;
            root.style.paddingLeft = 14;
            root.style.paddingRight = 14;
            root.style.paddingTop = 14;
            root.style.paddingBottom = 14;

            root.Add(BuildHeader());

            _tabHeaderContainer = BuildNavigation();
            root.Add(_tabHeaderContainer);

            root.Add(new VisualElement
            {
                style =
                {
                    height = 2,
                    backgroundColor = COLOR_GREY_BORDER,
                    marginTop = 4,
                    marginBottom = 12
                }
            });

            _contentContainer = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1 } };
            root.Add(_contentContainer);

            RefreshRegion();
        }
        #endregion

        #region Layout Builders
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
                    borderTopLeftRadius = 6,
                    borderTopRightRadius = 6,
                    borderBottomLeftRadius = 6,
                    borderBottomRightRadius = 6
                }
            };

            string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
            string scriptDir = Path.GetDirectoryName(scriptPath);
            string fullPath = Path.GetFullPath(Path.Combine(scriptDir, SIGNATURE_MARK_RELATIVE_PATH)).Replace("\\", "/");
            string resolvedPath = "Assets" + fullPath.Substring(Application.dataPath.Length);

            var signatureTex = AssetDatabase.LoadAssetAtPath<Texture2D>(resolvedPath);
            if (signatureTex != null)
            {
                signatureMark.style.backgroundImage = signatureTex;
            }
            else
            {
                Debug.LogWarning($"{LOG_TAG_WARNING} BuildHeader: Missing Logo {resolvedPath}");
                signatureMark.style.backgroundColor = COLOR_GREY_BOX;
            }
            headerRow.Add(signatureMark);

            var textColumn = new VisualElement { style = { flexGrow = 1 } };
            textColumn.Add(new Label(WINDOW_TITLE)
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 16,
                    color = COLOR_SKY_BLUE
                }
            });
            textColumn.Add(new Label("Swap button GameObjects for a prefab, keep their hierarchy setup")
            {
                style =
                {
                    fontSize = 11,
                    color = COLOR_FOREST_MIST
                }
            });
            headerRow.Add(textColumn);

            return headerRow;
        }

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

            bar.Add(CreateNavigationButton("Replace", TabType.REPLACE));
            bar.Add(CreateNavigationButton("Options", TabType.OPTIONS));
            return bar;
        }

        private Button CreateNavigationButton(string label, TabType tab)
        {
            bool isActive = _activeTab == tab;
            return new Button(() => SwitchRegion(tab))
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
        }

        private void SwitchRegion(TabType newTab)
        {
            if (_activeTab == newTab) return;
            _activeTab = newTab;

            var parent = _tabHeaderContainer.parent;
            int siblingIdx = parent.IndexOf(_tabHeaderContainer);
            parent.Remove(_tabHeaderContainer);
            _tabHeaderContainer = BuildNavigation();
            parent.Insert(siblingIdx, _tabHeaderContainer);

            RefreshRegion();
        }

        private void RefreshRegion()
        {
            _contentContainer.Clear();
            _serializedWindow.Update();

            switch (_activeTab)
            {
                case TabType.REPLACE:
                    BuildReplaceRegion(_contentContainer);
                    break;
                case TabType.OPTIONS:
                    BuildOptionsRegion(_contentContainer);
                    break;
            }
        }

        private VisualElement BuildStyledBox(string title, Color borderColor)
        {
            var box = UITK_AssetPipelineHelper.BuildBox(title);
            box.style.backgroundColor = COLOR_GREY_BOX;
            box.style.borderTopColor = borderColor;
            box.style.borderBottomColor = borderColor;
            box.style.borderLeftColor = borderColor;
            box.style.borderRightColor = borderColor;
            return box;
        }

        private Button BuildActionButton(string text, System.Action onClick, Color background, float height = 32)
        {
            return new Button(onClick)
            {
                text = text,
                style =
                {
                    height = height,
                    flexGrow = 1,
                    marginLeft = 2,
                    marginRight = 2,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    backgroundColor = background,
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4
                }
            };
        }

        private PropertyField BuildBoundField(string propertyName, string label, string tooltip = null)
        {
            var field = new PropertyField(_serializedWindow.FindProperty(propertyName), label) { tooltip = tooltip };
            field.Bind(_serializedWindow);
            return field;
        }
        #endregion

        #region Page Content Builders
        private void BuildReplaceRegion(VisualElement container)
        {
            var prefabBox = BuildStyledBox("1. Replacement Prefab", COLOR_GREY_BORDER);
            prefabBox.Add(BuildBoundField("_replacementPrefab", "Prefab", "Prefab asset whose instances replace the targets."));
            container.Add(prefabBox);

            var targetsBox = BuildStyledBox("2. Target Buttons (scene or open prefab)", COLOR_GREY_BORDER);
            targetsBox.Add(BuildBoundField("_targets", "Targets"));

            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 8 } };
            row.Add(BuildActionButton("Add Selected", AddSelectedTargets, COLOR_TAB_INACTIVE_BG, 26));
            row.Add(BuildActionButton("Select Targets", SelectTargets, COLOR_TAB_INACTIVE_BG, 26));
            row.Add(BuildActionButton("Clear", ClearTargets, COLOR_TAB_INACTIVE_BG, 26));
            targetsBox.Add(row);
            container.Add(targetsBox);

            var runBox = BuildStyledBox("3. Replace", COLOR_GREY_BORDER);
            runBox.Add(new Label(
                "Kept from each old button (see Options):\n" +
                "• Parent, sibling index, name, active / layer / tag\n" +
                "• RectTransform anchors, pivot, position, size, rotation, scale\n" +
                "• Button onClick, interactable, navigation\n" +
                "• Label text, components the prefab lacks\n" +
                "• References to the old button in the same scene / prefab\n" +
                "Ctrl+Z reverts the whole batch.")
            {
                style =
                {
                    fontSize = 11,
                    color = COLOR_FOREST_MIST,
                    whiteSpace = WhiteSpace.Normal,
                    marginBottom = 8
                }
            });
            runBox.Add(BuildActionButton("Replace With Prefab", ReplaceAll, COLOR_OCEAN_BLUE, 34));
            container.Add(runBox);
        }

        private void BuildOptionsRegion(VisualElement container)
        {
            var keepBox = BuildStyledBox("Keep From Old Button", COLOR_GREY_BORDER);
            keepBox.Add(BuildBoundField("_keepName", "Name"));
            keepBox.Add(BuildBoundField("_keepActiveLayerTag", "Active / Layer / Tag"));
            keepBox.Add(BuildBoundField("_keepRectLayout", "RectTransform Layout", "Anchors, pivot, position, rotation, scale."));
            keepBox.Add(BuildBoundField("_keepSize", "Size (sizeDelta)", "Off: keep the prefab's own size."));
            keepBox.Add(BuildBoundField("_copyButtonSetup", "Button onClick / Interactable / Navigation"));
            keepBox.Add(BuildBoundField("_copyLabelText", "Label Text", "First TMP_Text / Text of the old button → first label of the prefab."));
            keepBox.Add(BuildBoundField("_copyExtraComponents", "Extra Components", "Root components of the old button whose type the prefab root lacks (e.g. LayoutElement)."));
            keepBox.Add(BuildBoundField("_moveOldChildren", "Move Old Children", "Move the old button's children under the new one (badges, icons...). Off: they are deleted."));
            keepBox.Add(BuildBoundField("_remapReferences", "Remap References", "Fields pointing to the old button (GameObject or its root components) now point to the new one."));
            container.Add(keepBox);

            var dangerBox = BuildStyledBox("Options", COLOR_DANGER_BORDER);
            dangerBox.Add(BuildActionButton("Reset To Defaults", ResetToDefaults, COLOR_DANGER_BG));
            container.Add(dangerBox);
        }
        #endregion

        #region Target List
        private void AddSelectedTargets()
        {
            Debug.Log($"{LOG_TAG} AddSelectedTargets: Start");

            int added = 0;
            foreach (GameObject go in Selection.gameObjects)
            {
                if (EditorUtility.IsPersistent(go))
                {
                    Debug.LogWarning($"{LOG_TAG_WARNING} AddSelectedTargets: Skip asset {go.name}");
                    continue;
                }

                if (!_targets.Contains(go))
                {
                    _targets.Add(go);
                    added++;
                }
            }

            _serializedWindow.Update();
            Debug.Log($"{LOG_TAG} AddSelectedTargets: Added {added}");
        }

        private void SelectTargets()
        {
            var selection = new List<Object>();
            foreach (GameObject go in _targets)
            {
                if (go != null) selection.Add(go);
            }

            Selection.objects = selection.ToArray();
            Debug.Log($"{LOG_TAG} SelectTargets: {selection.Count}");
        }

        private void ClearTargets()
        {
            _targets.Clear();
            _serializedWindow.Update();
            Debug.Log($"{LOG_TAG} ClearTargets");
        }
        #endregion

        #region Operations / Methods
        private void ReplaceAll()
        {
            Debug.Log($"{LOG_TAG} ReplaceAll: Start");

            if (_replacementPrefab == null)
            {
                Debug.LogError($"{LOG_TAG_ERROR} ReplaceAll: Missing Prefab");
                return;
            }

            if (!PrefabUtility.IsPartOfPrefabAsset(_replacementPrefab))
            {
                Debug.LogError($"{LOG_TAG_ERROR} ReplaceAll: Prefab must be a prefab asset");
                return;
            }

            List<GameObject> targets = CollectValidTargets();
            if (targets.Count == 0)
            {
                Debug.LogWarning($"{LOG_TAG_WARNING} ReplaceAll: No valid targets");
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(UNDO_NAME);
            int undoGroup = Undo.GetCurrentGroup();

            var created = new List<Object>();
            var dirtyScenes = new HashSet<Scene>();
            int remapped = 0;
            int lost = 0;

            foreach (GameObject target in targets)
            {
                dirtyScenes.Add(target.scene);
                GameObject replacement = ReplaceOne(target, ref remapped, ref lost);
                created.Add(replacement);
            }

            Undo.CollapseUndoOperations(undoGroup);

            foreach (Scene scene in dirtyScenes)
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }

            // Old entries are destroyed: the list now holds the new buttons (Rule 8: cleared, not re-created)
            _targets.Clear();
            foreach (Object go in created) _targets.Add((GameObject)go);
            _serializedWindow.Update();
            Selection.objects = created.ToArray();

            Debug.Log($"{LOG_TAG_SUCCESS} ReplaceAll: Success {created.Count} replaced, {remapped} refs remapped");
            if (lost > 0)
            {
                Debug.LogWarning($"{LOG_TAG_WARNING} ReplaceAll: {lost} refs to deleted old children (see warnings above)");
            }
        }

        /// <summary>Distinct scene / prefab-stage targets that can be deleted; a target inside another target is skipped.</summary>
        private List<GameObject> CollectValidTargets()
        {
            var valid = new List<GameObject>();
            var unique = new HashSet<GameObject>();
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();

            foreach (GameObject go in _targets)
            {
                if (go == null || !unique.Add(go)) continue;

                if (EditorUtility.IsPersistent(go))
                {
                    Debug.LogError($"{LOG_TAG_ERROR} CollectValidTargets: {go.name} is an asset, open it in prefab mode");
                    continue;
                }

                if (stage != null && stage.prefabContentsRoot == go)
                {
                    Debug.LogError($"{LOG_TAG_ERROR} CollectValidTargets: {go.name} is the prefab root");
                    continue;
                }

                if (PrefabUtility.IsPartOfPrefabInstance(go) && PrefabUtility.GetOutermostPrefabInstanceRoot(go) != go)
                {
                    Debug.LogError($"{LOG_TAG_ERROR} CollectValidTargets: {go.name} is inside a prefab instance, edit that prefab");
                    continue;
                }

                valid.Add(go);
            }

            valid.RemoveAll(go =>
            {
                for (Transform parent = go.transform.parent; parent != null; parent = parent.parent)
                {
                    if (valid.Contains(parent.gameObject))
                    {
                        Debug.LogWarning($"{LOG_TAG_WARNING} CollectValidTargets: Skip {go.name}, parent {parent.name} is a target");
                        return true;
                    }
                }

                return false;
            });

            return valid;
        }

        private GameObject ReplaceOne(GameObject oldGo, ref int remapped, ref int lost)
        {
            Debug.Log($"{LOG_TAG} ReplaceOne: {oldGo.name}");

            Transform oldTransform = oldGo.transform;
            Transform parent = oldTransform.parent;
            var newGo = (GameObject)(parent != null
                ? PrefabUtility.InstantiatePrefab(_replacementPrefab, parent)
                : PrefabUtility.InstantiatePrefab(_replacementPrefab, oldGo.scene));
            Undo.RegisterCreatedObjectUndo(newGo, UNDO_NAME);
            newGo.transform.SetSiblingIndex(oldTransform.GetSiblingIndex());

            if (_keepName) newGo.name = oldGo.name;
            if (_keepActiveLayerTag)
            {
                newGo.layer = oldGo.layer;
                newGo.tag = oldGo.tag;
            }

            CopyTransform(oldTransform, newGo.transform);
            if (_copyButtonSetup) CopyButtonSetup(oldGo, newGo);
            if (_copyLabelText) CopyLabelText(oldGo, newGo);
            if (_copyExtraComponents) CopyExtraComponents(oldGo, newGo);
            if (_moveOldChildren) MoveOldChildren(oldGo, newGo);
            if (_remapReferences) RemapReferences(oldGo, newGo, ref remapped, ref lost);

            if (_keepActiveLayerTag) newGo.SetActive(oldGo.activeSelf);
            PrefabUtility.RecordPrefabInstancePropertyModifications(newGo);

            Undo.DestroyObjectImmediate(oldGo);
            return newGo;
        }

        private void CopyTransform(Transform oldTransform, Transform newTransform)
        {
            if (!_keepRectLayout && !_keepSize) return;

            Undo.RecordObject(newTransform, UNDO_NAME);
            if (oldTransform is RectTransform oldRect && newTransform is RectTransform newRect)
            {
                if (_keepRectLayout)
                {
                    newRect.anchorMin = oldRect.anchorMin;
                    newRect.anchorMax = oldRect.anchorMax;
                    newRect.pivot = oldRect.pivot;
                }

                if (_keepSize) newRect.sizeDelta = oldRect.sizeDelta;

                if (_keepRectLayout)
                {
                    newRect.anchoredPosition3D = oldRect.anchoredPosition3D;
                    newRect.localRotation = oldRect.localRotation;
                    newRect.localScale = oldRect.localScale;
                }
            }
            else if (_keepRectLayout)
            {
                newTransform.localPosition = oldTransform.localPosition;
                newTransform.localRotation = oldTransform.localRotation;
                newTransform.localScale = oldTransform.localScale;
            }

            PrefabUtility.RecordPrefabInstancePropertyModifications(newTransform);
        }

        private void CopyButtonSetup(GameObject oldGo, GameObject newGo)
        {
            var oldButton = oldGo.GetComponent<UGUIButton>();
            if (oldButton == null)
            {
                Debug.LogWarning($"{LOG_TAG_WARNING} CopyButtonSetup: No Button on {oldGo.name}");
                return;
            }

            var newButton = newGo.GetComponentInChildren<UGUIButton>(true);
            if (newButton == null)
            {
                Debug.LogError($"{LOG_TAG_ERROR} CopyButtonSetup: Prefab has no Button");
                return;
            }

            var source = new SerializedObject(oldButton);
            var destination = new SerializedObject(newButton);
            destination.CopyFromSerializedProperty(source.FindProperty(BUTTON_ON_CLICK_PROPERTY));
            destination.CopyFromSerializedProperty(source.FindProperty(BUTTON_INTERACTABLE_PROPERTY));
            destination.CopyFromSerializedProperty(source.FindProperty(BUTTON_NAVIGATION_PROPERTY));
            destination.ApplyModifiedProperties();
        }

        private void CopyLabelText(GameObject oldGo, GameObject newGo)
        {
            string text = GetLabelText(oldGo);
            if (text == null) return;

            var newTmp = newGo.GetComponentInChildren<TMP_Text>(true);
            if (newTmp != null)
            {
                Undo.RecordObject(newTmp, UNDO_NAME);
                newTmp.text = text;
                PrefabUtility.RecordPrefabInstancePropertyModifications(newTmp);
                return;
            }

            var newText = newGo.GetComponentInChildren<UGUIText>(true);
            if (newText != null)
            {
                Undo.RecordObject(newText, UNDO_NAME);
                newText.text = text;
                PrefabUtility.RecordPrefabInstancePropertyModifications(newText);
                return;
            }

            Debug.LogWarning($"{LOG_TAG_WARNING} CopyLabelText: Prefab has no label");
        }

        private static string GetLabelText(GameObject go)
        {
            var tmp = go.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null) return tmp.text;

            var legacy = go.GetComponentInChildren<UGUIText>(true);
            return legacy != null ? legacy.text : null;
        }

        /// <summary>
        /// Root components of the old button whose type the new root lacks (missing scripts skipped).
        /// Visuals and the Button itself come from the prefab: Graphic, CanvasRenderer and Selectable are never copied.
        /// </summary>
        private void CopyExtraComponents(GameObject oldGo, GameObject newGo)
        {
            int copied = 0;
            foreach (Component component in oldGo.GetComponents<Component>())
            {
                if (component == null || component is Transform || component is CanvasRenderer) continue;
                if (component is UGUIGraphic || component is UGUISelectable) continue;
                if (newGo.GetComponent(component.GetType()) != null) continue;

                if (ComponentUtility.CopyComponent(component) && ComponentUtility.PasteComponentAsNew(newGo))
                {
                    copied++;
                }
                else
                {
                    Debug.LogWarning($"{LOG_TAG_WARNING} CopyExtraComponents: Failed {component.GetType().Name}");
                }
            }

            if (copied > 0) Debug.Log($"{LOG_TAG} CopyExtraComponents: {copied}");
        }

        private static void MoveOldChildren(GameObject oldGo, GameObject newGo)
        {
            var children = new List<Transform>();
            foreach (Transform child in oldGo.transform) children.Add(child);

            foreach (Transform child in children)
            {
                Undo.SetTransformParent(child, newGo.transform, UNDO_NAME);
            }
        }

        /// <summary>
        /// Points every field of the scene / open prefab that referenced the old GameObject (or one of its root components)
        /// to the new one. Counts fields left pointing to old children, which are deleted with the old button.
        /// </summary>
        private void RemapReferences(GameObject oldGo, GameObject newGo, ref int remapped, ref int lost)
        {
            var map = new Dictionary<Object, Object> { { oldGo, newGo } };
            foreach (Component oldComponent in oldGo.GetComponents<Component>())
            {
                if (oldComponent == null) continue;
                // The prefab may keep its Button / Image on a child: fall back to the first one in children
                Component newComponent = newGo.GetComponent(oldComponent.GetType());
                if (newComponent == null) newComponent = newGo.GetComponentInChildren(oldComponent.GetType(), true);
                if (newComponent != null) map[oldComponent] = newComponent;
            }

            // Objects deleted with the old button (children still under it)
            var doomed = new HashSet<Object>();
            foreach (Transform child in oldGo.GetComponentsInChildren<Transform>(true))
            {
                if (child == oldGo.transform) continue;
                doomed.Add(child.gameObject);
                foreach (Component component in child.GetComponents<Component>())
                {
                    if (component != null) doomed.Add(component);
                }
            }

            foreach (GameObject root in GetScopeRoots(oldGo))
            {
                foreach (Component component in root.GetComponentsInChildren<Component>(true))
                {
                    if (component == null || component.transform.IsChildOf(oldGo.transform)) continue;

                    var serialized = new SerializedObject(component);
                    SerializedProperty property = serialized.GetIterator();
                    bool isChanged = false;
                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference) continue;

                        Object value = property.objectReferenceValue;
                        if (value == null) continue;

                        if (map.TryGetValue(value, out Object replacement))
                        {
                            property.objectReferenceValue = replacement;
                            isChanged = true;
                            remapped++;
                        }
                        else if (doomed.Contains(value))
                        {
                            lost++;
                            Debug.LogWarning($"{LOG_TAG_WARNING} RemapReferences: {component.name}.{component.GetType().Name}.{property.propertyPath} → deleted {value.name}", component);
                        }
                    }

                    if (isChanged) serialized.ApplyModifiedProperties();
                }
            }
        }

        private static IEnumerable<GameObject> GetScopeRoots(GameObject go)
        {
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.scene == go.scene)
            {
                return new[] { stage.prefabContentsRoot };
            }

            return go.scene.GetRootGameObjects();
        }

        /// <summary>Rule 7 / 8 / 11: clears the saved prefs and the in-memory values, then reopens the window.</summary>
        private void ResetToDefaults()
        {
            Debug.Log($"{LOG_TAG_ERROR} ResetToDefaults");

            EditorPrefs.DeleteKey(PREF_KEY_PREFAB_PATH);
            EditorPrefs.DeleteKey(PREF_KEY_ACTIVE_TAB);
            EditorPrefs.DeleteKey(PREF_KEY_KEEP_NAME);
            EditorPrefs.DeleteKey(PREF_KEY_KEEP_RECT_LAYOUT);
            EditorPrefs.DeleteKey(PREF_KEY_KEEP_SIZE);
            EditorPrefs.DeleteKey(PREF_KEY_KEEP_ACTIVE_LAYER_TAG);
            EditorPrefs.DeleteKey(PREF_KEY_COPY_BUTTON_SETUP);
            EditorPrefs.DeleteKey(PREF_KEY_COPY_LABEL_TEXT);
            EditorPrefs.DeleteKey(PREF_KEY_COPY_EXTRA_COMPONENTS);
            EditorPrefs.DeleteKey(PREF_KEY_MOVE_OLD_CHILDREN);
            EditorPrefs.DeleteKey(PREF_KEY_REMAP_REFERENCES);

            _replacementPrefab = null;
            _activeTab = TabType.REPLACE;
            _keepName = DEFAULT_KEEP_NAME;
            _keepRectLayout = DEFAULT_KEEP_RECT_LAYOUT;
            _keepSize = DEFAULT_KEEP_SIZE;
            _keepActiveLayerTag = DEFAULT_KEEP_ACTIVE_LAYER_TAG;
            _copyButtonSetup = DEFAULT_COPY_BUTTON_SETUP;
            _copyLabelText = DEFAULT_COPY_LABEL_TEXT;
            _copyExtraComponents = DEFAULT_COPY_EXTRA_COMPONENTS;
            _moveOldChildren = DEFAULT_MOVE_OLD_CHILDREN;
            _remapReferences = DEFAULT_REMAP_REFERENCES;

            if (_targets != null)
            {
                _targets.Clear();
            }

            Close();
            ShowWindow();
        }
        #endregion
    }
#endif
}
