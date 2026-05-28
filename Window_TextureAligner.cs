using UnityEngine;
using UnityEngine.UIElements;
using System.IO;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
#endif

namespace NamPhuThuy.AssetPipelineTools
{
#if UNITY_EDITOR
    public class Window_TextureAligner : EditorWindow
    {
        #region Private Fields
        [SerializeField] private List<Texture2D> _texturesToProcess = new List<Texture2D>();
        [SerializeField] private float _minAngleThreshold = 2.0f; // Only rotate if diagonal angle is greater than this
        [SerializeField] private bool _autoBackup = false;

        // UI References
        private VisualElement _listContainer;
        private Label _summaryLabel;
        private Label _previewLabel;
        private VisualElement _gridContainer;
        private VisualElement _previewBox;
        
        private int _selectedPreviewIndex = 0;
        private float _detectedAngleDeg = 0f;

        // Pixel cache for bilinear interpolation performance
        private Color[] _cachedPixels;
        #endregion

        #region Menu Item
        [MenuItem("NamPhuThuy/Assets Pipeline/Window UITK - Texture Aligner")]
        public static void ShowWindow()
        {
            var window = GetWindow<Window_TextureAligner>("Texture Aligner");
            window.minSize = new Vector2(500, 680);
            window.Show();
        }
        #endregion

        #region Unity Callbacks
        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoPerformed;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoPerformed;
        }

        private void OnUndoPerformed()
        {
            RefreshTexturesListUI();
            UpdatePreview();
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = 16;
            root.style.paddingRight = 16;
            root.style.paddingTop = 16;
            root.style.paddingBottom = 16;
            root.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f);

