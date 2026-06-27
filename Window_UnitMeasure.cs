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
// 7. CACHING: Provide a 'Reset to Defaults' button in the options panel to clear/override cached or persisted EditorPrefs values that might become stale or invalid.
// 8. LISTS: When resetting list fields, avoid re-instantiating them if they are not null. Clear them instead to prevent issues with serialized property bindings.
// 9. NOTIFICATIONS: Reduce to use addition window to notify information, just Debug.Log it with color and method name prefix.
// ───────────────────────────────────────────────────────────────────────

using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
#endif

using NamPhuThuy.Common;

namespace NamPhuThuy.AssetPipelineTools
{
#if UNITY_EDITOR
    public enum MeasurementMode
    {
        RENDERERS = 0,
        COLLIDERS = 1,
        COMBINED = 2
    }

    public class Window_UnitMeasure : EditorWindow
    {
        #region Private Fields
        [SerializeField] private List<GameObject> _targetGameObjects = new List<GameObject>();
        [SerializeField] private MeasurementMode _measurementMode = MeasurementMode.COMBINED;
        [SerializeField] private bool _includeChildren = true;

        // Visualizer Settings
        [SerializeField] private Color _worldBoundsColor = Color.cyan;
        [SerializeField] private bool _drawDimensionLabels = true;

        // EditorPrefs Keys
        private const string PREF_KEY_MODE = "NamPhuThuy_UnitMeasure_Mode";
        private const string PREF_KEY_INCLUDE_CHILDREN = "NamPhuThuy_UnitMeasure_IncludeChildren";
        private const string PREF_KEY_DRAW_DIMENSIONS = "NamPhuThuy_UnitMeasure_DrawDimensions";
        private const string PREF_KEY_WORLD_COLOR = "NamPhuThuy_UnitMeasure_WorldColor";

        // Serialized Object representation for bindings
        private SerializedObject _so;

        // UI References
        private EnumField _modeField;
        private Toggle _includeChildrenToggle;

        private VisualElement _resultsListContainer;
        private VisualElement _resultsContainer;
        private HelpBox _noTargetHelpBox;

        private ColorField _worldColorField;
        private Toggle _drawDimensionsToggle;

        private VisualElement _summaryBox;
        private Label _biggestSizeLabel;
        private Label _smallestSizeLabel;

        // Scene Label GUIStyle
        private GUIStyle _sceneLabelStyle;
        #endregion

        #region Menu Item
        [MenuItem("NamPhuThuy/Assets Pipeline/Window - Unit Measure")]
        public static void ShowWindow()
        {
            Window_UnitMeasure window = GetWindow<Window_UnitMeasure>("Unit Measure");
            window.minSize = new Vector2(500, 600);
            window.Show();
        }
        #endregion

        #region Unity Callbacks
        private void OnEnable()
        {
            Debug.Log("[Unit Measure] OnEnable");
            SceneView.duringSceneGui += OnSceneGUI;

            _so = new SerializedObject(this);

            // Load persisted settings
            _measurementMode = (MeasurementMode)EditorPrefs.GetInt(PREF_KEY_MODE, (int)MeasurementMode.COMBINED);
            _includeChildren = EditorPrefs.GetBool(PREF_KEY_INCLUDE_CHILDREN, true);
            _drawDimensionLabels = EditorPrefs.GetBool(PREF_KEY_DRAW_DIMENSIONS, true);

            string worldColorHex = EditorPrefs.GetString(PREF_KEY_WORLD_COLOR, "#00FFFF");
            if (ColorUtility.TryParseHtmlString(worldColorHex, out Color worldCol)) _worldBoundsColor = worldCol;
            else _worldBoundsColor = Color.cyan;
        }

        private void OnDisable()
        {
            Debug.Log("[Unit Measure] OnDisable");
            SceneView.duringSceneGui -= OnSceneGUI;

            // Save persisted settings
            EditorPrefs.SetInt(PREF_KEY_MODE, (int)_measurementMode);
            EditorPrefs.SetBool(PREF_KEY_INCLUDE_CHILDREN, _includeChildren);
            EditorPrefs.SetBool(PREF_KEY_DRAW_DIMENSIONS, _drawDimensionLabels);
            EditorPrefs.SetString(PREF_KEY_WORLD_COLOR, "#" + ColorUtility.ToHtmlStringRGBA(_worldBoundsColor));
        }

