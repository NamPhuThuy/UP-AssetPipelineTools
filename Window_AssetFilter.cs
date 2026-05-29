#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace NamPhuThuy.AssetPipelineTools
{
    public class Window_AssetFilter : EditorWindow
    {
        #region Private Fields
        private Vector2 _scrollPos;
        private Vector2 _resultsScrollPos;
        private GUIStyle _centeredButtonStyle;
        private GUIStyle _headerStyle;

        [SerializeField] private List<DefaultAsset> _targetFolders = new List<DefaultAsset>();
        private List<string> _foundAssetPaths = new List<string>();

        // Filter
        private string _filterText = "";
        private AssetTypeFilter _filterTypeMask = AssetTypeFilter.All;

        // Move to folder
        private DefaultAsset _moveTargetFolder;
        #endregion

        #region Menu Item
        [MenuItem("NamPhuThuy/Assets Pipeline/Window - AssetFilter")]
        public static void ShowWindow()
        {
            var window = GetWindow<Window_AssetFilter>("Asset Filter");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }
        #endregion

        #region Unity Callbacks
        private void OnGUI()
        {
            InitializeStyles();

            GUILayout.Space(10);
            GUILayout.Label("Asset Filter", _headerStyle);
            EditorGUILayout.HelpBox(
                "Filter and batch move assets.",
                MessageType.Info);
            GUILayout.Space(10);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            DrawSearchFoldersSection();
            GUILayout.Space(10);
            DrawFilterSection();
            GUILayout.Space(10);
            DrawActionButtons();
            GUILayout.Space(10);
            DrawResultsSection();

            EditorGUILayout.EndScrollView();
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

            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 16
                };
            }
        }
        #endregion

        #region Drawing
        private void DrawSearchFoldersSection()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);

            // Header row with buttons
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"Folders ({_targetFolders.Count})", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Add Selected", GUILayout.Width(100)))
            {
                Undo.RecordObject(this, "Add Selected Folders");
                foreach (var obj in Selection.objects)
                {
                    if (obj is DefaultAsset folder && AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(folder)))
                    {
                        if (!_targetFolders.Contains(folder)) _targetFolders.Add(folder);
                    }
                }
            }
            if (GUILayout.Button("Clear", GUILayout.Width(80)))
            {
                Undo.RecordObject(this, "Clear Folders");
                _targetFolders.Clear();
                _foundAssetPaths.Clear();
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(5);

            // Entry list
            for (int i = 0; i < _targetFolders.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("X", GUILayout.Width(20)))
                {
                    Undo.RecordObject(this, "Remove Folder");
                    _targetFolders.RemoveAt(i);
                    GUIUtility.ExitGUI();
                }

                _targetFolders[i] = (DefaultAsset)EditorGUILayout.ObjectField(
                    _targetFolders[i], typeof(DefaultAsset), false);

                EditorGUILayout.EndHorizontal();
            }

            // Drop area
            GUILayout.Space(5);
            Rect dropRect = GUILayoutUtility.GetRect(0, 35, GUILayout.ExpandWidth(true));
            GUI.Box(dropRect, "Drag Folders Here", _centeredButtonStyle);
            HandleDragAndDrop(dropRect);

            EditorGUILayout.EndVertical();
        }

        private void DrawFilterSection()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);

            // Row 1: Text filter
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Filter", EditorStyles.miniLabel, GUILayout.Width(38));
            _filterText = EditorGUILayout.TextField(_filterText);
            if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(45)))
            {
                _filterText = "";
                _filterTypeMask = AssetTypeFilter.All;
            }
            EditorGUILayout.EndHorizontal();

            // Row 2: Type toggle buttons
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Types", EditorStyles.miniLabel, GUILayout.Width(38));

            // All / None shortcuts
            if (GUILayout.Button("All", EditorStyles.miniButton, GUILayout.Width(30)))
                _filterTypeMask = AssetTypeFilter.All;
            if (GUILayout.Button("None", EditorStyles.miniButton, GUILayout.Width(38)))
                _filterTypeMask = 0;

            GUILayout.Space(5);

            float windowWidth = EditorGUIUtility.currentViewWidth - 20; // Accounts for scrollbar & padding
            float currentWidth = 120; // Start with approx width of Label + All + None + Space

            // Individual toggles for each type flag
            foreach (AssetTypeFilter flag in System.Enum.GetValues(typeof(AssetTypeFilter)))
            {
                if (flag == AssetTypeFilter.All || flag == 0) continue;

                string label = flag.ToString();
                Vector2 btnSize = EditorStyles.miniButton.CalcSize(new GUIContent(label));
                float buttonWidth = btnSize.x + 4;

                if (currentWidth + buttonWidth > windowWidth)
                {
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                    
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(42); // Indent to align with other buttons
                    currentWidth = 42;
                }

                bool isOn = (_filterTypeMask & flag) != 0;
                bool newIsOn = GUILayout.Toggle(isOn, label, EditorStyles.miniButton, GUILayout.Width(btnSize.x));
                if (newIsOn != isOn)
                {
                    if (newIsOn)
                        _filterTypeMask |= flag;
                    else
                        _filterTypeMask &= ~flag;
                }

                currentWidth += buttonWidth;
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawActionButtons()
        {
            bool hasValidFolders = _targetFolders.Count > 0 && _targetFolders.Any(f => f != null);
            GUI.enabled = hasValidFolders;

            if (GUILayout.Button("Find Assets", _centeredButtonStyle, GUILayout.Height(35)))
            {
                FindAssets();
            }

            GUI.enabled = true;
        }

        private void DrawResultsSection()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);

            var filteredPaths = GetFilteredPaths(_foundAssetPaths);

            // Results header with Move controls
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Results", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            GUILayout.Label("To Folder:", EditorStyles.miniLabel, GUILayout.Width(78));
            _moveTargetFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                _moveTargetFolder, typeof(DefaultAsset), false, GUILayout.Width(200));

            bool canMove = _moveTargetFolder != null && filteredPaths.Count > 0
                           && AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(_moveTargetFolder));
            GUI.enabled = canMove;
            if (GUILayout.Button($"Move ({filteredPaths.Count})", GUILayout.Width(180)))
            {
                MoveResultsToFolder(filteredPaths);
            }
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(5);

            if (filteredPaths.Count > 0)
                EditorGUILayout.LabelField($"{filteredPaths.Count} items", EditorStyles.miniLabel);

            _resultsScrollPos = EditorGUILayout.BeginScrollView(_resultsScrollPos, GUILayout.MinHeight(300));

            if (_foundAssetPaths.Count > 0 && filteredPaths.Count == 0)
            {
                EditorGUILayout.LabelField("(No match)", EditorStyles.miniLabel);
            }

            foreach (string refPath in filteredPaths)
            {
                EditorGUILayout.BeginHorizontal();

                Object refAsset = AssetDatabase.LoadMainAssetAtPath(refPath);
                if (refAsset != null)
                {
                    Texture icon = AssetDatabase.GetCachedIcon(refPath);
                    GUIContent iconContent = icon != null ? new GUIContent(icon) : GUIContent.none;

                    if (GUILayout.Button(iconContent, EditorStyles.label, GUILayout.Width(18), GUILayout.Height(18)))
                    {
                        EditorGUIUtility.PingObject(refAsset);
                    }

                    GUI.enabled = false;
                    EditorGUILayout.ObjectField(refAsset, typeof(Object), false);
                    GUI.enabled = true;

                    if (GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(50)))
                    {
                        Selection.activeObject = refAsset;
                        EditorGUIUtility.PingObject(refAsset);
                    }
                }
                else
                {
                    EditorGUILayout.LabelField(refPath, "(Missing or Invalid Asset)");
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }
        #endregion

        #region Core Logic
        private void FindAssets()
        {
            _foundAssetPaths.Clear();
            var validFolderPaths = _targetFolders
                .Where(f => f != null)
                .Select(f => AssetDatabase.GetAssetPath(f))
                .Where(p => AssetDatabase.IsValidFolder(p))
                .Distinct()
                .ToArray();

            if (validFolderPaths.Length == 0) return;

            EditorUtility.DisplayProgressBar("Scanning", "Scanning...", 0.5f);

            string[] guids = AssetDatabase.FindAssets("", validFolderPaths);
            
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(path)) continue; // skip folders
                _foundAssetPaths.Add(path);
            }

            _foundAssetPaths = _foundAssetPaths.Distinct().ToList();

            EditorUtility.ClearProgressBar();
        }

        private void MoveResultsToFolder(List<string> pathsToMove)
        {
            string targetFolderPath = AssetDatabase.GetAssetPath(_moveTargetFolder);

            if (!AssetDatabase.IsValidFolder(targetFolderPath))
            {
                EditorUtility.DisplayDialog("Error", "Invalid folder.", "OK");
                return;
            }

            var assetsToMove = pathsToMove
                .Where(p => !p.StartsWith(targetFolderPath + "/"))
                .Distinct()
                .ToList();

            if (assetsToMove.Count == 0)
            {
                EditorUtility.DisplayDialog("Error", "Already in folder.", "OK");
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "Confirm",
                $"Move {assetsToMove.Count} assets to {targetFolderPath}?",
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
                        "Moving",
                        $"Moving {i + 1}/{assetsToMove.Count}",
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

            string message = $"Moved={movedCount}, Failed={failedCount}";
            Debug.Log(message);
            EditorUtility.DisplayDialog("Done", message, "OK");

            Repaint();
        }
        #endregion

        #region Helpers
        private List<string> GetFilteredPaths(List<string> paths)
        {
            if (_filterTypeMask == 0)
            {
                return new List<string>();
            }

            IEnumerable<string> result = paths;

            // Filter by type (if not All)
            if (_filterTypeMask != AssetTypeFilter.All)
            {
                result = result.Where(p => MatchesTypeMask(p, _filterTypeMask));
            }

            // Filter by text
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
                        Undo.RecordObject(this, "Drag and Drop Folders");

                        foreach (Object obj in DragAndDrop.objectReferences)
                        {
                            if (obj is DefaultAsset folder && AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(folder)))
                            {
                                if (!_targetFolders.Contains(folder)) _targetFolders.Add(folder);
                            }
                        }
                        currentEvent.Use();
                    }
                }
            }
        }
        #endregion
    }
}
#endif
