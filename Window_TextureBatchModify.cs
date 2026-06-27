#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;

namespace NamPhuThuy.AssetPipelineTools
{
    public class Window_TextureBatchModify : EditorWindow
    {
        private enum OperationMode { AutoAlign, ManualRotate }

        #region Private Fields
        [SerializeField] private List<Texture2D> _texturesToProcess = new List<Texture2D>();
        [SerializeField] private float _minAngleThreshold = 2.0f; // Only auto-rotate if diagonal angle is greater than this
        [SerializeField] private float _manualRotationAngle = 90.0f; // Custom rotation angle in degrees
        [SerializeField] private bool _autoBackup = false;
        [SerializeField] private OperationMode _currentMode = OperationMode.AutoAlign;

        private SerializedObject _serializedObject;

        // UI References
        private Label _previewLabel;
        private VisualElement _gridContainer;
        private VisualElement _previewBox;
        
        private Button _actionBtn;
        private Button _btnAutoModeRef;
        private Button _btnManualModeRef;
        private VisualElement _autoOptionsRow;
        private VisualElement _manualOptionsRow;

        private int _selectedPreviewIndex = 0;

        // Pixel cache for bilinear interpolation performance
        private Color[] _cachedPixels;
        #endregion

        #region Menu Item
        [MenuItem("NamPhuThuy/Assets Pipeline/Window UITK - Texture Batch Modify")]
        public static void ShowWindow()
        {
            var window = GetWindow<Window_TextureBatchModify>("Texture Batch Modify");
            window.minSize = new Vector2(500, 700);
            window.Show();
        }
        #endregion

        #region Unity Callbacks
        private void OnEnable()
        {
            _serializedObject = new SerializedObject(this);
            Undo.undoRedoPerformed += OnUndoPerformed;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoPerformed;
        }

        private void OnUndoPerformed()
        {
            if (_serializedObject != null) _serializedObject.Update();
            UpdatePreview();
        }

        private const string SIGNATURE_MARK_RELATIVE_PATH = "../../UP_Common/nam_phu_thuy.png";

        public void CreateGUI()
        {
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
                signatureMark.style.backgroundColor = new Color(0.16f, 0.16f, 0.16f, 0.6f);
            }
            headerRow.Add(signatureMark);

