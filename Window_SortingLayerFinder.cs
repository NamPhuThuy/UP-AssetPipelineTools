#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditor.SceneManagement;

namespace NamPhuThuy.AssetPipelineTools
{
    public class Window_SortingLayerFinder : EditorWindow
    {
        public enum SearchScope
        {
            NONE = 0,
            ACTIVE_SCENE = 1,
            PROJECT_PREFABS = 2,
            SPECIFIC_FOLDERS = 3,
            ALL_SCENES_IN_BUILD = 4
        }

        [System.Serializable]
        public class SortingLayerMatch
        {
            public string GameObjectName;
            public string ComponentType;
            public string SortingLayerName;
            public int SortingOrder;
            public bool IsPrefab;
            public string Path; // Scene path or Prefab asset path
            public string HierarchyPath;
            public UnityEngine.Object TargetComponent;
            public UnityEngine.Object TargetGameObject;
        }

        #region Private Fields
        [SerializeField] private string _selectedLayerName = "Default";
        [SerializeField] private SearchScope _searchScope = SearchScope.ACTIVE_SCENE;
        [SerializeField] private List<DefaultAsset> _targetFolders = new List<DefaultAsset>();

        private List<SortingLayerMatch> _results = new List<SortingLayerMatch>();

        private SerializedObject _serializedObject;

        // UI references
        private DropdownField _sortingLayerDropdown;
        private EnumField _searchScopeField;
        private VisualElement _foldersSectionContainer;
        private VisualElement _resultsListContainer;
        private Label _summaryLabel;

        // EditorPrefs Keys
        private const string PREF_KEY_SELECTED_LAYER = "NamPhuThuy_SortingLayerFinder_SelectedLayer";
        private const string PREF_KEY_SEARCH_SCOPE = "NamPhuThuy_SortingLayerFinder_SearchScope";
        private const string ALL_LAYERS_OPTION = "<All Sorting Layers>";
        #endregion

        #region Menu Item
        [MenuItem("NamPhuThuy/Assets Pipeline/Window UITK - Sorting Layer Finder")]
        public static void ShowWindow()
        {
            var window = GetWindow<Window_SortingLayerFinder>("Sorting Layer Finder");
            window.minSize = new Vector2(500, 600);
            window.Show();
        }
        #endregion

        #region Unity Callbacks
        private void OnEnable()
        {
            Debug.Log("[SortingLayerFinder.OnEnable] Initialization and cache load.");
            _serializedObject = new SerializedObject(this);

            // Load persisted data
            _selectedLayerName = EditorPrefs.GetString(PREF_KEY_SELECTED_LAYER, "Default");
            _searchScope = (SearchScope)EditorPrefs.GetInt(PREF_KEY_SEARCH_SCOPE, (int)SearchScope.ACTIVE_SCENE);
        }

        private void OnDisable()
        {
            Debug.Log("[SortingLayerFinder.OnDisable] Save settings to cache.");
            // Save data when window closes
            EditorPrefs.SetString(PREF_KEY_SELECTED_LAYER, _selectedLayerName);
            EditorPrefs.SetInt(PREF_KEY_SEARCH_SCOPE, (int)_searchScope);
        }

        public void CreateGUI()
        {
            Debug.Log("[SortingLayerFinder.CreateGUI] Building UI hierarchy.");
            var root = rootVisualElement;
            root.style.paddingLeft = 16;
            root.style.paddingRight = 16;
            root.style.paddingTop = 16;
            root.style.paddingBottom = 16;
            root.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f);

            // ── Header Section ──
            var header = new Label("Sorting Layer Finder")
            {
                style = { 
                    unityFontStyleAndWeight = FontStyle.Bold, 
                    fontSize = 18, 
                    unityTextAlign = TextAnchor.MiddleCenter, 
                    color = new Color(0.9f, 0.7f, 0.1f),
                    marginBottom = 10 
                }
            };
            root.Add(header);

