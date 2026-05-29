#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;

namespace NamPhuThuy.AssetPipelineTools
{
    public class Window_ComponentSetup : EditorWindow
    {
        #region Private Fields
        [SerializeField] private List<GameObject> _targetGameObjects = new List<GameObject>();

        // UI References
        private VisualElement _listContainer;
        private Label _summaryLabel;
        #endregion

        #region Menu Item
        [MenuItem("NamPhuThuy/Assets Pipeline/Window - Component Setup")]
        public static void ShowWindow()
        {
            var window = GetWindow<Window_ComponentSetup>("Component Setup");
            window.minSize = new Vector2(450, 600);
            window.Show();
        }
        #endregion

        #region Unity Callbacks
        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = 18;
            root.style.paddingRight = 18;
            root.style.paddingTop = 18;
            root.style.paddingBottom = 18;
            root.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 1f);

            // ── Header Section ──
            var header = new Label("Component Setup Tool")
            {
                style = { 
                    unityFontStyleAndWeight = FontStyle.Bold, 
                    fontSize = 18, 
                    unityTextAlign = TextAnchor.MiddleCenter, 
                    marginBottom = 8, 
                    color = new Color(0.3f, 0.75f, 1f) 
                }
            };
            root.Add(header);

            var helpBox = new HelpBox(
                "Batch configure PolygonCollider2D outlines.",
                HelpBoxMessageType.Info);
            root.Add(helpBox);