            // ── Header Section ──
            var headerRow = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.Center, alignItems = Align.Center, marginBottom = 4 } };
            
            var header = new Label("Texture Aligner")
            {
                style = { 
                    unityFontStyleAndWeight = FontStyle.Bold, 
                    fontSize = 18, 
                    unityTextAlign = TextAnchor.MiddleCenter, 
                    color = new Color(0.0f, 0.81f, 0.77f) 
                }
            };
            headerRow.Add(header);
            root.Add(headerRow);

            var helpBox = new HelpBox(
                "Detects and fixes diagonal asset textures by rotating them perfectly vertical.\n" +
                "• Moments Analysis: Uses 2D Image Moments to calculate the principal axis angle of non-transparent content.\n" +
                "• Bilinear Filtering: Performs premium sub-pixel interpolation to preserve high-res details.\n" +
                "• Dynamic Negative Space: Re-calculates and shrinks/expands texture size to perfectly wrap around the rotated image with clean padding.",
                HelpBoxMessageType.Info);
            helpBox.style.marginBottom = 10;
            root.Add(helpBox);

            // ── Main Scroll View ──
            var mainScroll = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1 } };
            root.Add(mainScroll);

            // Add Sections
            mainScroll.Add(BuildPreviewSection());
            mainScroll.Add(BuildTexturesSection());

            // ── Footer Buttons ──
            var buttonRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 12 } };
            
            var rotateBtn = new Button(ProcessAllTextures) 
            { 
                text = "🔄 Rotate & Align Textures", 
                style = { flexGrow = 1.5f, height = 32, unityFontStyleAndWeight = FontStyle.Bold, backgroundColor = new Color(0.09f, 0.62f, 0.37f) } 
            };
            buttonRow.Add(rotateBtn);

            root.Add(buttonRow);

            // Initial UI sync
            RefreshTexturesListUI();
            UpdatePreview();
        }
        #endregion

        #region UI Builders
        private VisualElement BuildBox()
        {
            var box = new VisualElement();
            box.style.borderTopWidth = 1; box.style.borderBottomWidth = 1; box.style.borderLeftWidth = 1; box.style.borderRightWidth = 1;
            box.style.borderTopColor = new Color(0.28f, 0.28f, 0.28f, 1f); box.style.borderBottomColor = new Color(0.28f, 0.28f, 0.28f, 1f);
            box.style.borderLeftColor = new Color(0.28f, 0.28f, 0.28f, 1f); box.style.borderRightColor = new Color(0.28f, 0.28f, 0.28f, 1f);
            box.style.borderTopLeftRadius = 5; box.style.borderTopRightRadius = 5;
            box.style.borderBottomLeftRadius = 5; box.style.borderBottomRightRadius = 5;
            box.style.paddingLeft = 12; box.style.paddingRight = 12; box.style.paddingTop = 12; box.style.paddingBottom = 12;
            box.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.9f);
            box.style.marginBottom = 10;
            return box;
        }

        private VisualElement BuildPreviewSection()
        {
            _previewBox = BuildBox();

            var title = new Label("Responsive Grid Preview (ALL Targets)") 
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
            _previewLabel = new Label("Select a texture below to preview its orientation.")
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
            var box = BuildBox();
            box.style.flexGrow = 1;

            var titleRow = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, alignItems = Align.Center, marginBottom = 6 } };
            var title = new Label("Target Textures to Align") { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 13, color = Color.white } };
            
            var addSelectedBtn = new Button(AddCurrentlySelectedTextures)
            {
                text = "➕ Add Selected",
                style = {
                    height = 20,
                    fontSize = 10,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    backgroundColor = new Color(0.2f, 0.4f, 0.6f),
                    marginRight = 6
                }
            };
            
            _summaryLabel = new Label("0 textures") { style = { fontSize = 11, unityFontStyleAndWeight = FontStyle.Italic, color = Color.gray } };
            
            var titleRightGroup = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            titleRightGroup.Add(addSelectedBtn);
            titleRightGroup.Add(_summaryLabel);
            
            titleRow.Add(title);
            titleRow.Add(titleRightGroup);
            box.Add(titleRow);

            var scroll = new ScrollView { style = { maxHeight = 160, minHeight = 60, flexGrow = 1 } };
            _listContainer = new VisualElement();
            scroll.Add(_listContainer);
            box.Add(scroll);

            // Drag and Drop Area
            var dragArea = new VisualElement 
            { 
                style = { 
                    borderTopWidth = 1, borderBottomWidth = 1, borderLeftWidth = 1, borderRightWidth = 1,
                    borderTopColor = new Color(0.35f, 0.35f, 0.35f, 0.5f), borderBottomColor = new Color(0.35f, 0.35f, 0.35f, 0.5f),
                    borderLeftColor = new Color(0.35f, 0.35f, 0.35f, 0.5f), borderRightColor = new Color(0.35f, 0.35f, 0.35f, 0.5f),
                    borderTopLeftRadius = 4, borderTopRightRadius = 4, borderBottomLeftRadius = 4, borderBottomRightRadius = 4,
                    paddingTop = 10, paddingBottom = 10, marginTop = 10,
                    alignItems = Align.Center, justifyContent = Justify.Center,
                    backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.5f)
                } 
            };
            
            dragArea.Add(new Label("Drag & Drop Texture2D Assets Here") { style = { fontSize = 11, color = Color.gray, unityFontStyleAndWeight = FontStyle.Bold } });
            
            dragArea.RegisterCallback<DragUpdatedEvent>(_ =>
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            });
            
            dragArea.RegisterCallback<DragPerformEvent>(_ =>
            {
                DragAndDrop.AcceptDrag();
                Undo.RecordObject(this, "Drag and Drop Textures");
                foreach (var obj in DragAndDrop.objectReferences)
                {
                    if (obj is Texture2D tex && !_texturesToProcess.Contains(tex))
                    {
                        _texturesToProcess.Add(tex);
                    }
                }
                RefreshTexturesListUI();
                UpdatePreview();
            });

            box.Add(dragArea);

            // Options Row
            var optionsRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 10, justifyContent = Justify.SpaceBetween } };
            
            var backupToggle = new Toggle("Auto-Backup") { value = _autoBackup };
            backupToggle.RegisterValueChangedCallback(evt => _autoBackup = evt.newValue);
            optionsRow.Add(backupToggle);

            var thresholdField = new FloatField("Min Angle Threshold (Deg)") { value = _minAngleThreshold, style = { width = 200 } };
            thresholdField.RegisterValueChangedCallback(evt => {
                _minAngleThreshold = Mathf.Clamp(evt.newValue, 0.5f, 45f);
                UpdatePreview();
            });
            optionsRow.Add(thresholdField);

            box.Add(optionsRow);

            return box;
        }

        private void RefreshTexturesListUI()
        {
            if (_listContainer == null) return;

            _listContainer.Clear();
            _texturesToProcess.RemoveAll(t => t == null);

            var localList = new List<Texture2D>(_texturesToProcess);

            _summaryLabel.text = $"{localList.Count} texture(s)";

            if (localList.Count == 0)
            {
                var emptyLabel = new Label("No textures added. Drag texture assets above to begin.")
                {
                    style = {
                        fontSize = 11,
                        unityFontStyleAndWeight = FontStyle.Italic,
                        color = Color.gray,
                        marginTop = 6,
                        unityTextAlign = TextAnchor.MiddleCenter
                    }
                };
                _listContainer.Add(emptyLabel);
                return;
            }

            for (int i = 0; i < localList.Count; i++)
            {
                int index = i;
                var tex = localList[index];

                var row = new VisualElement 
                { 
                    style = { 
                        flexDirection = FlexDirection.Row, 
                        marginBottom = 4, 
                        alignItems = Align.Center, 
                        paddingBottom = 4,
                        borderBottomWidth = 1,
                        borderBottomColor = new Color(0.18f, 0.18f, 0.18f, 0.5f)
                    } 
                };

                var objField = new ObjectField 
                { 
                    value = tex, 
                    objectType = typeof(Texture2D), 
                    allowSceneObjects = false,
                    style = { flexGrow = 1 }
                };
                
                objField.RegisterValueChangedCallback(evt => 
                {
                    Undo.RecordObject(this, "Change Target Texture Slot");
                    int curIndex = _texturesToProcess.IndexOf(tex);
                    if (curIndex >= 0)
                    {
                        _texturesToProcess[curIndex] = evt.newValue as Texture2D;
                    }
                    UpdatePreview();
                });
                row.Add(objField);

                var previewBtn = new Button(() => {
                    Selection.activeObject = tex;
                    int curIndex = _texturesToProcess.IndexOf(tex);
                    if (curIndex >= 0)
                    {
                        _selectedPreviewIndex = curIndex;
                    }
                    UpdatePreview();
                }) 
                { 
                    text = "👁 Preview", 
                    style = { height = 20, fontSize = 10, marginLeft = 4 } 
                };
                row.Add(previewBtn);

                var removeBtn = new Button(() => 
                {
                    Undo.RecordObject(this, "Remove Target Texture");
                    int curIndex = _texturesToProcess.IndexOf(tex);
                    if (curIndex >= 0)
                    {
                        _texturesToProcess.RemoveAt(curIndex);
                    }
                    RefreshTexturesListUI();
                    UpdatePreview();
                }) 
                { 
                    text = "✕", 
                    style = { width = 24, height = 20, unityFontStyleAndWeight = FontStyle.Bold, backgroundColor = new Color(0.5f, 0.15f, 0.15f) } 
                };
                row.Add(removeBtn);

                _listContainer.Add(row);
            }
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
                _previewLabel.text = "Drag textures to target list to analyze and preview.";
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

                    float rotToPositive90 = NormalizeAngle(90f - angleDeg);
                    float rotToNegative90 = NormalizeAngle(-90f - angleDeg);
                    finalRotDeg = (Mathf.Abs(rotToPositive90) < Mathf.Abs(rotToNegative90)) ? rotToPositive90 : rotToNegative90;
                    statusOk = Mathf.Abs(finalRotDeg) < _minAngleThreshold;
                }

                // Grid card element (64x64 px image container -> about 80% of original 80px)
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

                var angleLabel = new Label($"{angleDeg:F0}°")
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
                    string statusText = !statusOk
                        ? $"<color=#ffaa00><b>DIAGONAL DETECTED</b></color>\n• Needs Rotation: <b>{finalRotDeg:F1}°</b> to align vertical." 
                        : "<color=#00ff88><b>ALREADY VERTICAL</b></color>\n• Angle offset within threshold.";

                    _previewLabel.text = $"<b>Selected Asset</b>: {tex.name}\n" +
                                         $"• Original Dimensions: {tex.width}x{tex.height}\n" +
                                         $"• Calculated Center of Mass: ({centerX:F1}, {centerY:F1})\n" +
                                         $"• Content Tilt Angle: {angleDeg:F1}°\n" +
                                         $"• Status: {statusText}\n" +
                                         $"<i>(Click other grid items to select and view details)</i>";
                    _previewLabel.style.color = Color.white;
                }
            }
        }

        private void ProcessAllTextures()
        {
            if (_texturesToProcess.Count == 0)
            {
                EditorUtility.DisplayDialog("Error", "Texture list is empty.", "OK");
                return;
            }

            Debug.Log($"[Aligner] Start: count={_texturesToProcess.Count}");

            int processedCount = 0;
            int skippedCount = 0;
            List<Texture2D> alignedTextures = new List<Texture2D>();

            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (var tex in _texturesToProcess)
                {
                    if (tex == null)
                    {
                        Debug.LogError("[Aligner] Null reference!");
                        continue;
                    }

                    string assetPath = AssetDatabase.GetAssetPath(tex);
                    if (string.IsNullOrEmpty(assetPath))
                    {
                        Debug.LogError($"[Aligner] Path failed: '{tex.name}'");
                        continue;
                    }

                    // Ensure readable
                    var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                    if (importer == null)
                    {
                        Debug.LogError($"[Aligner] Importer failed: '{assetPath}'");
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
                        Debug.LogError($"[Aligner] Load failed: '{assetPath}'");
                        continue;
                    }

                    AnalyzeOrientation(readableTex, out float centerX, out float centerY, out float theta);
                    float currentAngleDeg = theta * Mathf.Rad2Deg;

                    float rotToPositive90 = NormalizeAngle(90f - currentAngleDeg);
                    float rotToNegative90 = NormalizeAngle(-90f - currentAngleDeg);
                    float finalRotDeg = (Mathf.Abs(rotToPositive90) < Mathf.Abs(rotToNegative90)) ? rotToPositive90 : rotToNegative90;

                    Debug.Log($"[Aligner] Analyze '{readableTex.name}': tilt={currentAngleDeg:F1}°, rec={finalRotDeg:F1}°, thresh={_minAngleThreshold}°");

                    if (Mathf.Abs(finalRotDeg) < _minAngleThreshold)
                    {
                        skippedCount++;
                        Debug.Log($"[Aligner] Skip '{readableTex.name}': vertical");
                        // Restore importer
                        if (!wasReadable)
                        {
                            importer.isReadable = false;
                            importer.SaveAndReimport();
                        }
                        continue;
                    }

                    // Perform backup if requested
                    if (_autoBackup)
                    {
                        string dir = Path.GetDirectoryName(assetPath);
                        string filename = Path.GetFileNameWithoutExtension(assetPath);
                        string ext = Path.GetExtension(assetPath);
                        string backupPath = $"{dir}/{filename}_Backup{ext}";
                        AssetDatabase.CopyAsset(assetPath, backupPath);
                        Debug.Log($"[Aligner] Backup: '{backupPath}'");
                    }

                    // Rotate the texture
                    float rotationAngleRad = finalRotDeg * Mathf.Deg2Rad;
                    Texture2D rotatedTex = RotateAndCenterTexture(readableTex, rotationAngleRad, centerX, centerY);

                    if (rotatedTex != null)
                    {
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
                        alignedTextures.Add(tex);
                    }
                    else
                    {
                        Debug.LogError($"[Aligner] Rotate failed: '{readableTex.name}'");
                    }

                    // Restore importer settings and ensure Read/Write is unticked after modifying the texture
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                    var postImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                    if (postImporter != null)
                    {
                        Debug.Log($"[Aligner] set readable back to FALSE");
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

            // Remove successfully aligned textures from the target process list so that UpdatePreview()
            // doesn't force them back to readable = true
            if (alignedTextures.Count > 0)
            {
                Undo.RecordObject(this, "Align and Remove Textures");
                foreach (var aligned in alignedTextures)
                {
                    _texturesToProcess.Remove(aligned);
                }
                RefreshTexturesListUI();
            }

            UpdatePreview();
            Debug.Log($"[Aligner] Done: aligned={processedCount}, skipped={skippedCount}");
            EditorUtility.DisplayDialog("Rotation Complete",
                $"Successfully aligned diagonal textures!\n\n" +
                $"• Aligned & Center-stabilized: {processedCount}\n" +
                $"• Skipped (Already vertical): {skippedCount}", "OK");
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
            // We rotate each non-transparent pixel's coordinate relative to the centroid (centerX, centerY)
            // to find the tightest possible rotated bounds, cutting out excess negative space.
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

            // Fallback: if no visible pixels found, rotate the corners of the original bounding box relative to the centroid
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

            // 3. Define new texture dimensions tightly fitting the rotated visual contents (2px safety margin to avoid edge cutoffs)
            int paddingX = 2;
            int paddingY = 2;
            int newWidth = Mathf.RoundToInt(rotWidth) + paddingX;
            int newHeight = Mathf.RoundToInt(rotHeight) + paddingY;

            // Ensure dimensions are even numbers (better for compression & graphics memory)
            if (newWidth % 2 != 0) newWidth++;
            if (newHeight % 2 != 0) newHeight++;

            // Impose sensible limits (min size 32, max size 2048)
            newWidth = Mathf.Clamp(newWidth, 32, 2048);
            newHeight = Mathf.Clamp(newHeight, 32, 2048);

            Debug.Log($"[Aligner] Bounds '{tex.name}': foot={rotWidth:F1}x{rotHeight:F1}, box=({minX:F1},{minY:F1})-({maxX:F1},{maxY:F1}), canvas={newWidth}x{newHeight}, pad={paddingX}x{paddingY}");

            // 4. Create the new texture of the appropriate size
            Texture2D result = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false);
            Color[] newPixels = new Color[newWidth * newHeight];

            float targetCenterX = newWidth / 2f;
            float targetCenterY = newHeight / 2f;

            // Centroid of the rotated visual box relative to the original center of mass
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

                    if (xs >= 0 && xs < width && ys >= 0 && ys < height)
                    {
                        newPixels[yd * newWidth + xd] = GetBilinearSample(tex, xs, ys);
                    }
                    else
                    {
                        newPixels[yd * newWidth + xd] = Color.clear;
                    }
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

            // Atan2(2 * mu11, mu20 - mu02) yields angle of the major axis relative to horizontal
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
                EditorUtility.DisplayDialog("No Textures Selected", "Please select one or more Texture2D assets in the Project window first.", "OK");
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
                RefreshTexturesListUI();
                UpdatePreview();
            }
        }
        #endregion
    }
#endif
}