        private const string SIGNATURE_MARK_RELATIVE_PATH = "../../UP_Common/nam_phu_thuy.png";

        // CreateGUI is the UITK entry point called once when the window is loaded
        public void CreateGUI()
        {
            Debug.Log("[Unit Measure] CreateGUI");
            var root = rootVisualElement;
            root.style.paddingLeft = 14;
            root.style.paddingRight = 14;
            root.style.paddingTop = 14;
            root.style.paddingBottom = 14;
            root.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f, 1f);

            // ── Premium Header Block ──
            var headerRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingBottom = 10,
                    marginBottom = 8,
                    borderBottomWidth = 1,
                    borderBottomColor = new Color(0.26f, 0.26f, 0.26f, 0.8f)
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
            string scriptDir = System.IO.Path.GetDirectoryName(scriptPath);
            string combinedPath = System.IO.Path.Combine(scriptDir, SIGNATURE_MARK_RELATIVE_PATH);
            string fullPath = System.IO.Path.GetFullPath(combinedPath).Replace("\\", "/");
            string resolvedPath = "Assets" + fullPath.Substring(Application.dataPath.Length);

            var signatureTex = AssetDatabase.LoadAssetAtPath<Texture2D>(resolvedPath);
            if (signatureTex != null)
            {
                signatureMark.style.backgroundImage = signatureTex;
            }
            else
            {
                signatureMark.style.backgroundColor = new Color(0.16f, 0.16f, 0.16f, 0.6f);
            }
            headerRow.Add(signatureMark);