            var helpBox = new HelpBox(
                "Find all GameObjects that use a certain sorting layer in scenes, prefabs, or specific folders.",
                HelpBoxMessageType.Info);
            helpBox.style.marginBottom = 12;
            root.Add(helpBox);

            // ── Main Scroll View ──
            var mainScroll = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1 } };
            root.Add(mainScroll);

            // ── Configuration Section ──
            var configBox = UITK_AssetPipelineHelper.BuildBox("Configuration");
            mainScroll.Add(configBox);

            // Dynamic Sorting Layers retrieval
            var sortingLayers = SortingLayer.layers;
            var layerNames = new List<string>();
            layerNames.Add(ALL_LAYERS_OPTION);
            foreach (var layer in sortingLayers)
            {
                layerNames.Add(layer.name);
            }
            if (!layerNames.Contains("Default"))
            {
                layerNames.Add("Default");
            }

            // Dropdown field for selecting sorting layer
            _sortingLayerDropdown = new DropdownField("Sorting Layer", layerNames, _selectedLayerName);
            _sortingLayerDropdown.RegisterValueChangedCallback(evt => {
                _selectedLayerName = evt.newValue;
            });
            configBox.Add(_sortingLayerDropdown);

            // Search Scope Enum field
            _searchScopeField = new EnumField("Search Scope", _searchScope);
            _searchScopeField.RegisterValueChangedCallback(evt => {
                _searchScope = (SearchScope)evt.newValue;
                UpdateFoldersVisibility();
            });
            configBox.Add(_searchScopeField);

            // ── Folder Assets Section ──
            _foldersSectionContainer = new VisualElement();
            _foldersSectionContainer.Add(UITK_AssetPipelineHelper.BuildAssetListSection<DefaultAsset>(
                _serializedObject,
                "_targetFolders",
                "Target Search Folders",
                "Folders List",
                _targetFolders,
                () => {
                    if (_serializedObject != null) _serializedObject.Update();
                }
            ));
            mainScroll.Add(_foldersSectionContainer);

            // ── Actions Buttons ──
            var buttonRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 8, marginBottom = 12 } };
            
            var findBtn = new Button(FindSortingLayerObjects) 
            { 
                text = "🔍 Find GameObjects", 
                style = { flexGrow = 2f, height = 32, unityFontStyleAndWeight = FontStyle.Bold, backgroundColor = new Color(0.09f, 0.62f, 0.37f), color = Color.white, marginRight = 4 } 
            };
            buttonRow.Add(findBtn);

            var resetBtn = new Button(ResetToDefaults) 
            { 
                text = "Reset to Defaults", 
                style = { flexGrow = 1f, height = 32, unityFontStyleAndWeight = FontStyle.Bold, backgroundColor = new Color(0.25f, 0.25f, 0.25f), color = Color.white, marginLeft = 4 } 
            };
            buttonRow.Add(resetBtn);

            mainScroll.Add(buttonRow);

            // ── Results Section ──
            var resultsBox = UITK_AssetPipelineHelper.BuildBox("Results");
            mainScroll.Add(resultsBox);

            _summaryLabel = new Label("No scan performed yet.")
            {
                style = { fontSize = 11, color = Color.gray, marginBottom = 8 }
            };
            resultsBox.Add(_summaryLabel);

            _resultsListContainer = new VisualElement();
            resultsBox.Add(_resultsListContainer);

            // Initialize UI States
            UpdateFoldersVisibility();
            RefreshResultsUI();
        }
        #endregion

        #region Helpers / Visibility
        private void UpdateFoldersVisibility()
        {
            if (_foldersSectionContainer == null) return;
            _foldersSectionContainer.style.display = (_searchScope == SearchScope.SPECIFIC_FOLDERS) ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void ResetToDefaults()
        {
            Debug.Log("[SortingLayerFinder.ResetToDefaults] Resetting search configuration to defaults.");
            
            // Clear EditorPrefs
            EditorPrefs.DeleteKey(PREF_KEY_SELECTED_LAYER);
            EditorPrefs.DeleteKey(PREF_KEY_SEARCH_SCOPE);

            _selectedLayerName = "Default";
            _searchScope = SearchScope.ACTIVE_SCENE;

            // Clear target folders and results without re-instantiating (Rule 8)
            if (_targetFolders != null)
            {
                _targetFolders.Clear();
            }
            else
            {
                _targetFolders = new List<DefaultAsset>();
            }

            if (_results != null)
            {
                _results.Clear();
            }
            else
            {
                _results = new List<SortingLayerMatch>();
            }

            if (_serializedObject != null)
            {
                _serializedObject.Update();
            }

            // Sync UI components
            if (_sortingLayerDropdown != null) _sortingLayerDropdown.value = _selectedLayerName;
            if (_searchScopeField != null) _searchScopeField.value = _searchScope;
            
            UpdateFoldersVisibility();
            RefreshResultsUI();
        }
        #endregion

        #region Search Logic
        private void FindSortingLayerObjects()
        {
            Debug.Log($"[SortingLayerFinder.FindSortingLayerObjects] Starting scan. Target Layer: {_selectedLayerName}, Scope: {_searchScope}");

            if (_results != null)
            {
                _results.Clear();
            }
            else
            {
                _results = new List<SortingLayerMatch>();
            }

            bool searchAll = (_selectedLayerName == ALL_LAYERS_OPTION);
            int targetLayerID = searchAll ? 0 : SortingLayer.NameToID(_selectedLayerName);

            switch (_searchScope)
            {
                case SearchScope.ACTIVE_SCENE:
                    ScanActiveScene(targetLayerID, searchAll);
                    break;
                case SearchScope.PROJECT_PREFABS:
                    ScanProjectPrefabs(targetLayerID, searchAll);
                    break;
                case SearchScope.SPECIFIC_FOLDERS:
                    ScanSpecificFolders(targetLayerID, searchAll);
                    break;
                case SearchScope.ALL_SCENES_IN_BUILD:
                    ScanBuildScenes(targetLayerID, searchAll);
                    break;
                default:
                    Debug.LogError("[SortingLayerFinder.FindSortingLayerObjects] Invalid search scope selected.");
                    break;
            }

            RefreshResultsUI();
        }

        private void ScanActiveScene(int targetLayerID, bool searchAll)
        {
            var activeScene = SceneManager.GetActiveScene();
            string sceneName = string.IsNullOrEmpty(activeScene.path) ? "Unsaved Scene" : Path.GetFileName(activeScene.path);
            var rootObjects = activeScene.GetRootGameObjects();

            foreach (var root in rootObjects)
            {
                ScanGameObject(root, sceneName, targetLayerID, searchAll, false);
            }
            Debug.Log($"[SortingLayerFinder.ScanActiveScene] Scan complete. Found {_results.Count} matches in active scene.");
        }

        private void ScanProjectPrefabs(int targetLayerID, bool searchAll)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            int scannedCount = 0;

            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    EditorUtility.DisplayProgressBar("Scanning Prefabs", $"Scanning {i + 1}/{guids.Length}...", (float)i / guids.Length);

                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null)
                    {
                        ScanGameObject(prefab, path, targetLayerID, searchAll, true);
                        scannedCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SortingLayerFinder.ScanProjectPrefabs] Error scanning prefabs: {ex.Message}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            Debug.Log($"[SortingLayerFinder.ScanProjectPrefabs] Prefab scan complete. Scanned {scannedCount} prefabs, found {_results.Count} matches.");
        }

        private void ScanSpecificFolders(int targetLayerID, bool searchAll)
        {
            var folderPaths = _targetFolders
                .Where(f => f != null)
                .Select(f => AssetDatabase.GetAssetPath(f))
                .Where(p => AssetDatabase.IsValidFolder(p))
                .Distinct()
                .ToArray();

            if (folderPaths.Length == 0)
            {
                Debug.LogError("[SortingLayerFinder.ScanSpecificFolders] Boundary Check: No search folders configured.");
                return;
            }

            // Prefabs in these folders
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", folderPaths);
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    ScanGameObject(prefab, path, targetLayerID, searchAll, true);
                }
            }

            // Scenes in these folders
            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", folderPaths);
            if (sceneGuids.Length > 0)
            {
                ScanScenesList(sceneGuids.Select(AssetDatabase.GUIDToAssetPath).ToList(), targetLayerID, searchAll);
            }

            Debug.Log($"[SortingLayerFinder.ScanSpecificFolders] Specific folder scan complete. Found {_results.Count} matches.");
        }

        private void ScanBuildScenes(int targetLayerID, bool searchAll)
        {
            var scenes = EditorBuildSettings.scenes;
            var scenePaths = scenes.Where(s => s.enabled).Select(s => s.path).ToList();

            if (scenePaths.Count == 0)
            {
                Debug.LogError("[SortingLayerFinder.ScanBuildScenes] Boundary Check: No enabled scenes in Build Settings.");
                return;
            }

            ScanScenesList(scenePaths, targetLayerID, searchAll);
            Debug.Log($"[SortingLayerFinder.ScanBuildScenes] Build Scenes scan complete. Found {_results.Count} matches.");
        }

        private void ScanScenesList(List<string> scenePaths, int targetLayerID, bool searchAll)
        {
            if (scenePaths == null || scenePaths.Count == 0) return;

            // Prompt to save active scene changes first
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("[SortingLayerFinder.ScanScenesList] Action cancelled because active scene modifications were not saved.");
                return;
            }

            string originalScenePath = SceneManager.GetActiveScene().path;

            try
            {
                for (int i = 0; i < scenePaths.Count; i++)
                {
                    string scenePath = scenePaths[i];
                    if (string.IsNullOrEmpty(scenePath)) continue;

                    EditorUtility.DisplayProgressBar("Scanning Scenes", $"Opening {Path.GetFileName(scenePath)}...", (float)i / scenePaths.Count);

                    Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    var rootObjects = scene.GetRootGameObjects();
                    foreach (var root in rootObjects)
                    {
                        ScanGameObject(root, scenePath, targetLayerID, searchAll, false);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SortingLayerFinder.ScanScenesList] Error during scene scan: {ex.Message}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                // Restore original scene
                if (string.IsNullOrEmpty(originalScenePath))
                {
                    EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                }
                else
                {
                    EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
                }
            }
        }

        private void ScanGameObject(GameObject go, string sourcePath, int targetLayerID, bool searchAll, bool isPrefab)
        {
            if (go == null) return;

            // 1. Check Renderers
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r != null && (searchAll || r.sortingLayerID == targetLayerID))
                {
                    AddMatch(r.gameObject, r, "Renderer", r.sortingLayerName, r.sortingOrder, sourcePath, isPrefab);
                }
            }

            // 2. Check Canvases
            var canvases = go.GetComponentsInChildren<Canvas>(true);
            foreach (var c in canvases)
            {
                if (c != null && (searchAll || c.sortingLayerID == targetLayerID))
                {
                    AddMatch(c.gameObject, c, "Canvas", c.sortingLayerName, c.sortingOrder, sourcePath, isPrefab);
                }
            }

            // 3. Check SortingGroups
            var sortingGroups = go.GetComponentsInChildren<SortingGroup>(true);
            foreach (var sg in sortingGroups)
            {
                if (sg != null && (searchAll || sg.sortingLayerID == targetLayerID))
                {
                    AddMatch(sg.gameObject, sg, "SortingGroup", sg.sortingLayerName, sg.sortingOrder, sourcePath, isPrefab);
                }
            }
        }

        private void AddMatch(GameObject go, UnityEngine.Object component, string type, string layerName, int order, string path, bool isPrefab)
        {
            if (go == null || component == null)
            {
                Debug.LogError("[SortingLayerFinder.AddMatch] Boundary Check: GameObject or Component was null.");
                return;
            }

            if (_results.Any(r => r.TargetComponent == component)) return;

            var match = new SortingLayerMatch
            {
                GameObjectName = go.name,
                ComponentType = type,
                SortingLayerName = layerName,
                SortingOrder = order,
                IsPrefab = isPrefab,
                Path = path,
                HierarchyPath = GetHierarchyPath(go.transform),
                TargetComponent = component,
                TargetGameObject = go
            };
            _results.Add(match);
        }

        private string GetHierarchyPath(Transform t)
        {
            if (t == null) return string.Empty;
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }
        #endregion

        #region UI Rendering & Interaction
        private void RefreshResultsUI()
        {
            if (_resultsListContainer == null || _summaryLabel == null) return;

            _resultsListContainer.Clear();

            if (_results.Count == 0)
            {
                _summaryLabel.text = "No matching GameObjects found.";
                _summaryLabel.style.color = Color.gray;
                return;
            }

            _summaryLabel.text = $"Found {_results.Count} matching GameObject(s).";
            _summaryLabel.style.color = new Color(0.0f, 0.81f, 0.77f);

            for (int i = 0; i < _results.Count; i++)
            {
                var match = _results[i];
                var row = new VisualElement
                {
                    style = {
                        flexDirection = FlexDirection.Row,
                        marginBottom = 4,
                        paddingLeft = 8,
                        paddingRight = 8,
                        paddingTop = 6,
                        paddingBottom = 6,
                        backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.6f),
                        borderTopWidth = 1, borderBottomWidth = 1, borderLeftWidth = 1, borderRightWidth = 1,
                        borderTopColor = new Color(0.25f, 0.25f, 0.25f),
                        borderBottomColor = new Color(0.25f, 0.25f, 0.25f),
                        borderLeftColor = new Color(0.25f, 0.25f, 0.25f),
                        borderRightColor = new Color(0.25f, 0.25f, 0.25f),
                        borderTopLeftRadius = 4, borderTopRightRadius = 4, borderBottomLeftRadius = 4, borderBottomRightRadius = 4,
                        alignItems = Align.Center
                    }
                };

                var typeLabel = new Label($"[{match.ComponentType}]")
                {
                    style = {
                        width = 90,
                        unityFontStyleAndWeight = FontStyle.Bold,
                        fontSize = 10,
                        color = match.ComponentType == "Canvas" ? new Color(0.9f, 0.4f, 0.4f) :
                                match.ComponentType == "SortingGroup" ? new Color(0.4f, 0.7f, 0.9f) :
                                new Color(0.4f, 0.9f, 0.5f)
                    }
                };
                row.Add(typeLabel);

                var infoContainer = new VisualElement { style = { flexGrow = 1, marginLeft = 4, marginRight = 4 } };
                
                var nameLabel = new Label(match.GameObjectName)
                {
                    style = {
                        unityFontStyleAndWeight = FontStyle.Bold,
                        fontSize = 12,
                        color = Color.white
                    }
                };
                infoContainer.Add(nameLabel);

                var pathLabel = new Label($"{match.HierarchyPath} ({match.Path})")
                {
                    style = {
                        fontSize = 9,
                        color = Color.gray
                    }
                };
                infoContainer.Add(pathLabel);
                row.Add(infoContainer);

                var orderLabel = new Label($"Layer: {match.SortingLayerName} | Order: {match.SortingOrder}")
                {
                    style = {
                        width = 160,
                        unityTextAlign = TextAnchor.MiddleRight,
                        fontSize = 11,
                        marginRight = 8,
                        color = new Color(0.9f, 0.7f, 0.1f)
                    }
                };
                row.Add(orderLabel);

                var btnPing = new Button(() => PingMatch(match))
                {
                    text = "Ping",
                    style = { width = 50, height = 22, fontSize = 10, unityFontStyleAndWeight = FontStyle.Bold }
                };
                row.Add(btnPing);

                var btnSelect = new Button(() => SelectMatch(match))
                {
                    text = "Select",
                    style = {
                        width = 55,
                        height = 22,
                        fontSize = 10,
                        unityFontStyleAndWeight = FontStyle.Bold,
                        backgroundColor = new Color(0.0f, 0.47f, 0.74f),
                        color = Color.white,
                        marginLeft = 4
                    }
                };
                row.Add(btnSelect);

                _resultsListContainer.Add(row);
            }
        }

        private void PingMatch(SortingLayerMatch match)
        {
            if (match == null) return;
            Debug.Log($"[SortingLayerFinder.PingMatch] Pinging GameObject '{match.GameObjectName}' in {match.Path}");

            if (match.IsPrefab)
            {
                var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(match.Path);
                if (prefabAsset != null)
                {
                    EditorGUIUtility.PingObject(prefabAsset);
                }
                else
                {
                    Debug.LogError($"[SortingLayerFinder.PingMatch] Failed to load prefab asset for ping: {match.Path}");
                }
                return;
            }

            string activeScenePath = SceneManager.GetActiveScene().path;
            if (match.Path != activeScenePath && !string.IsNullOrEmpty(match.Path))
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(match.Path, OpenSceneMode.Single);
                    GameObject foundGo = GameObject.Find(match.HierarchyPath);
                    if (foundGo != null)
                    {
                        EditorGUIUtility.PingObject(foundGo);
                    }
                    else
                    {
                        Debug.LogError($"[SortingLayerFinder.PingMatch] Resolution failure: GameObject '{match.GameObjectName}' not found at path '{match.HierarchyPath}' in opened scene.");
                    }
                }
            }
            else
            {
                if (match.TargetGameObject != null)
                {
                    EditorGUIUtility.PingObject(match.TargetGameObject);
                }
                else
                {
                    GameObject foundGo = GameObject.Find(match.HierarchyPath);
                    if (foundGo != null)
                    {
                        EditorGUIUtility.PingObject(foundGo);
                    }
                    else
                    {
                        Debug.LogError($"[SortingLayerFinder.PingMatch] GameObject '{match.GameObjectName}' not found in active scene.");
                    }
                }
            }
        }

        private void SelectMatch(SortingLayerMatch match)
        {
            if (match == null) return;
            Debug.Log($"[SortingLayerFinder.SelectMatch] Selecting GameObject '{match.GameObjectName}' in {match.Path}");

            if (match.IsPrefab)
            {
                var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(match.Path);
                if (prefabAsset != null)
                {
                    Selection.activeObject = prefabAsset;
                    EditorGUIUtility.PingObject(prefabAsset);
                }
                else
                {
                    Debug.LogError($"[SortingLayerFinder.SelectMatch] Failed to load prefab asset for selection: {match.Path}");
                }
                return;
            }

            string activeScenePath = SceneManager.GetActiveScene().path;
            if (match.Path != activeScenePath && !string.IsNullOrEmpty(match.Path))
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(match.Path, OpenSceneMode.Single);
                    GameObject foundGo = GameObject.Find(match.HierarchyPath);
                    if (foundGo != null)
                    {
                        Selection.activeObject = foundGo;
                        EditorGUIUtility.PingObject(foundGo);
                    }
                    else
                    {
                        Debug.LogError($"[SortingLayerFinder.SelectMatch] Resolution failure: GameObject '{match.GameObjectName}' not found at path '{match.HierarchyPath}' in opened scene.");
                    }
                }
            }
            else
            {
                if (match.TargetGameObject != null)
                {
                    Selection.activeObject = match.TargetGameObject;
                    EditorGUIUtility.PingObject(match.TargetGameObject);
                }
                else
                {
                    GameObject foundGo = GameObject.Find(match.HierarchyPath);
                    if (foundGo != null)
                    {
                        Selection.activeObject = foundGo;
                        EditorGUIUtility.PingObject(foundGo);
                    }
                    else
                    {
                        Debug.LogError($"[SortingLayerFinder.SelectMatch] GameObject '{match.GameObjectName}' not found in active scene.");
                    }
                }
            }
        }
        #endregion
    }
}
#endif