            var textColumn = new VisualElement { style = { flexGrow = 1 } };
            var mainTitle = new Label("Texture Batch Modify")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 16,
                    color = new Color(0.53f, 0.8f, 0.92f, 1f)
                }
            };
            var subTitle = new Label("Batch align or rotate texture assets in project")
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
                "Batch align or rotate texture assets.",
                HelpBoxMessageType.Info);
            helpBox.style.marginBottom = 10;
            root.Add(helpBox);

            // ── Mode Selection Tabs ──
            var modeToggleRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 12 } };
            
            var btnAutoMode = new Button(() => { SwitchMode(OperationMode.AutoAlign); }) 
            { 
                text = "⚙️ Auto-Align", 
                style = { flexGrow = 1, height = 28, unityFontStyleAndWeight = FontStyle.Bold } 
            };
            var btnManualMode = new Button(() => { SwitchMode(OperationMode.ManualRotate); }) 
            { 
                text = "🔄 Rotate", 
                style = { flexGrow = 1, height = 28, unityFontStyleAndWeight = FontStyle.Bold } 
            };
            
            modeToggleRow.Add(btnAutoMode);
            modeToggleRow.Add(btnManualMode);
            root.Add(modeToggleRow);

            // ── Main Scroll View ──
            var mainScroll = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1 } };
            root.Add(mainScroll);

            // Add Sections
            mainScroll.Add(BuildPreviewSection());
            mainScroll.Add(BuildTexturesSection());

            // ── Danger Zone ──
            var dangerBox = UITK_AssetPipelineHelper.BuildBox("Danger Zone / Options");
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

            // ── Footer Buttons ──
            var buttonRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 12 } };
            
            _actionBtn = new Button(ProcessAllTextures) 
            { 
                text = "Align", 
                style = { 
                    flexGrow = 1.5f, 
                    height = 32, 
                    unityFontStyleAndWeight = FontStyle.Bold, 
                    backgroundColor = new Color(0.0f, 0.47f, 0.74f, 1f),
                    color = Color.white,
                    borderTopLeftRadius = 4, borderTopRightRadius = 4, borderBottomLeftRadius = 4, borderBottomRightRadius = 4
                } 
            };
            buttonRow.Add(_actionBtn);

            root.Add(buttonRow);

            // Save references to mode buttons to style them dynamically
            _btnAutoModeRef = btnAutoMode;
            _btnManualModeRef = btnManualMode;

            // Initial UI sync
            RefreshModeStyles();
            UpdatePreview();
        }

        private void ResetToDefaults()
        {
            Debug.Log("<color=red>[Window_TextureBatchModify]</color> ResetToDefaults");
            if (_texturesToProcess != null) _texturesToProcess.Clear();
            _minAngleThreshold = 2.0f;
            _manualRotationAngle = 90.0f;
            _autoBackup = false;
            _currentMode = OperationMode.AutoAlign;
            _selectedPreviewIndex = 0;

            Close();
            ShowWindow();
        }
        #endregion

        #region Mode Switching
        private void SwitchMode(OperationMode mode)
        {
            _currentMode = mode;
            RefreshModeStyles();
            UpdatePreview();
        }

        private void RefreshModeStyles()
        {
            if (_btnAutoModeRef == null || _btnManualModeRef == null || _actionBtn == null) return;

            Color activeColor = new Color(0.53f, 0.8f, 0.92f, 1f);
            Color inactiveColor = new Color(0.25f, 0.25f, 0.25f);

            if (_currentMode == OperationMode.AutoAlign)
            {
                _btnAutoModeRef.style.backgroundColor = activeColor;
                _btnAutoModeRef.style.color = Color.black;
                _btnManualModeRef.style.backgroundColor = inactiveColor;
                _btnManualModeRef.style.color = Color.white;

                _actionBtn.text = "Align";
                _actionBtn.style.backgroundColor = new Color(0.0f, 0.47f, 0.74f, 1f);

                if (_autoOptionsRow != null) _autoOptionsRow.style.display = DisplayStyle.Flex;
                if (_manualOptionsRow != null) _manualOptionsRow.style.display = DisplayStyle.None;
                return;
            }

            _btnAutoModeRef.style.backgroundColor = inactiveColor;
            _btnAutoModeRef.style.color = Color.white;
            _btnManualModeRef.style.backgroundColor = activeColor;
            _btnManualModeRef.style.color = Color.black;

            _actionBtn.text = "Rotate";
            _actionBtn.style.backgroundColor = new Color(0.0f, 0.47f, 0.74f, 1f);

            if (_autoOptionsRow != null) _autoOptionsRow.style.display = DisplayStyle.None;
            if (_manualOptionsRow != null) _manualOptionsRow.style.display = DisplayStyle.Flex;
        }
        #endregion

        #region UI Builders
        

        private VisualElement BuildPreviewSection()
        {
            _previewBox = UITK_AssetPipelineHelper.BuildBox();

            var title = new Label("Grid Preview") 
            { 
                style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 13, color = Color.white, marginBottom = 8 } 
            };
            _previewBox.Add(title);

            // Responsive grid container wrapping automatically
            _gridContainer = new VisualElement
            {
                style = {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.Wrap,
                    justifyContent = Justify.FlexStart,
                    alignItems = Align.Center,
                    marginBottom = 8
                }
            };
            _previewBox.Add(_gridContainer);

            // Detailed analysis info below
            _previewLabel = new Label("Select texture to preview.")
            {
                style = {
                    fontSize = 11,
                    unityFontStyleAndWeight = FontStyle.Normal,
                    color = Color.gray,
                    paddingTop = 8,
                    borderTopWidth = 1,
                    borderTopColor = new Color(0.25f, 0.25f, 0.25f, 0.5f)
                }
            };
            _previewBox.Add(_previewLabel);

            return _previewBox;
        }

        private VisualElement BuildTexturesSection()
        {
            var section = UITK_AssetPipelineHelper.BuildAssetListSection<Texture2D>(
                _serializedObject,
                "_texturesToProcess",
                "Target Textures to Modify",
                "Target Textures List",
                _texturesToProcess,
                () => {
                    UpdatePreview();
                }
            );

            // Options Container
            var optionsContainer = new VisualElement { style = { marginTop = 10 } };

            // Backup Row (global)
            var backupRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 6 } };
            var backupToggle = new Toggle("Backup") { value = _autoBackup };
            backupToggle.RegisterValueChangedCallback(evt => _autoBackup = evt.newValue);
            backupRow.Add(backupToggle);
            optionsContainer.Add(backupRow);

            // Auto Options Row
            _autoOptionsRow = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween } };
            var thresholdField = new FloatField("Threshold (Deg)") { value = _minAngleThreshold, style = { width = 240 } };
            thresholdField.RegisterValueChangedCallback(evt => {
                _minAngleThreshold = Mathf.Clamp(evt.newValue, 0.5f, 45f);
                UpdatePreview();
            });
            _autoOptionsRow.Add(thresholdField);
            optionsContainer.Add(_autoOptionsRow);

            // Manual Options Row
            _manualOptionsRow = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, alignItems = Align.Center } };
            var manualAngleField = new FloatField("Angle (Deg)") { value = _manualRotationAngle, style = { width = 240 } };
            manualAngleField.RegisterValueChangedCallback(evt => {
                _manualRotationAngle = evt.newValue;
                UpdatePreview();
            });
            _manualOptionsRow.Add(manualAngleField);

            // Preset Buttons Container
            var presetContainer = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            var presetLabel = new Label("Presets") { style = { fontSize = 11, color = Color.gray, marginRight = 4 } };
            presetContainer.Add(presetLabel);

            float[] presets = { 45f, -45f, 90f, -90f, 180f };
            foreach (var preset in presets)
            {
                string buttonText = preset > 0 ? $"+{preset}°" : $"{preset}°";
                var btn = new Button(() => {
                    manualAngleField.value = preset;
                })
                {
                    text = buttonText,
                    style = {
                        height = 20,
                        fontSize = 10,
                        marginLeft = 2,
                        marginRight = 2,
                        unityFontStyleAndWeight = FontStyle.Bold,
                        backgroundColor = new Color(0.22f, 0.22f, 0.22f),
                        color = Color.white
                    }
                };
                presetContainer.Add(btn);
            }
            _manualOptionsRow.Add(presetContainer);
            optionsContainer.Add(_manualOptionsRow);

            section.Add(optionsContainer);

            return section;
        }
        #endregion

        #region Core Rotation Logic
        private void UpdatePreview()
        {
            if (_gridContainer == null || _previewLabel == null) return;

            _gridContainer.Clear();
            _texturesToProcess.RemoveAll(t => t == null);

            var localList = new List<Texture2D>(_texturesToProcess);

            if (localList.Count == 0)
            {
                _previewLabel.text = "Drag textures to target list.";
                _previewLabel.style.color = Color.gray;
                return;
            }

            if (_selectedPreviewIndex >= localList.Count)
            {
                _selectedPreviewIndex = 0;
            }

            for (int i = 0; i < localList.Count; i++)
            {
                int index = i;
                var tex = localList[index];

                bool isReadable = MakeTextureReadable(tex);
                float angleDeg = 0f;
                float centerX = 0f;
                float centerY = 0f;
                bool statusOk = false;
                float finalRotDeg = 0f;

                if (isReadable)
                {
                    AnalyzeOrientation(tex, out centerX, out centerY, out float theta);
                    angleDeg = theta * Mathf.Rad2Deg;

                    if (_currentMode == OperationMode.AutoAlign)
                    {
                        float rotToPositive90 = NormalizeAngle(90f - angleDeg);
                        float rotToNegative90 = NormalizeAngle(-90f - angleDeg);
                        finalRotDeg = (Mathf.Abs(rotToPositive90) < Mathf.Abs(rotToNegative90)) ? rotToPositive90 : rotToNegative90;
                        statusOk = Mathf.Abs(finalRotDeg) < _minAngleThreshold;
                    }
                    else
                    {
                        finalRotDeg = _manualRotationAngle;
                        statusOk = Mathf.Abs(finalRotDeg) < 0.01f;
                    }
                }

                // Grid card element (64x64 px image container)
                var card = new VisualElement
                {
                    style = {
                        width = 85,
                        height = 115,
                        marginRight = 6,
                        marginBottom = 6,
                        paddingLeft = 4, paddingRight = 4, paddingTop = 4, paddingBottom = 4,
                        backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.6f),
                        borderTopWidth = 1, borderBottomWidth = 1, borderLeftWidth = 1, borderRightWidth = 1,
                        borderTopLeftRadius = 4, borderTopRightRadius = 4, borderBottomLeftRadius = 4, borderBottomRightRadius = 4,
                        alignItems = Align.Center
                    }
                };

                // Apply active select/status borders
                if (index == _selectedPreviewIndex)
                {
                    card.style.borderTopColor = card.style.borderBottomColor = card.style.borderLeftColor = card.style.borderRightColor = new Color(0.0f, 0.81f, 0.77f);
                    card.style.borderTopWidth = card.style.borderBottomWidth = card.style.borderLeftWidth = card.style.borderRightWidth = 2;
                }
                else
                {
                    card.style.borderTopColor = card.style.borderBottomColor = card.style.borderLeftColor = card.style.borderRightColor = 
                        statusOk ? new Color(0.1f, 0.6f, 0.2f, 0.6f) : new Color(0.8f, 0.5f, 0.1f, 0.6f);
                    card.style.borderTopWidth = card.style.borderBottomWidth = card.style.borderLeftWidth = card.style.borderRightWidth = 1;
                }

                // Thumbnail (64x64 px)
                var img = new Image
                {
                    sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f),
                    style = {
                        width = 64,
                        height = 64,
                        backgroundColor = new Color(0.05f, 0.05f, 0.05f, 0.8f),
                        marginBottom = 4
                    }
                };
                card.Add(img);

                // Label Truncated
                string cleanName = tex.name;
                if (cleanName.Length > 10) cleanName = cleanName.Substring(0, 8) + "..";
                
                var nameLabel = new Label(cleanName)
                {
                    style = {
                        fontSize = 9,
                        unityFontStyleAndWeight = FontStyle.Bold,
                        color = Color.white,
                        unityTextAlign = TextAnchor.MiddleCenter
                    }
                };
                card.Add(nameLabel);

                string angleText = _currentMode == OperationMode.AutoAlign ? $"{angleDeg:F0}°" : $"{finalRotDeg:F0}°";
                var angleLabel = new Label(angleText)
                {
                    style = {
                        fontSize = 8,
                        color = statusOk ? new Color(0.4f, 0.9f, 0.5f) : new Color(0.9f, 0.6f, 0.2f),
                        unityTextAlign = TextAnchor.MiddleCenter
                    }
                };
                card.Add(angleLabel);

                // Mouse click registration
                card.RegisterCallback<MouseDownEvent>(evt =>
                {
                    int curIndex = _texturesToProcess.IndexOf(tex);
                    if (curIndex >= 0)
                    {
                        _selectedPreviewIndex = curIndex;
                    }
                    UpdatePreview();
                });

                _gridContainer.Add(card);

                // Show selected details in full HUD label below
                if (index == _selectedPreviewIndex)
                {
                    string statusText;
                    if (_currentMode == OperationMode.AutoAlign)
                    {
                        statusText = !statusOk
                            ? $"<color=#ffaa00><b>DIAGONAL DETECTED</b></color>\n• Needs Rotation: <b>{finalRotDeg:F1}°</b>" 
                            : "<color=#00ff88><b>ALREADY VERTICAL</b></color>";
                    }
                    else
                    {
                        statusText = $"<color=#00ccff><b>MANUAL ROTATION MODE</b></color>\n• Will rotate content by <b>{_manualRotationAngle:F1}°</b> around its center of mass.";
                    }

                    _previewLabel.text = $"<b>Selected Asset</b>: {tex.name}\n" +
                                         $"• Original Dimensions: {tex.width}x{tex.height}\n" +
                                         $"• Center of Mass: ({centerX:F1}, {centerY:F1})\n" +
                                         $"• Content Tilt Angle: {angleDeg:F1}°\n" +
                                         $"• Status: {statusText}\n";
                    _previewLabel.style.color = Color.white;
                }
            }
        }

        private void ProcessAllTextures()
        {
            if (_texturesToProcess.Count == 0)
            {
                Debug.Log("<color=red>[ProcessAllTextures][Error] Empty.</color>");
                return;
            }

            Debug.Log($"[TextureBatchModify] Start: count={_texturesToProcess.Count}, Mode={_currentMode}");

            int processedCount = 0;
            int skippedCount = 0;
            List<Texture2D> modifiedTextures = new List<Texture2D>();

            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (var tex in _texturesToProcess)
                {
                    if (tex == null)
                    {
                        Debug.LogError("[TextureBatchModify] Null reference!");
                        continue;
                    }

                    string assetPath = AssetDatabase.GetAssetPath(tex);
                    if (string.IsNullOrEmpty(assetPath))
                    {
                        Debug.LogError($"[TextureBatchModify] Path failed: '{tex.name}'");
                        continue;
                    }

                    // Ensure readable
                    var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                    if (importer == null)
                    {
                        Debug.LogError($"[TextureBatchModify] Importer failed: '{assetPath}'");
                        continue;
                    }

                    bool wasReadable = importer.isReadable;
                    TextureImporterType originalType = importer.textureType;
                    TextureImporterNPOTScale originalNpot = importer.npotScale;

                    if (!wasReadable)
                    {
                        importer.isReadable = true;
                        importer.SaveAndReimport();
                    }

                    // Re-load the texture after import changes
                    var readableTex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                    if (readableTex == null)
                    {
                        Debug.LogError($"[TextureBatchModify] Load failed: '{assetPath}'");
                        continue;
                    }

                    AnalyzeOrientation(readableTex, out float centerX, out float centerY, out float theta);
                    float currentAngleDeg = theta * Mathf.Rad2Deg;
                    float angleDeg = theta * Mathf.Rad2Deg;

                    float finalRotDeg = 0f;
                    bool shouldSkip = false;
                    string skipReason = "";

                    if (_currentMode == OperationMode.AutoAlign)
                    {
                        float rotToPositive90 = NormalizeAngle(90f - angleDeg);
                        float rotToNegative90 = NormalizeAngle(-90f - angleDeg);
                        finalRotDeg = (Mathf.Abs(rotToPositive90) < Mathf.Abs(rotToNegative90)) ? rotToPositive90 : rotToNegative90;
                        if (Mathf.Abs(finalRotDeg) < _minAngleThreshold)
                        {
                            shouldSkip = true;
                            skipReason = "vertical";
                        }
                    }
                    else
                    {
                        finalRotDeg = _manualRotationAngle;
                        if (Mathf.Abs(finalRotDeg) < 0.01f)
                        {
                            shouldSkip = true;
                            skipReason = "angle is zero";
                        }
                    }

                    if (shouldSkip)
                    {
                        skippedCount++;
                        Debug.Log($"[TextureBatchModify] Skip '{readableTex.name}': {skipReason}");
                        if (!wasReadable)
                        {
                            importer.isReadable = false;
                            importer.SaveAndReimport();
                        }
                        continue;
                    }

                    Debug.Log($"[TextureBatchModify] Processing '{readableTex.name}': angle={finalRotDeg:F1}°");

                    // Perform backup if requested
                    if (_autoBackup)
                    {
                        string dir = Path.GetDirectoryName(assetPath);
                        string filename = Path.GetFileNameWithoutExtension(assetPath);
                        string backupPath = $"{dir}/{filename}_Backup{Path.GetExtension(assetPath)}";
                        AssetDatabase.CopyAsset(assetPath, backupPath);
                        Debug.Log($"[TextureBatchModify] Backup: '{backupPath}'");
                    }

                    // Rotate the texture
                    float rotationAngleRad = finalRotDeg * Mathf.Deg2Rad;
                    Texture2D rotatedTex = RotateAndCenterTexture(readableTex, rotationAngleRad, centerX, centerY);
                    if (rotatedTex == null)
                    {
                        Debug.LogError($"[TextureBatchModify] Rotate failed: '{readableTex.name}'");
                        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                        var tempImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                        if (tempImporter != null)
                        {
                            tempImporter.isReadable = false;
                            tempImporter.SaveAndReimport();
                        }
                        continue;
                    }

                    byte[] bytes;
                    string ext = Path.GetExtension(assetPath).ToLowerInvariant();
                    if (ext == ".jpg" || ext == ".jpeg")
                    {
                        bytes = rotatedTex.EncodeToJPG(95);
                    }
                    else if (ext == ".tga")
                    {
                        bytes = rotatedTex.EncodeToTGA();
                    }
                    else
                    {
                        bytes = rotatedTex.EncodeToPNG(); // Default to PNG
                    }

                    File.WriteAllBytes(assetPath, bytes);
                    DestroyImmediate(rotatedTex);
                    processedCount++;
                    modifiedTextures.Add(tex);

                    // Restore importer settings and ensure Read/Write is unticked after modifying the texture
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                    var postImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                    if (postImporter != null)
                    {
                        postImporter.isReadable = false;
                        postImporter.SaveAndReimport();
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            // Remove successfully processed textures from the target process list
            if (modifiedTextures.Count > 0)
            {
                Undo.RecordObject(this, "Modify and Remove Textures");
                foreach (var mod in modifiedTextures)
                {
                    _texturesToProcess.Remove(mod);
                }
                if (_serializedObject != null) _serializedObject.Update();
            }

            UpdatePreview();
            Debug.Log($"Done: {processedCount}");
            
            Debug.Log($"<color=green>[ProcessAllTextures][Done] Success={processedCount}, Skipped={skippedCount}</color>");
        }

        private Texture2D RotateAndCenterTexture(Texture2D tex, float rotationAngleRad, float centerX, float centerY)
        {
            Color[] pixels = tex.GetPixels();
            int width = tex.width;
            int height = tex.height;

            _cachedPixels = pixels;

            // 1. Find the bounding box of non-transparent pixels in original texture space
            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            bool foundVisible = false;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (pixels[y * width + x].a > 0.05f)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                        foundVisible = true;
                    }
                }
            }

            // Fallback if completely transparent
            if (!foundVisible)
            {
                minX = 0; maxX = width - 1;
                minY = 0; maxY = height - 1;
            }

            // 2. Compute the bounding box of the non-transparent pixels AFTER rotation
            float rotCos = Mathf.Cos(rotationAngleRad);
            float rotSin = Mathf.Sin(rotationAngleRad);

            float rotatedMinX = float.MaxValue;
            float rotatedMaxX = float.MinValue;
            float rotatedMinY = float.MaxValue;
            float rotatedMaxY = float.MinValue;
            bool foundVisibleRotated = false;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (pixels[y * width + x].a > 0.05f)
                    {
                        float dx = x - centerX;
                        float dy = y - centerY;
                        float rx = dx * rotCos - dy * rotSin;
                        float ry = dx * rotSin + dy * rotCos;

                        if (rx < rotatedMinX) rotatedMinX = rx;
                        if (rx > rotatedMaxX) rotatedMaxX = rx;
                        if (ry < rotatedMinY) rotatedMinY = ry;
                        if (ry > rotatedMaxY) rotatedMaxY = ry;
                        foundVisibleRotated = true;
                    }
                }
            }

            // Fallback: if no visible pixels found, rotate original bounds
            if (!foundVisibleRotated)
            {
                Vector2[] corners = new Vector2[]
                {
                    new Vector2(minX, minY),
                    new Vector2(maxX, minY),
                    new Vector2(minX, maxY),
                    new Vector2(maxX, maxY)
                };

                foreach (var c in corners)
                {
                    float dx = c.x - centerX;
                    float dy = c.y - centerY;
                    float rx = dx * rotCos - dy * rotSin;
                    float ry = dx * rotSin + dy * rotCos;

                    if (rx < rotatedMinX) rotatedMinX = rx;
                    if (rx > rotatedMaxX) rotatedMaxX = rx;
                    if (ry < rotatedMinY) rotatedMinY = ry;
                    if (ry > rotatedMaxY) rotatedMaxY = ry;
                }
            }

            // Rotated visual size
            float rotWidth = rotatedMaxX - rotatedMinX;
            float rotHeight = rotatedMaxY - rotatedMinY;

            // 3. Define new texture dimensions tightly fitting the rotated visual contents (2px safety margin)
            int paddingX = 2;
            int paddingY = 2;
            int newWidth = Mathf.RoundToInt(rotWidth) + paddingX;
            int newHeight = Mathf.RoundToInt(rotHeight) + paddingY;

            // Ensure dimensions are even numbers (better for compression & graphics memory)
            if (newWidth % 2 != 0) newWidth++;
            if (newHeight % 2 != 0) newHeight++;

            // Impose limits
            newWidth = Mathf.Clamp(newWidth, 32, 2048);
            newHeight = Mathf.Clamp(newHeight, 32, 2048);

            // 4. Create the new texture of the appropriate size
            Texture2D result = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false);
            Color[] newPixels = new Color[newWidth * newHeight];

            float targetCenterX = newWidth / 2f;
            float targetCenterY = newHeight / 2f;

            // Centroid of the rotated visual box relative to original center of mass
            float rotCentroidX = (rotatedMinX + rotatedMaxX) / 2f;
            float rotCentroidY = (rotatedMinY + rotatedMaxY) / 2f;

            // 5. Inverse map pixels from target to source
            float invCos = Mathf.Cos(-rotationAngleRad);
            float invSin = Mathf.Sin(-rotationAngleRad);

            for (int yd = 0; yd < newHeight; yd++)
            {
                for (int xd = 0; xd < newWidth; xd++)
                {
                    float dx = xd - targetCenterX;
                    float dy = yd - targetCenterY;

                    float rx = dx + rotCentroidX;
                    float ry = dy + rotCentroidY;

                    float xs = centerX + (rx * invCos - ry * invSin);
                    float ys = centerY + (rx * invSin + ry * invCos);

                    if (xs < 0 || xs >= width || ys < 0 || ys >= height)
                    {
                        newPixels[yd * newWidth + xd] = Color.clear;
                        continue;
                    }

                    newPixels[yd * newWidth + xd] = GetBilinearSample(tex, xs, ys);
                }
            }

            result.SetPixels(newPixels);
            result.Apply();
            return result;
        }

        private Color GetBilinearSample(Texture2D tex, float x, float y)
        {
            int x1 = Mathf.FloorToInt(x);
            int y1 = Mathf.FloorToInt(y);
            int x2 = x1 + 1;
            int y2 = y1 + 1;

            int width = tex.width;
            int height = tex.height;

            x1 = Mathf.Clamp(x1, 0, width - 1);
            x2 = Mathf.Clamp(x2, 0, width - 1);
            y1 = Mathf.Clamp(y1, 0, height - 1);
            y2 = Mathf.Clamp(y2, 0, height - 1);

            float tx = x - x1;
            float ty = y - y1;

            Color c11 = _cachedPixels[y1 * width + x1];
            Color c21 = _cachedPixels[y1 * width + x2];
            Color c12 = _cachedPixels[y2 * width + x1];
            Color c22 = _cachedPixels[y2 * width + x2];

            Color cReg1 = Color.Lerp(c11, c21, tx);
            Color cReg2 = Color.Lerp(c12, c22, tx);

            return Color.Lerp(cReg1, cReg2, ty);
        }

        private void AnalyzeOrientation(Texture2D tex, out float centerX, out float centerY, out float theta)
        {
            Color[] pixels = tex.GetPixels();
            int width = tex.width;
            int height = tex.height;

            double m00 = 0, m10 = 0, m01 = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float alpha = pixels[y * width + x].a;
                    if (alpha > 0.05f)
                    {
                        m00 += alpha;
                        m10 += x * alpha;
                        m01 += y * alpha;
                    }
                }
            }

            if (m00 < 5.0)
            {
                centerX = width / 2f;
                centerY = height / 2f;
                theta = 0f;
                return;
            }

            centerX = (float)(m10 / m00);
            centerY = (float)(m01 / m00);

            double mu20 = 0, mu02 = 0, mu11 = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float alpha = pixels[y * width + x].a;
                    if (alpha > 0.05f)
                    {
                        double dx = x - centerX;
                        double dy = y - centerY;
                        mu20 += dx * dx * alpha;
                        mu02 += dy * dy * alpha;
                        mu11 += dx * dy * alpha;
                    }
                }
            }

            theta = 0.5f * Mathf.Atan2((float)(2.0 * mu11), (float)(mu20 - mu02));
        }

        private bool MakeTextureReadable(Texture2D tex)
        {
            string path = AssetDatabase.GetAssetPath(tex);
            if (string.IsNullOrEmpty(path)) return false;

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return false;

            if (!importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
            return true;
        }

        private float NormalizeAngle(float angle)
        {
            while (angle <= -180f) angle += 360f;
            while (angle > 180f) angle -= 360f;
            return angle;
        }

        private void AddCurrentlySelectedTextures()
        {
            Texture2D[] selectedTextures = Selection.GetFiltered<Texture2D>(SelectionMode.Assets);
            if (selectedTextures == null || selectedTextures.Length == 0)
            {
                Debug.Log("<color=yellow>[AddCurrentlySelectedTextures][Warning] No Textures Selected: Please select one or more Texture2D assets in the Project window first.</color>");
                return;
            }

            Undo.RecordObject(this, "Add Selected Textures");
            int addedCount = 0;
            foreach (var tex in selectedTextures)
            {
                if (tex != null && !_texturesToProcess.Contains(tex))
                {
                    _texturesToProcess.Add(tex);
                    addedCount++;
                }
            }

            if (addedCount > 0)
            {
                if (_serializedObject != null) _serializedObject.Update();
                UpdatePreview();
            }
        }
        #endregion
    }
}
#endif