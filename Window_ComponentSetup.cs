#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using UnityEditor;
using UnityEditor.UIElements;

namespace NamPhuThuy.AssetPipelineTools
{
    public class Window_ComponentSetup : EditorWindow
    {
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
        // ───────────────────────────────────────────────────────────────────────

        public enum TargetColliderType
        {
            POLYGON_2D = 0,
            EDGE_2D = 1
        }

        #region Private Fields
        [SerializeField] private List<GameObject> _targetGameObjects = new List<GameObject>();
        [SerializeField] private TargetColliderType _colliderType = TargetColliderType.POLYGON_2D;
        [SerializeField] private float _simplificationTolerance = 0.01f;

        // EditorPrefs Keys
        private const string PREF_KEY_COLLIDER_TYPE = "NamPhuThuy_ComponentSetup_ColliderType";
        private const string PREF_KEY_SIMPLIFICATION_TOLERANCE = "NamPhuThuy_ComponentSetup_SimplificationTolerance";

        // UI References
        private PropertyField _targetsPropertyField;
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
        private void OnEnable()
        {
            _colliderType = (TargetColliderType)EditorPrefs.GetInt(PREF_KEY_COLLIDER_TYPE, (int)TargetColliderType.POLYGON_2D);
            _simplificationTolerance = EditorPrefs.GetFloat(PREF_KEY_SIMPLIFICATION_TOLERANCE, 0.01f);
        }

        private void OnDisable()
        {
            EditorPrefs.SetInt(PREF_KEY_COLLIDER_TYPE, (int)_colliderType);
            EditorPrefs.SetFloat(PREF_KEY_SIMPLIFICATION_TOLERANCE, _simplificationTolerance);
        }

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
                "Batch configure Polygon/Edge 2D Colliders.\n\n" +
                "• In-Place: Modifies existing colliders without deleting them.\n" +
                "• Tolerance (RDP Algorithm):\n" +
                "  - Low (0.005): Precise contour, higher point count.\n" +
                "  - High (0.05): Simplified contour, lower point count (better performance).\n" +
                "• Steps: Set Type & Tolerance → Add GameObjects → Generate.",
                HelpBoxMessageType.Info);
            helpBox.style.marginBottom = 10;
            root.Add(helpBox);