            var mainScroll = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1, marginTop = 10 } };
            root.Add(mainScroll);

            // ── Target list Section ──
            mainScroll.Add(BuildListSection());

            // ── Footer Control Buttons ──
            var buttonRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 10 } };
            
            var runBtn = new Button(ApplyCollidersToTargets) 
            { 
                text = "Generate Colliders", 
                style = { flexGrow = 1, height = 35, unityFontStyleAndWeight = FontStyle.Bold, backgroundColor = new Color(0.15f, 0.6f, 0.3f) } 
            };
            buttonRow.Add(runBtn);

            root.Add(buttonRow);

            RefreshListUI();
        }
        #endregion

        #region UI Builders
        

        private VisualElement BuildListSection()
        {
            var box = UITK_AssetPipelineHelper.BuildBox();

            var titleRow = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, alignItems = Align.Center, marginBottom = 6 } };
            var title = new Label("GameObjects") { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 13, color = Color.white } };
            _summaryLabel = new Label("0 items") { style = { fontSize = 11, unityFontStyleAndWeight = FontStyle.Italic, color = Color.gray } };
            
            titleRow.Add(title);
            titleRow.Add(_summaryLabel);
            box.Add(titleRow);

            var scroll = new ScrollView { style = { maxHeight = 320, minHeight = 160 } };
            _listContainer = new VisualElement();
            scroll.Add(_listContainer);
            box.Add(scroll);

            // Add slots & Selection buttons
            var listButtonsRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 10 } };
            
            var addSelectionBtn = new Button(AddSelectedFromHierarchy) 
            { 
                text = "Add Selected", 
                style = { flexGrow = 1.5f, height = 26, unityFontStyleAndWeight = FontStyle.Bold, backgroundColor = new Color(0.2f, 0.4f, 0.6f) } 
            };
            listButtonsRow.Add(addSelectionBtn);

            var clearBtn = new Button(ClearList) 
            { 
                text = "Clear", 
                style = { width = 80, height = 26, unityFontStyleAndWeight = FontStyle.Bold, backgroundColor = new Color(0.5f, 0.15f, 0.15f) } 
            };
            listButtonsRow.Add(clearBtn);

            box.Add(listButtonsRow);

            // Drag and drop area support
            var dragArea = new VisualElement 
            { 
                style = { 
                    borderTopWidth = 1, borderBottomWidth = 1, borderLeftWidth = 1, borderRightWidth = 1,
                    borderTopColor = new Color(0.35f, 0.35f, 0.35f, 0.5f), borderBottomColor = new Color(0.35f, 0.35f, 0.35f, 0.5f),
                    borderLeftColor = new Color(0.35f, 0.35f, 0.35f, 0.5f), borderRightColor = new Color(0.35f, 0.35f, 0.35f, 0.5f),
                    borderTopLeftRadius = 4, borderTopRightRadius = 4, borderBottomLeftRadius = 4, borderBottomRightRadius = 4,
                    paddingTop = 8, paddingBottom = 8, marginTop = 10,
                    alignItems = Align.Center, justifyContent = Justify.Center,
                    backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.5f)
                } 
            };
            
            dragArea.Add(new Label("Drag GameObjects Here") { style = { fontSize = 11, color = Color.gray, unityFontStyleAndWeight = FontStyle.Bold } });
            
            dragArea.RegisterCallback<DragUpdatedEvent>(_ =>
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            });
            
            dragArea.RegisterCallback<DragPerformEvent>(_ =>
            {
                DragAndDrop.AcceptDrag();
                Undo.RecordObject(this, "Drag and Drop targets");
                foreach (var obj in DragAndDrop.objectReferences)
                {
                    if (obj is GameObject go && !_targetGameObjects.Contains(go))
                    {
                        _targetGameObjects.Add(go);
                    }
                }
                RefreshListUI();
            });

            box.Add(dragArea);

            return box;
        }

        private void RefreshListUI()
        {
            if (_listContainer == null) return;
            
            _listContainer.Clear();

            _summaryLabel.text = $"{_targetGameObjects.Count} item(s)";

            for (int i = 0; i < _targetGameObjects.Count; i++)
            {
                int index = i;
                var go = _targetGameObjects[index];
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

                // Interactive ObjectField so the user can easily assign/change targets directly in the list
                var objField = new ObjectField 
                { 
                    value = go, 
                    objectType = typeof(GameObject), 
                    allowSceneObjects = true,
                    style = { flexGrow = 1 }
                };
                
                objField.RegisterValueChangedCallback(evt => 
                {
                    Undo.RecordObject(this, "Change Target GameObject Slot");
                    _targetGameObjects[index] = evt.newValue as GameObject;
                });
                
                row.Add(objField);

                // Quick Action buttons: Ping/Select and Remove
                var selectBtn = new Button(() => 
                {
                    if (go != null)
                    {
                        Selection.activeGameObject = go;
                        EditorGUIUtility.PingObject(go);
                    }
                }) 
                { 
                    text = "🔎", 
                    style = { width = 28, height = 20, unityFontStyleAndWeight = FontStyle.Bold, marginLeft = 4 } 
                };
                row.Add(selectBtn);

                var removeBtn = new Button(() => 
                {
                    Undo.RecordObject(this, "Remove Target GameObject");
                    _targetGameObjects.RemoveAt(index);
                    RefreshListUI();
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

        #region Core Setup Methods
        private void AddSelectedFromHierarchy()
        {
            var selected = Selection.gameObjects;
            if (selected.Length == 0)
            {
                EditorUtility.DisplayDialog("Warning", "No selection.", "OK");
                return;
            }

            Undo.RecordObject(this, "Add Selection to Targets");
            int addedCount = 0;
            foreach (var go in selected)
            {
                if (!_targetGameObjects.Contains(go))
                {
                    _targetGameObjects.Add(go);
                    addedCount++;
                }
            }

            RefreshListUI();

            if (addedCount == 0)
            {
                Debug.Log("[Component Setup] Already in list.");
            }
        }

        private void ApplyCollidersToTargets()
        {
            // Clean up any trailing null fields before execution
            _targetGameObjects.RemoveAll(go => go == null);
            RefreshListUI();

            if (_targetGameObjects.Count == 0)
            {
                EditorUtility.DisplayDialog("Warning", "Empty.", "OK");
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Component Setup - Apply Polygon Colliders");
            int undoGroup = Undo.GetCurrentGroup();

            int successCount = 0;
            int failedCount = 0;

            foreach (var go in _targetGameObjects)
            {
                if (go == null) continue;

                var spriteRenderer = go.GetComponent<SpriteRenderer>();
                if (spriteRenderer == null)
                {
                    Debug.LogError($"[Component Setup] '{go.name}' no SpriteRenderer -> failed", go);
                    failedCount++;
                    continue;
                }

                AddPolygonColliderToMatchSprite(go);
                successCount++;
            }

            Undo.CollapseUndoOperations(undoGroup);

            EditorUtility.DisplayDialog("Done", 
                $"Success={successCount}, Failed={failedCount}", "OK");
        }

        private void AddPolygonColliderToMatchSprite(GameObject go)
        {
            if (go == null)
            {
                Debug.LogError("[Component Setup] target GameObject is null -> failed");
                return;
            }

            // Check if it is a prefab asset
            bool isPrefabAsset = PrefabUtility.IsPartOfPrefabAsset(go);
            string assetPath = isPrefabAsset ? AssetDatabase.GetAssetPath(go) : null;

            GameObject targetGo = go;
            GameObject prefabRoot = null;

            if (isPrefabAsset && !string.IsNullOrEmpty(assetPath))
            {
                // Load prefab contents to modify it safely
                prefabRoot = PrefabUtility.LoadPrefabContents(assetPath);
                targetGo = prefabRoot;
            }

            var spriteRenderer = targetGo.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                Debug.LogError($"[Component Setup] '{go.name}''s SpriteRenderer component is null on GameObject -> failed", go);
                if (prefabRoot != null)
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
                return;
            }

            if (spriteRenderer.sprite == null)
            {
                Debug.LogError($"[Component Setup] '{go.name}''s Sprite is null -> failed.", go);
                if (prefabRoot != null)
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
                return;
            }

            // Perform modification
            if (!isPrefabAsset)
            {
                // Register undo for Scene GameObject
                Undo.RegisterCompleteObjectUndo(targetGo, "Update Sprite Collider Shape");
            }

            // Clean up old collider
            var existingCollider = targetGo.GetComponent<PolygonCollider2D>();
            if (existingCollider != null)
            {
                if (isPrefabAsset)
                {
                    DestroyImmediate(existingCollider, true);
                }
                else
                {
                    Undo.DestroyObjectImmediate(existingCollider);
                }
            }

            // Add new collider
            PolygonCollider2D newCollider;
            if (isPrefabAsset)
            {
                newCollider = targetGo.AddComponent<PolygonCollider2D>();
            }
            else
            {
                newCollider = Undo.AddComponent<PolygonCollider2D>(targetGo);
            }

            // Copy physics shape points directly to match the sprite outline perfectly
            Sprite sprite = spriteRenderer.sprite;
            int shapeCount = sprite.GetPhysicsShapeCount();
            newCollider.pathCount = shapeCount;

            var pathList = new List<Vector2>();
            for (int i = 0; i < shapeCount; i++)
            {
                pathList.Clear();
                sprite.GetPhysicsShape(i, pathList);
                newCollider.SetPath(i, pathList.ToArray());
            }

            // Save and unload prefab if it was a prefab asset
            if (isPrefabAsset && prefabRoot != null)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, assetPath);
                PrefabUtility.UnloadPrefabContents(prefabRoot);
                // Reimport to apply changes to the database
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            }

            Debug.Log($"[Component Setup] Successfully applied PolygonCollider2D to '{go.name}'.", go);
        }

        private void ClearList()
        {
            if (_targetGameObjects.Count == 0) return;
            
            Undo.RecordObject(this, "Clear Target List");
            _targetGameObjects.Clear();
            RefreshListUI();
        }
        #endregion
    }
}
#endif
