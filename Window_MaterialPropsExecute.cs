#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;

namespace NamPhuThuy.AssetPipelineTools
{
    public class Window_MaterialPropsExecute : EditorWindow
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
        private const string SIGNATURE_MARK_RELATIVE_PATH = "../../UP_Common/nam_phu_thuy.png";
        private const string WINDOW_TITLE = "Material Properties Execute";

        [SerializeField] private List<Material> _materialListA = new List<Material>();
        [SerializeField] private List<Material> _materialListB = new List<Material>();

        private SerializedObject _so;
        private VisualElement _actionPanel;
        private HelpBox _warningBox;
        #endregion

        #region Menu Item
        [MenuItem("NamPhuThuy/Assets Pipeline/Window - Material Properties Execute")]
        public static void ShowWindow()
        {
            Window_MaterialPropsExecute window = GetWindow<Window_MaterialPropsExecute>("Material Props");
            window.minSize = new Vector2(550, 500);
            window.Show();
        }
        #endregion

        #region Unity Callbacks
        private void OnEnable()
        {
            Debug.Log("<color=#3B82F6>[Window_MaterialPropsExecute]</color> OnEnable");
            _so = new SerializedObject(this);
        }

        public void CreateGUI()
        {
            Debug.Log("<color=#3B82F6>[Window_MaterialPropsExecute]</color> CreateGUI");
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

            var helpBox = new HelpBox(
                "Copy/swap properties between two material lists of equal size.",
                HelpBoxMessageType.Info);
            root.Add(helpBox);

            // Scroll View
            var mainScroll = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1, marginTop = 10 } };
            root.Add(mainScroll);

            // Lists side-by-side row
            var listsRow = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween } };
            
            var leftList = UITK_AssetPipelineHelper.BuildAssetListSection<Material>(
                _so, "_materialListA", "List A", "Materials", _materialListA, OnListChanged
            );
            leftList.style.flexGrow = 1;
            leftList.style.marginRight = 6;
            leftList.style.flexBasis = Length.Percent(48);

            var rightList = UITK_AssetPipelineHelper.BuildAssetListSection<Material>(
                _so, "_materialListB", "List B", "Materials", _materialListB, OnListChanged
            );
            rightList.style.flexGrow = 1;
            rightList.style.marginLeft = 6;
            rightList.style.flexBasis = Length.Percent(48);

            listsRow.Add(leftList);
            listsRow.Add(rightList);
            mainScroll.Add(listsRow);

            // Warning Box for mismatch
            _warningBox = new HelpBox("Size mismatch or empty.", HelpBoxMessageType.Warning);
            _warningBox.style.marginBottom = 12;
            mainScroll.Add(_warningBox);

            // Action Panel (Buttons)
            _actionPanel = new VisualElement { style = { marginBottom = 12 } };
            
            var btnCopyAToB = new Button(() => CopyPropertiesList(_materialListA, _materialListB))
            {
                text = "Copy A \u2192 B",
                style = { height = 30, unityFontStyleAndWeight = FontStyle.Bold, backgroundColor = COLOR_OCEAN_BLUE, color = Color.white, marginBottom = 6 }
            };
            var btnCopyBToA = new Button(() => CopyPropertiesList(_materialListB, _materialListA))
            {
                text = "Copy B \u2192 A",
                style = { height = 30, unityFontStyleAndWeight = FontStyle.Bold, backgroundColor = COLOR_OCEAN_BLUE, color = Color.white, marginBottom = 6 }
            };
            var btnSwap = new Button(() => SwapPropertiesList(_materialListA, _materialListB))
            {
                text = "Swap A \u21c4 B",
                style = { height = 30, unityFontStyleAndWeight = FontStyle.Bold, backgroundColor = COLOR_OCEAN_BLUE, color = Color.white }
            };

            _actionPanel.Add(btnCopyAToB);
            _actionPanel.Add(btnCopyBToA);
            _actionPanel.Add(btnSwap);
            mainScroll.Add(_actionPanel);

            // Danger Zone
            mainScroll.Add(BuildResetSection());

            OnListChanged();
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
            var subTitle = new Label("Copy and swap properties between material arrays")
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

        private VisualElement BuildResetSection()
        {
            var resetBox = new VisualElement();
            resetBox.style.borderTopWidth = 1; resetBox.style.borderBottomWidth = 1; resetBox.style.borderLeftWidth = 1; resetBox.style.borderRightWidth = 1;
            resetBox.style.borderTopColor = COLOR_DANGER_BORDER; resetBox.style.borderBottomColor = COLOR_DANGER_BORDER;
            resetBox.style.borderLeftColor = COLOR_DANGER_BORDER; resetBox.style.borderRightColor = COLOR_DANGER_BORDER;
            resetBox.style.borderTopLeftRadius = 4; resetBox.style.borderTopRightRadius = 4;
            resetBox.style.borderBottomLeftRadius = 4; resetBox.style.borderBottomRightRadius = 4;
            resetBox.style.paddingLeft = 12; resetBox.style.paddingRight = 12; resetBox.style.paddingTop = 12; resetBox.style.paddingBottom = 12;
            resetBox.style.backgroundColor = COLOR_GREY_BOX;

            var resetTitle = new Label("Danger Zone / Options") { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 11, color = new Color(0.9f, 0.4f, 0.4f), marginBottom = 6 } };
            resetBox.Add(resetTitle);

            var resetBtn = new Button(ResetToDefaults)
            {
                text = "Reset Configurations to Defaults",
                style =
                {
                    height = 28,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    backgroundColor = COLOR_DANGER_BG,
                    color = Color.white,
                    borderTopLeftRadius = 4, borderTopRightRadius = 4, borderBottomLeftRadius = 4, borderBottomRightRadius = 4
                }
            };
            resetBox.Add(resetBtn);
            return resetBox;
        }
        #endregion

        #region Private Methods
        private void OnListChanged()
        {
            _materialListA.RemoveAll(m => m == null);
            _materialListB.RemoveAll(m => m == null);

            bool hasValidLists = _materialListA.Count > 0 && _materialListB.Count > 0 && _materialListA.Count == _materialListB.Count;
            _warningBox.style.display = hasValidLists ? DisplayStyle.None : DisplayStyle.Flex;
            _actionPanel.SetEnabled(hasValidLists);
        }

        private void CopyPropertiesList(List<Material> sourceList, List<Material> targetList)
        {
            int count = sourceList.Count;
            if (count == 0 || count != targetList.Count)
            {
                Debug.LogError("<color=red>[Window_MaterialPropsExecute]</color> Error: Lists are empty or mismatch.");
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Copy Materials List");
            int undoGroup = Undo.GetCurrentGroup();

            for (int i = 0; i < count; i++)
            {
                if (targetList[i] != null)
                {
                    Undo.RecordObject(targetList[i], "Copy Material Properties");
                }
            }

            Material[] tempSources = new Material[count];
            for (int i = 0; i < count; i++)
            {
                if (sourceList[i] != null)
                {
                    tempSources[i] = new Material(sourceList[i]);
                    tempSources[i].shader = sourceList[i].shader;
                }
            }

            for (int i = 0; i < count; i++)
            {
                Material source = tempSources[i];
                Material target = targetList[i];

                if (source != null && target != null)
                {
                    target.shader = source.shader;
                    target.CopyPropertiesFromMaterial(source);
                }
            }

            for (int i = 0; i < count; i++)
            {
                if (tempSources[i] != null)
                {
                    DestroyImmediate(tempSources[i]);
                }
            }

            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log($"<color=green>[Window_MaterialPropsExecute]</color> Success: Copied properties for {count} materials.");
        }

        private void SwapPropertiesList(List<Material> listA, List<Material> listB)
        {
            int count = listA.Count;
            if (count == 0 || count != listB.Count)
            {
                Debug.LogError("<color=red>[Window_MaterialPropsExecute]</color> Error: Lists are empty or mismatch.");
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Swap Materials List");
            int undoGroup = Undo.GetCurrentGroup();

            for (int i = 0; i < count; i++)
            {
                if (listA[i] != null) Undo.RecordObject(listA[i], "Swap Material Properties");
                if (listB[i] != null) Undo.RecordObject(listB[i], "Swap Material Properties");
            }

            Material[] tempA = new Material[count];
            Material[] tempB = new Material[count];

            for (int i = 0; i < count; i++)
            {
                if (listA[i] != null)
                {
                    tempA[i] = new Material(listA[i]);
                    tempA[i].shader = listA[i].shader;
                }
                if (listB[i] != null)
                {
                    tempB[i] = new Material(listB[i]);
                    tempB[i].shader = listB[i].shader;
                }
            }

            for (int i = 0; i < count; i++)
            {
                if (listA[i] != null && tempB[i] != null)
                {
                    listA[i].shader = tempB[i].shader;
                    listA[i].CopyPropertiesFromMaterial(tempB[i]);
                }

                if (listB[i] != null && tempA[i] != null)
                {
                    listB[i].shader = tempA[i].shader;
                    listB[i].CopyPropertiesFromMaterial(tempA[i]);
                }
            }

            for (int i = 0; i < count; i++)
            {
                if (tempA[i] != null) DestroyImmediate(tempA[i]);
                if (tempB[i] != null) DestroyImmediate(tempB[i]);
            }

            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log($"<color=green>[Window_MaterialPropsExecute]</color> Success: Swapped properties for {count} materials.");
        }

        private void ResetToDefaults()
        {
            Debug.Log("<color=red>[Window_MaterialPropsExecute]</color> ResetToDefaults");
            if (_materialListA != null) _materialListA.Clear();
            if (_materialListB != null) _materialListB.Clear();

            Close();
            ShowWindow();
        }
        #endregion
    }
}
#endif