            var textColumn = new VisualElement { style = { flexGrow = 1 } };
            var mainTitle = new Label("Unit Measure Tool")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 16,
                    color = new Color(0.53f, 0.8f, 0.92f, 1f)
                }
            };
            var subTitle = new Label("Measure exact dimensions in Unity units (Width, Height, Depth)")
            {
                style =
                {
                    fontSize = 11,
                    color = new Color(0.8f, 0.8f, 0.8f, 1f),
                    unityFontStyleAndWeight = FontStyle.Normal
                }
            };
            textColumn.Add(mainTitle);
            textColumn.Add(subTitle);
            headerRow.Add(textColumn);
            root.Add(headerRow);

            var helpBox = new HelpBox(
                "Measure the exact dimensions of multiple GameObjects in Unity units.\n" +
                "Draws visual bounds in the Scene View.",
                HelpBoxMessageType.Info);
            root.Add(helpBox);

            var mainScroll = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1, marginTop = 10 } };
            root.Add(mainScroll);

            // ── Content Sections ──
            mainScroll.Add(BuildSettingsSection());
            mainScroll.Add(BuildResultsSection());
            mainScroll.Add(BuildVisualizerSection());

            // ── Danger Zone ──
            var dangerBox = UITKEditorHelper.BuildBox("Danger Zone / Options");
            dangerBox.style.borderTopColor = new Color(0.6f, 0.2f, 0.2f, 0.8f);
            dangerBox.style.borderBottomColor = new Color(0.6f, 0.2f, 0.2f, 0.8f);
            dangerBox.style.borderLeftColor = new Color(0.6f, 0.2f, 0.2f, 0.8f);
            dangerBox.style.borderRightColor = new Color(0.6f, 0.2f, 0.2f, 0.8f);

            var resetBtn = new Button(ResetToDefaults)
            {
                text = "Reset Configurations to Defaults",
                style =
                {
                    height = 26,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    backgroundColor = new Color(0.55f, 0.15f, 0.15f, 1f),
                    color = Color.white,
                    borderTopLeftRadius = 4, borderTopRightRadius = 4, borderBottomLeftRadius = 4, borderBottomRightRadius = 4
                }
            };
            dangerBox.Add(resetBtn);
            mainScroll.Add(dangerBox);

            RefreshMeasurements();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (_targetGameObjects == null || _targetGameObjects.Count == 0) return;

            Matrix4x4 oldMatrix = Handles.matrix;
            Color oldColor = Handles.color;

            foreach (var go in _targetGameObjects)
            {
                if (go == null || !go.scene.IsValid()) continue;

                Bounds measuredBounds = GetMeasuredBounds(go);
                Handles.matrix = Matrix4x4.identity;
                Handles.color = _worldBoundsColor;
                Handles.DrawWireCube(measuredBounds.center, measuredBounds.size);

                if (_drawDimensionLabels)
                {
                    DrawSceneLabels(measuredBounds, $"{go.name} ");
                }
            }

            Handles.matrix = oldMatrix;
            Handles.color = oldColor;
        }
        #endregion

        #region UI Builders
        private VisualElement BuildSettingsSection()
        {
            // Build the List target section using the common UITK helper
            var listSection = UITKEditorHelper.BuildAssetListSection<GameObject>(
                _so,
                "_targetGameObjects",
                "Target GameObjects",
                "GameObjects",
                _targetGameObjects,
                () =>
                {
                    RefreshMeasurements();
                    SceneView.RepaintAll();
                },
                null,
                false
            );

            // Track any serialized modifications on the list to auto-refresh the metrics UI
            listSection.TrackSerializedObjectValue(_so, so =>
            {
                RefreshMeasurements();
                SceneView.RepaintAll();
            });

            _modeField = new EnumField("Measurement Mode", _measurementMode);
            _modeField.RegisterValueChangedCallback(evt =>
            {
                _measurementMode = (MeasurementMode)evt.newValue;
                RefreshMeasurements();
                SceneView.RepaintAll();
            });
            listSection.Add(_modeField);

            _includeChildrenToggle = new Toggle("Include Children")
            {
                value = _includeChildren
            };
            _includeChildrenToggle.RegisterValueChangedCallback(evt =>
            {
                _includeChildren = evt.newValue;
                RefreshMeasurements();
                SceneView.RepaintAll();
            });
            listSection.Add(_includeChildrenToggle);

            return listSection;
        }

        private VisualElement BuildResultsSection()
        {
            var container = new VisualElement();

            _noTargetHelpBox = new HelpBox("Select or drag one or more GameObjects to start measuring.", HelpBoxMessageType.Warning);
            container.Add(_noTargetHelpBox);

            _resultsContainer = new VisualElement();

            var resultsBox = UITKEditorHelper.BuildBox("Measurement Results List");
            
            // Add a header row with Copy All button
            var headerRow = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, marginBottom = 8 } };
            var title = new Label("Results") { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 12 } };
            var copyAllBtn = new Button(CopyAllSizesToClipboard)
            {
                text = "Copy All (CSV)",
                style = { height = 20, fontSize = 10 }
            };
            headerRow.Add(title);
            headerRow.Add(copyAllBtn);
            resultsBox.Add(headerRow);

            // Summary Stats Box
            _summaryBox = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.SpaceBetween,
                    backgroundColor = new Color(0.16f, 0.22f, 0.28f, 0.4f),
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderTopColor = new Color(0.2f, 0.3f, 0.4f, 0.5f),
                    borderBottomColor = new Color(0.2f, 0.3f, 0.4f, 0.5f),
                    borderLeftColor = new Color(0.2f, 0.3f, 0.4f, 0.5f),
                    borderRightColor = new Color(0.2f, 0.3f, 0.4f, 0.5f),
                    paddingLeft = 8,
                    paddingRight = 8,
                    paddingTop = 6,
                    paddingBottom = 6,
                    marginBottom = 8,
                    display = DisplayStyle.None
                }
            };

            var biggestCol = new VisualElement { style = { flexGrow = 1, flexBasis = 0, marginRight = 4 } };
            biggestCol.Add(new Label("LARGEST ASSET") { style = { fontSize = 8, unityFontStyleAndWeight = FontStyle.Bold, color = new Color(0.4f, 0.8f, 1f) } });
            _biggestSizeLabel = new Label("N/A") { style = { fontSize = 10, unityFontStyleAndWeight = FontStyle.Bold } };
            biggestCol.Add(_biggestSizeLabel);

            var smallestCol = new VisualElement { style = { flexGrow = 1, flexBasis = 0, marginLeft = 4 } };
            smallestCol.Add(new Label("SMALLEST ASSET") { style = { fontSize = 8, unityFontStyleAndWeight = FontStyle.Bold, color = new Color(1f, 0.6f, 0.4f) } });
            _smallestSizeLabel = new Label("N/A") { style = { fontSize = 10, unityFontStyleAndWeight = FontStyle.Bold } };
            smallestCol.Add(_smallestSizeLabel);

            _summaryBox.Add(biggestCol);
            _summaryBox.Add(smallestCol);
            resultsBox.Add(_summaryBox);
            
            _resultsListContainer = new VisualElement();
            // Configure layout for responsiveness:
            _resultsListContainer.style.flexDirection = FlexDirection.Row;
            _resultsListContainer.style.flexWrap = Wrap.Wrap;
            _resultsListContainer.style.marginLeft = -4;
            _resultsListContainer.style.marginRight = -4;
            resultsBox.Add(_resultsListContainer);
            _resultsContainer.Add(resultsBox);

            container.Add(_resultsContainer);
            return container;
        }

        private VisualElement BuildVisualizerSection()
        {
            var box = UITKEditorHelper.BuildBox("Scene View Visualization");

            _worldColorField = new ColorField("Bounds Color") { value = _worldBoundsColor };
            _worldColorField.RegisterValueChangedCallback(evt =>
            {
                _worldBoundsColor = evt.newValue;
                SceneView.RepaintAll();
            });
            box.Add(_worldColorField);

            _drawDimensionsToggle = new Toggle("Draw Dimension Labels") { value = _drawDimensionLabels };
            _drawDimensionsToggle.RegisterValueChangedCallback(evt =>
            {
                _drawDimensionLabels = evt.newValue;
                SceneView.RepaintAll();
            });
            box.Add(_drawDimensionsToggle);

            return box;
        }

        private VisualElement BuildRecordRow(GameObject go, int index)
        {
            Bounds measuredBounds = GetMeasuredBounds(go);
            Vector3 size = measuredBounds.size;

            var card = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    justifyContent = Justify.SpaceBetween,
                    marginBottom = 8,
                    marginLeft = 4,
                    marginRight = 4,
                    paddingLeft = 8,
                    paddingRight = 8,
                    paddingTop = 6,
                    paddingBottom = 6,
                    backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.4f),
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderTopColor = new Color(0.12f, 0.12f, 0.12f, 1f),
                    borderBottomColor = new Color(0.12f, 0.12f, 0.12f, 1f),
                    borderLeftColor = new Color(0.12f, 0.12f, 0.12f, 1f),
                    borderRightColor = new Color(0.12f, 0.12f, 0.12f, 1f),
                    flexGrow = 1,
                    flexShrink = 1,
                    flexBasis = Length.Percent(48),
                    minWidth = 230
                }
            };

            // Left: Name & Ping/Copy
            var leftSection = new VisualElement { style = { flexGrow = 1, marginRight = 6 } };
            
            var nameRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 4 } };
            var indexLabel = new Label($"{index + 1}.") 
            { 
                style = { marginRight = 4, unityFontStyleAndWeight = FontStyle.Bold, color = Color.gray } 
            };
            nameRow.Add(indexLabel);

            var nameLabel = new Label(go.name) 
            { 
                style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 11 } 
            };
            nameRow.Add(nameLabel);
            leftSection.Add(nameRow);

            var actionRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var pingBtn = new Button(() => EditorGUIUtility.PingObject(go))
            {
                text = "Ping",
                style = { height = 18, fontSize = 9, paddingLeft = 4, paddingRight = 4, marginRight = 4 }
            };
            var copyBtn = new Button(() =>
            {
                GUIUtility.systemCopyBuffer = $"{size.x:F3}, {size.y:F3}, {size.z:F3}";
                Debug.Log($"[Unit Measure] Copied size of {go.name} to clipboard: {GUIUtility.systemCopyBuffer}");
            })
            {
                text = "Copy",
                style = { height = 18, fontSize = 9, paddingLeft = 4, paddingRight = 4 }
            };
            actionRow.Add(pingBtn);
            actionRow.Add(copyBtn);
            leftSection.Add(actionRow);

            card.Add(leftSection);

            // Right: Size Grid
            var gridSection = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };

            var bgCol = new Color(0.15f, 0.15f, 0.15f, 0.6f);
            var borderColor = new Color(0.12f, 0.12f, 0.12f, 1f);

            VisualElement BuildValueBox(string axis, float value, Color valColor)
            {
                var box = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        backgroundColor = bgCol,
                        paddingLeft = 5,
                        paddingRight = 5,
                        paddingTop = 2,
                        paddingBottom = 2,
                        marginLeft = 3,
                        borderTopWidth = 1,
                        borderBottomWidth = 1,
                        borderLeftWidth = 1,
                        borderRightWidth = 1,
                        borderTopColor = borderColor,
                        borderBottomColor = borderColor,
                        borderLeftColor = borderColor,
                        borderRightColor = borderColor
                    }
                };
                box.Add(new Label($"{axis}:") { style = { fontSize = 8, color = Color.gray } });
                box.Add(new Label($"{value:F3}") { style = { fontSize = 10, unityFontStyleAndWeight = FontStyle.Bold, color = valColor, marginLeft = 2 } });
                return box;
            }

            gridSection.Add(BuildValueBox("X", size.x, new Color(1f, 0.4f, 0.4f)));
            gridSection.Add(BuildValueBox("Y", size.y, new Color(0.4f, 1f, 0.4f)));
            gridSection.Add(BuildValueBox("Z", size.z, new Color(0.4f, 0.6f, 1f)));

            card.Add(gridSection);

            return card;
        }
        #endregion

        #region Private Methods
        private void RefreshMeasurements()
        {
            if (_resultsListContainer == null) return;

            _resultsListContainer.Clear();

            int validCount = 0;
            GameObject biggestGo = null;
            GameObject smallestGo = null;
            float maxMag = float.MinValue;
            float minMag = float.MaxValue;
            Vector3 biggestSize = Vector3.zero;
            Vector3 smallestSize = Vector3.zero;

            foreach (var go in _targetGameObjects)
            {
                if (go == null) continue;
                validCount++;

                Bounds bounds = GetMeasuredBounds(go);
                Vector3 size = bounds.size;
                float mag = size.magnitude;

                if (mag > maxMag)
                {
                    maxMag = mag;
                    biggestGo = go;
                    biggestSize = size;
                }
                if (mag < minMag)
                {
                    minMag = mag;
                    smallestGo = go;
                    smallestSize = size;
                }
            }

            if (validCount == 0)
            {
                _noTargetHelpBox.style.display = DisplayStyle.Flex;
                _resultsContainer.style.display = DisplayStyle.None;
                if (_summaryBox != null) _summaryBox.style.display = DisplayStyle.None;
                return;
            }

            _noTargetHelpBox.style.display = DisplayStyle.None;
            _resultsContainer.style.display = DisplayStyle.Flex;

            if (_summaryBox != null && _biggestSizeLabel != null && _smallestSizeLabel != null && biggestGo != null && smallestGo != null)
            {
                _summaryBox.style.display = DisplayStyle.Flex;
                _biggestSizeLabel.text = $"{biggestGo.name} ({biggestSize.x:F3}, {biggestSize.y:F3}, {biggestSize.z:F3})";
                _smallestSizeLabel.text = $"{smallestGo.name} ({smallestSize.x:F3}, {smallestSize.y:F3}, {smallestSize.z:F3})";
            }
            else if (_summaryBox != null)
            {
                _summaryBox.style.display = DisplayStyle.None;
            }

            for (int i = 0; i < _targetGameObjects.Count; i++)
            {
                var go = _targetGameObjects[i];
                if (go == null) continue;

                _resultsListContainer.Add(BuildRecordRow(go, i));
            }
        }

        private void CopyAllSizesToClipboard()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("GameObject Name,Width (X),Height (Y),Depth (Z)");

            int count = 0;
            foreach (var go in _targetGameObjects)
            {
                if (go == null) continue;

                Bounds bounds = GetMeasuredBounds(go);
                Vector3 size = bounds.size;
                sb.AppendLine($"{go.name},{size.x:F3},{size.y:F3},{size.z:F3}");
                count++;
            }

            if (count == 0)
            {
                Debug.LogWarning("[Unit Measure] No valid GameObjects to copy.");
                return;
            }

            GUIUtility.systemCopyBuffer = sb.ToString();
            Debug.Log($"[Unit Measure] Copied CSV for {count} objects to clipboard.");
        }

        private void ResetToDefaults()
        {
            Debug.Log("<color=red>[Window_UnitMeasure]</color> ResetToDefaults");
            EditorPrefs.DeleteKey(PREF_KEY_MODE);
            EditorPrefs.DeleteKey(PREF_KEY_INCLUDE_CHILDREN);
            EditorPrefs.DeleteKey(PREF_KEY_DRAW_DIMENSIONS);
            EditorPrefs.DeleteKey(PREF_KEY_WORLD_COLOR);

            _measurementMode = MeasurementMode.COMBINED;
            _includeChildren = true;
            _drawDimensionLabels = true;
            _worldBoundsColor = Color.cyan;

            if (_targetGameObjects != null) _targetGameObjects.Clear();

            Close();
            ShowWindow();
        }
        #endregion

        #region Scene Helper Drawing
        private void DrawSceneLabels(Bounds bounds, string prefix)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            Vector3 size = bounds.size;

            Vector3 c000 = new Vector3(min.x, min.y, min.z);
            Vector3 c100 = new Vector3(max.x, min.y, min.z);
            Vector3 c010 = new Vector3(min.x, max.y, min.z);
            Vector3 c001 = new Vector3(min.x, min.y, max.z);

            if (_sceneLabelStyle == null)
            {
                _sceneLabelStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    normal = { textColor = Color.white },
                    fontSize = 10,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(4, 4, 2, 2)
                };
            }

            // Width (X) - Red
            Handles.color = Color.red;
            Handles.DrawDottedLine(c000, c100, 4.0f);
            Handles.Label(Vector3.Lerp(c000, c100, 0.5f), $"{prefix}W: {size.x:F3}", _sceneLabelStyle);

            // Height (Y) - Green
            Handles.color = Color.green;
            Handles.DrawDottedLine(c000, c010, 4.0f);
            Handles.Label(Vector3.Lerp(c000, c010, 0.5f), $"{prefix}H: {size.y:F3}", _sceneLabelStyle);

            // Depth (Z) - Blue
            Handles.color = Color.blue;
            Handles.DrawDottedLine(c000, c001, 4.0f);
            Handles.Label(Vector3.Lerp(c000, c001, 0.5f), $"{prefix}D: {size.z:F3}", _sceneLabelStyle);
        }
        #endregion

        #region Core Calculation Methods
        private Bounds GetMeasuredBounds(GameObject go)
        {
            if (_measurementMode == MeasurementMode.RENDERERS)
            {
                return CalculateWorldBounds(go, _includeChildren, MeasurementMode.RENDERERS);
            }
            else if (_measurementMode == MeasurementMode.COLLIDERS)
            {
                return CalculateWorldBounds(go, _includeChildren, MeasurementMode.COLLIDERS);
            }
            else // COMBINED
            {
                Bounds rBounds = CalculateWorldBounds(go, _includeChildren, MeasurementMode.RENDERERS);
                Bounds cBounds = CalculateWorldBounds(go, _includeChildren, MeasurementMode.COLLIDERS);
                
                // Compare magnitude to find the bigger bounds
                float rSize = rBounds.size.magnitude;
                float cSize = cBounds.size.magnitude;
                return (rSize >= cSize) ? rBounds : cBounds;
            }
        }

        private Bounds CalculateWorldBounds(GameObject target, bool includeChildren, MeasurementMode mode)
        {
            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool initialised = false;

            void EncapsulateWorldBounds(Bounds wBounds)
            {
                if (!initialised)
                {
                    bounds = wBounds;
                    initialised = true;
                }
                else
                {
                    bounds.Encapsulate(wBounds);
                }
            }

            List<GameObject> objs = new List<GameObject>();
            if (includeChildren)
            {
                foreach (Transform t in target.GetComponentsInChildren<Transform>(true))
                {
                    objs.Add(t.gameObject);
                }
            }
            else
            {
                objs.Add(target);
            }

            foreach (var obj in objs)
            {
                // Renderers
                if (mode == MeasurementMode.RENDERERS || mode == MeasurementMode.COMBINED)
                {
                    if (obj.TryGetComponent<Renderer>(out var renderer))
                    {
                        EncapsulateWorldBounds(renderer.bounds);
                    }
                }

                // Colliders
                if (mode == MeasurementMode.COLLIDERS || mode == MeasurementMode.COMBINED)
                {
                    if (obj.TryGetComponent<Collider>(out var col))
                    {
                        EncapsulateWorldBounds(col.bounds);
                    }
                    if (obj.TryGetComponent<Collider2D>(out var col2D))
                    {
                        EncapsulateWorldBounds(col2D.bounds);
                    }
                }
            }

            if (!initialised)
            {
                bounds = new Bounds(target.transform.position, Vector3.zero);
            }

            return bounds;
        }
        #endregion
    }
#endif
}