            var mainScroll = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1 } };
            root.Add(mainScroll);

            // ── Settings Section ──
            mainScroll.Add(BuildSettingsSection());

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
        }
        #endregion

        #region UI Builders
        private float GetSliderValueFromTolerance(float tolerance)
        {
            tolerance = Mathf.Clamp(tolerance, 0.001f, 0.5f);
            return Mathf.Log(tolerance / 0.001f) / Mathf.Log(500f);
        }

        private float GetToleranceFromSliderValue(float sliderVal)
        {
            sliderVal = Mathf.Clamp01(sliderVal);
            return 0.001f * Mathf.Pow(500f, sliderVal);
        }

        private Color GetToleranceColorFromSlider(float u)
        {
            u = Mathf.Clamp01(u);
            if (u < 0.5f)
            {
                return Color.Lerp(new Color(0.85f, 0.2f, 0.2f), new Color(0.85f, 0.7f, 0.1f), u * 2f);
            }
            else
            {
                return Color.Lerp(new Color(0.85f, 0.7f, 0.1f), new Color(0.2f, 0.65f, 0.2f), (u - 0.5f) * 2f);
            }
        }

        private VisualElement BuildSettingsSection()
        {
            var box = UITK_AssetPipelineHelper.BuildBox("Settings");

            var colliderTypeField = new EnumField("Target Collider Type", _colliderType);
            colliderTypeField.RegisterValueChangedCallback(evt =>
            {
                _colliderType = (TargetColliderType)evt.newValue;
            });
            box.Add(colliderTypeField);

            // Row containing the non-linear slider and the value badge
            var toleranceRow = new VisualElement 
            { 
                style = { 
                    flexDirection = FlexDirection.Row, 
                    alignItems = Align.Center,
                    marginTop = 4
                } 
            };

            float initialSliderVal = GetSliderValueFromTolerance(_simplificationTolerance);

            var toleranceSlider = new Slider("Tolerance", 0f, 1f)
            {
                value = initialSliderVal,
                style = { flexGrow = 1 }
            };

            var toleranceLabel = new Label
            {
                text = _simplificationTolerance.ToString("F3"),
                style =
                {
                    width = 55,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    marginLeft = 8,
                    paddingTop = 2,
                    paddingBottom = 2,
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = Color.black
                }
            };

            void UpdateToleranceVisuals(float u)
            {
                float calculatedTolerance = GetToleranceFromSliderValue(u);
                toleranceLabel.text = calculatedTolerance.ToString("F3");

                Color dynamicColor = GetToleranceColorFromSlider(u);
                toleranceLabel.style.backgroundColor = dynamicColor;

                var dragger = toleranceSlider.Q<VisualElement>("unity-dragger");
                if (dragger != null)
                {
                    dragger.style.backgroundColor = dynamicColor;
                }
            }

            toleranceSlider.RegisterValueChangedCallback(evt =>
            {
                _simplificationTolerance = GetToleranceFromSliderValue(evt.newValue);
                UpdateToleranceVisuals(evt.newValue);
            });

            toleranceSlider.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                float currentSliderVal = GetSliderValueFromTolerance(_simplificationTolerance);
                UpdateToleranceVisuals(currentSliderVal);
            });

            toleranceRow.Add(toleranceSlider);
            toleranceRow.Add(toleranceLabel);
            box.Add(toleranceRow);

            var resetBtn = new Button(() =>
            {
                _colliderType = TargetColliderType.POLYGON_2D;
                _simplificationTolerance = 0.01f;
                colliderTypeField.value = TargetColliderType.POLYGON_2D;
                
                float defaultSliderVal = GetSliderValueFromTolerance(0.01f);
                toleranceSlider.value = defaultSliderVal;
                UpdateToleranceVisuals(defaultSliderVal);
            })
            {
                text = "Reset to Defaults",
                style = { marginTop = 8, height = 22, unityFontStyleAndWeight = FontStyle.Bold, backgroundColor = new Color(0.3f, 0.3f, 0.3f) }
            };
            box.Add(resetBtn);

            return box;
        }

        private VisualElement BuildListSection()
        {
            var box = UITK_AssetPipelineHelper.BuildBox("GameObjects");

            // Automatically bind list of gameobjects using SerializedObject
            SerializedObject so = new SerializedObject(this);
            SerializedProperty targetsProp = so.FindProperty("_targetGameObjects");

            _targetsPropertyField = new PropertyField(targetsProp, "Target GameObjects");
            _targetsPropertyField.Bind(so);
            box.Add(_targetsPropertyField);

            // Add slots & Selection buttons
            var listButtonsRow = new VisualElement 
            { 
                style = { 
                    flexDirection = FlexDirection.Row, 
                    marginTop = 8,
                    justifyContent = Justify.SpaceBetween
                } 
            };
            
            var addSelectionBtn = new Button(AddSelectedFromHierarchy) 
            { 
                text = "Add Selected", 
                style = { flexGrow = 1, height = 25, unityFontStyleAndWeight = FontStyle.Bold, backgroundColor = new Color(0.2f, 0.5f, 0.4f), marginRight = 4 } 
            };
            listButtonsRow.Add(addSelectionBtn);

            var clearBtn = new Button(ClearList) 
            { 
                text = "Clear List", 
                style = { flexGrow = 1, height = 25, unityFontStyleAndWeight = FontStyle.Bold, backgroundColor = new Color(0.6f, 0.2f, 0.2f), marginLeft = 4 } 
            };
            listButtonsRow.Add(clearBtn);

            box.Add(listButtonsRow);

            return box;
        }

        private void ClearList()
        {
            SerializedObject so = new SerializedObject(this);
            SerializedProperty targetsProp = so.FindProperty("_targetGameObjects");
            targetsProp.ClearArray();
            so.ApplyModifiedProperties();
            _targetsPropertyField.Bind(so);
            Debug.Log("Cleared target GameObjects list.");
        }
        #endregion

        #region Core Setup Methods
        private void AddSelectedFromHierarchy()
        {
            var selected = Selection.gameObjects;
            if (selected.Length == 0)
            {
                Debug.LogWarning("[Component Setup] No selection.");
                return;
            }

            SerializedObject so = new SerializedObject(this);
            SerializedProperty targetsProp = so.FindProperty("_targetGameObjects");

            var existingObjects = new HashSet<GameObject>();
            for (int i = 0; i < targetsProp.arraySize; i++)
            {
                var element = targetsProp.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                if (element != null)
                {
                    existingObjects.Add(element);
                }
            }

            int addedCount = 0;
            foreach (var go in selected)
            {
                if (go != null && !existingObjects.Contains(go))
                {
                    int index = targetsProp.arraySize;
                    targetsProp.InsertArrayElementAtIndex(index);
                    targetsProp.GetArrayElementAtIndex(index).objectReferenceValue = go;
                    existingObjects.Add(go);
                    addedCount++;
                }
            }

            if (addedCount > 0)
            {
                so.ApplyModifiedProperties();
                _targetsPropertyField.Bind(so);
                Debug.Log($"[Component Setup] Added {addedCount} GameObjects from selection.");
            }
        }

        private void ApplyCollidersToTargets()
        {
            _targetGameObjects.RemoveAll(go => go == null);
            
            // Re-bind to ensure UI reflects any removed null items
            SerializedObject so = new SerializedObject(this);
            _targetsPropertyField.Bind(so);

            if (_targetGameObjects.Count == 0)
            {
                Debug.LogError("[Component Setup] GameObjects target list is empty.");
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName($"Component Setup - Apply {_colliderType} Colliders");
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

                if (_colliderType == TargetColliderType.POLYGON_2D)
                {
                    AddPolygonColliderToMatchSprite(go);
                }
                else
                {
                    AddEdgeColliderToMatchSprite(go);
                }
                successCount++;
            }

            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log($"[Component Setup] Batch generation finished. Success: {successCount}, Failed: {failedCount}.");
        }

        private void AddPolygonColliderToMatchSprite(GameObject go)
        {
            if (go == null)
            {
                Debug.LogError("[Component Setup] target GameObject is null -> failed");
                return;
            }

            bool isPrefabAsset = PrefabUtility.IsPartOfPrefabAsset(go);
            string assetPath = isPrefabAsset ? AssetDatabase.GetAssetPath(go) : null;

            GameObject targetGo = go;
            GameObject prefabRoot = null;

            if (isPrefabAsset && !string.IsNullOrEmpty(assetPath))
            {
                prefabRoot = PrefabUtility.LoadPrefabContents(assetPath);
                targetGo = prefabRoot;
            }

            var spriteRenderer = targetGo.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null || spriteRenderer.sprite == null)
            {
                Debug.LogError($"[Component Setup] '{go.name}''s SpriteRenderer/Sprite is null on GameObject -> failed", go);
                if (prefabRoot != null)
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
                return;
            }

            // Clean up the other type of collider (EdgeCollider2D) to avoid overlap/duplication
            var oldEdge = targetGo.GetComponent<EdgeCollider2D>();
            if (oldEdge != null)
            {
                if (isPrefabAsset) DestroyImmediate(oldEdge, true);
                else Undo.DestroyObjectImmediate(oldEdge);
            }

            // Get or Add PolygonCollider2D without recreating if it exists
            PolygonCollider2D collider = targetGo.GetComponent<PolygonCollider2D>();
            bool isNewComponent = (collider == null);

            if (isNewComponent)
            {
                if (isPrefabAsset)
                {
                    collider = targetGo.AddComponent<PolygonCollider2D>();
                }
                else
                {
                    collider = Undo.AddComponent<PolygonCollider2D>(targetGo);
                }
            }
            else
            {
                if (!isPrefabAsset)
                {
                    Undo.RecordObject(collider, "Modify PolygonCollider2D Shape");
                }
            }

            Sprite sprite = spriteRenderer.sprite;
            int shapeCount = sprite.GetPhysicsShapeCount();

            if (shapeCount > 0)
            {
                collider.pathCount = shapeCount;
                var pathList = new List<Vector2>();
                for (int i = 0; i < shapeCount; i++)
                {
                    pathList.Clear();
                    sprite.GetPhysicsShape(i, pathList);
                    
                    // Simplify the points to limit point count based on tolerance
                    var simplified = SimplifyPoints(pathList, _simplificationTolerance);
                    collider.SetPath(i, simplified.ToArray());
                }
            }
            else
            {
                // Fallback to rectangular outline
                var rect = sprite.rect;
                float w = rect.width / sprite.pixelsPerUnit;
                float h = rect.height / sprite.pixelsPerUnit;
                var pivot = sprite.pivot / sprite.pixelsPerUnit;

                float xMin = -pivot.x;
                float xMax = w - pivot.x;
                float yMin = -pivot.y;
                float yMax = h - pivot.y;

                collider.pathCount = 1;
                var points = new[]
                {
                    new Vector2(xMin, yMin),
                    new Vector2(xMax, yMin),
                    new Vector2(xMax, yMax),
                    new Vector2(xMin, yMax)
                };
                collider.SetPath(0, points);
            }

            if (isPrefabAsset && prefabRoot != null)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, assetPath);
                PrefabUtility.UnloadPrefabContents(prefabRoot);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            }

            Debug.Log($"[Component Setup] Successfully applied PolygonCollider2D to '{go.name}'.", go);
        }

        private void AddEdgeColliderToMatchSprite(GameObject go)
        {
            if (go == null)
            {
                Debug.LogError("[Component Setup] target GameObject is null -> failed");
                return;
            }

            bool isPrefabAsset = PrefabUtility.IsPartOfPrefabAsset(go);
            string assetPath = isPrefabAsset ? AssetDatabase.GetAssetPath(go) : null;

            GameObject targetGo = go;
            GameObject prefabRoot = null;

            if (isPrefabAsset && !string.IsNullOrEmpty(assetPath))
            {
                prefabRoot = PrefabUtility.LoadPrefabContents(assetPath);
                targetGo = prefabRoot;
            }

            var spriteRenderer = targetGo.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null || spriteRenderer.sprite == null)
            {
                Debug.LogError($"[Component Setup] '{go.name}''s SpriteRenderer/Sprite is null on GameObject -> failed", go);
                if (prefabRoot != null)
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
                return;
            }

            // Clean up the other type of collider (PolygonCollider2D) to avoid overlap/duplication
            var oldPoly = targetGo.GetComponent<PolygonCollider2D>();
            if (oldPoly != null)
            {
                if (isPrefabAsset) DestroyImmediate(oldPoly, true);
                else Undo.DestroyObjectImmediate(oldPoly);
            }

            // Get or Add EdgeCollider2D without recreating if it exists
            EdgeCollider2D collider = targetGo.GetComponent<EdgeCollider2D>();
            bool isNewComponent = (collider == null);

            if (isNewComponent)
            {
                if (isPrefabAsset)
                {
                    collider = targetGo.AddComponent<EdgeCollider2D>();
                }
                else
                {
                    collider = Undo.AddComponent<EdgeCollider2D>(targetGo);
                }
            }
            else
            {
                if (!isPrefabAsset)
                {
                    Undo.RecordObject(collider, "Modify EdgeCollider2D Shape");
                }
            }

            Sprite sprite = spriteRenderer.sprite;
            int shapeCount = sprite.GetPhysicsShapeCount();

            if (shapeCount > 0)
            {
                var pathList = new List<Vector2>();
                sprite.GetPhysicsShape(0, pathList);

                // Simplify points to limit count
                var simplified = SimplifyPoints(pathList, _simplificationTolerance);

                // Close the loop
                if (simplified.Count > 0 && simplified[0] != simplified[simplified.Count - 1])
                {
                    simplified.Add(simplified[0]);
                }
                collider.SetPoints(simplified);
            }
            else
            {
                // Fallback to rectangular outline
                var rect = sprite.rect;
                float w = rect.width / sprite.pixelsPerUnit;
                float h = rect.height / sprite.pixelsPerUnit;
                var pivot = sprite.pivot / sprite.pixelsPerUnit;

                float xMin = -pivot.x;
                float xMax = w - pivot.x;
                float yMin = -pivot.y;
                float yMax = h - pivot.y;

                var points = new List<Vector2>
                {
                    new Vector2(xMin, yMin),
                    new Vector2(xMax, yMin),
                    new Vector2(xMax, yMax),
                    new Vector2(xMin, yMax),
                    new Vector2(xMin, yMin)
                };
                collider.SetPoints(points);
            }

            if (isPrefabAsset && prefabRoot != null)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, assetPath);
                PrefabUtility.UnloadPrefabContents(prefabRoot);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            }

            Debug.Log($"[Component Setup] Successfully applied EdgeCollider2D to '{go.name}'.", go);
        }

        #endregion

        #region Point Simplification Algorithm (Ramer-Douglas-Peucker)
        private List<Vector2> SimplifyPoints(List<Vector2> points, float tolerance)
        {
            if (points == null || points.Count < 3 || tolerance <= 0f) return points;

            bool[] keep = new bool[points.Count];
            for (int i = 0; i < keep.Length; i++) keep[i] = true;

            SimplifySection(points, 0, points.Count - 1, tolerance, keep);

            List<Vector2> result = new List<Vector2>();
            for (int i = 0; i < points.Count; i++)
            {
                if (keep[i]) result.Add(points[i]);
            }
            return result;
        }

        private void SimplifySection(List<Vector2> points, int start, int end, float tolerance, bool[] keep)
        {
            if (end - start < 2) return;

            float maxDistSq = 0f;
            int maxIndex = -1;

            Vector2 pStart = points[start];
            Vector2 pEnd = points[end];
            Vector2 lineVec = pEnd - pStart;
            float lineLenSq = lineVec.sqrMagnitude;

            for (int i = start + 1; i < end; i++)
            {
                float distSq = 0f;
                if (lineLenSq == 0f)
                {
                    distSq = (points[i] - pStart).sqrMagnitude;
                }
                else
                {
                    float t = Vector2.Dot(points[i] - pStart, lineVec) / lineLenSq;
                    t = Mathf.Clamp01(t);
                    Vector2 projection = pStart + t * lineVec;
                    distSq = (points[i] - projection).sqrMagnitude;
                }

                if (distSq > maxDistSq)
                {
                    maxDistSq = distSq;
                    maxIndex = i;
                }
            }

            if (maxIndex != -1 && maxDistSq > tolerance * tolerance)
            {
                SimplifySection(points, start, maxIndex, tolerance, keep);
                SimplifySection(points, maxIndex, end, tolerance, keep);
            }
            else
            {
                for (int i = start + 1; i < end; i++)
                {
                    keep[i] = false;
                }
            }
        }
        #endregion
    }
}
#endif
