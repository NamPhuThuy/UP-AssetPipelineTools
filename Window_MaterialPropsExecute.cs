#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace NamPhuThuy.AssetPipelineTools
{
    public class Window_MaterialPropsExecute : EditorWindow
    {
        #region Private Fields
        private Vector2 _scrollPos;
        private GUIStyle _centeredButtonStyle;
        private GUIStyle _centeredLabelStyle;

        [SerializeField] private List<Material> _materialListA = new List<Material>();
        [SerializeField] private List<Material> _materialListB = new List<Material>();

        private SerializedObject _so;
        private SerializedProperty _propListA;
        private SerializedProperty _propListB;
        #endregion

        #region Menu Item
        [MenuItem("NamPhuThuy/Assets Pipeline/Window - Material Properties Execute")]
        public static void ShowWindow()
        {
            Window_MaterialPropsExecute window = GetWindow<Window_MaterialPropsExecute>("Material Props");
            window.minSize = new Vector2(500, 400);
            window.Show();
        }
        #endregion

        #region Unity Callbacks
        private void OnEnable()
        {
            _so = new SerializedObject(this);
            _propListA = _so.FindProperty("_materialListA");
            _propListB = _so.FindProperty("_materialListB");
        }

        private void OnGUI()
        {
            InitializeStyles();

            float padding = 20f;
            Rect areaRect = new Rect(padding, padding, position.width - 2 * padding, position.height - 2 * padding);

            GUILayout.BeginArea(areaRect);
            
            // Main scroll view that wraps everything
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            DrawHeader();
            GUILayout.Space(10);
            DrawContent();
            GUILayout.Space(20);
            DrawButtons();

            EditorGUILayout.EndScrollView();
            
            GUILayout.EndArea();
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

            if (_centeredLabelStyle == null)
            {
                _centeredLabelStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 16
                };
            }
        }

        private void DrawHeader()
        {
            GUILayout.Label("Material Properties Execute", _centeredLabelStyle);
            EditorGUILayout.HelpBox(
                "Copy/swap properties between two material lists of equal size.",
                MessageType.Info);
        }

        private void DrawContent()
        {
            _so.Update();

            EditorGUILayout.BeginHorizontal();
            
            EditorGUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("List A", _centeredLabelStyle);
            GUILayout.Space(5);
            EditorGUILayout.PropertyField(_propListA, true);
            
            GUILayout.Space(10);
            if (GUILayout.Button("Add Selected", GUILayout.Height(25)))
            {
                AddSelectedMaterials(_propListA);
            }
            if (GUILayout.Button("Clear", GUILayout.Height(25)))
            {
                _propListA.ClearArray();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("List B", _centeredLabelStyle);
            GUILayout.Space(5);
            EditorGUILayout.PropertyField(_propListB, true);
            
            GUILayout.Space(10);
            if (GUILayout.Button("Add Selected", GUILayout.Height(25)))
            {
                AddSelectedMaterials(_propListB);
            }
            if (GUILayout.Button("Clear", GUILayout.Height(25)))
            {
                _propListB.ClearArray();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            _so.ApplyModifiedProperties();
        }

        private void DrawButtons()
        {
            bool hasValidLists = _materialListA.Count > 0 && _materialListB.Count > 0 && _materialListA.Count == _materialListB.Count;

            if (!hasValidLists)
            {
                EditorGUILayout.HelpBox("Size mismatch or empty.", MessageType.Warning);
            }

            GUI.enabled = hasValidLists;

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Space(5);
            
            if (GUILayout.Button("Copy A \u2192 B", _centeredButtonStyle, GUILayout.Height(30)))
            {
                CopyPropertiesList(_materialListA, _materialListB);
            }
            
            GUILayout.Space(5);
            
            if (GUILayout.Button("Copy B \u2192 A", _centeredButtonStyle, GUILayout.Height(30)))
            {
                CopyPropertiesList(_materialListB, _materialListA);
            }
            
            GUILayout.Space(10);
            
            if (GUILayout.Button("Swap A \u21c4 B", _centeredButtonStyle, GUILayout.Height(30)))
            {
                SwapPropertiesList(_materialListA, _materialListB);
            }
            
            GUILayout.Space(5);
            GUILayout.EndVertical();

            GUI.enabled = true;
            
            GUILayout.Space(10);
            
            Color oldBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
            if (GUILayout.Button("Clear All", GUILayout.Height(30)))
            {
                _so.Update();
                _propListA.ClearArray();
                _propListB.ClearArray();
                _so.ApplyModifiedProperties();
            }
            GUI.backgroundColor = oldBg;
        }
        #endregion

        #region Private Methods

        private void AddSelectedMaterials(SerializedProperty propList)
        {
            foreach (Object obj in Selection.objects)
            {
                if (obj is Material mat)
                {
                    propList.arraySize++;
                    propList.GetArrayElementAtIndex(propList.arraySize - 1).objectReferenceValue = mat;
                }
            }
        }
        
        /// <summary>
        /// Copies all properties and shader from source list to target list safely.
        /// This handles overlapping lists/references by buffering the original source state before making modifications.
        /// </summary>
        private void CopyPropertiesList(List<Material> sourceList, List<Material> targetList)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName($"Copy Materials List");
            int undoGroup = Undo.GetCurrentGroup();

            int count = sourceList.Count;
            
            // Record all targets before modifying them
            for (int i = 0; i < count; i++)
            {
                if (targetList[i] != null)
                {
                    Undo.RecordObject(targetList[i], "Copy Material Properties");
                }
            }

            // Step 1: Buffer the original source materials to avoid cross-overwriting issues
            // This prevents the edge case where modifying a target alters a later source reference.
            Material[] tempSources = new Material[count];
            for (int i = 0; i < count; i++)
            {
                if (sourceList[i] != null)
                {
                    tempSources[i] = new Material(sourceList[i]);
                    tempSources[i].shader = sourceList[i].shader;
                }
            }

            // Step 2: Apply buffered properties to targets
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

            // Step 3: Cleanup temporary materials
            for (int i = 0; i < count; i++)
            {
                if (tempSources[i] != null)
                {
                    DestroyImmediate(tempSources[i]);
                }
            }

            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log($"Done: {count}");
        }

        /// <summary>
        /// Swaps all properties and shaders between List A and List B.
        /// Evaluated synchronously using buffered original materials to avoid overwrite collisions.
        /// </summary>
        private void SwapPropertiesList(List<Material> listA, List<Material> listB)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName($"Swap Materials List");
            int undoGroup = Undo.GetCurrentGroup();

            int count = listA.Count;

            // Record both lists
            for (int i = 0; i < count; i++)
            {
                if (listA[i] != null) Undo.RecordObject(listA[i], "Swap Material Properties");
                if (listB[i] != null) Undo.RecordObject(listB[i], "Swap Material Properties");
            }

            // Step 1: Buffer original materials for A and B
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

            // Step 2: Apply properties
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

            // Step 3: Cleanup
            for (int i = 0; i < count; i++)
            {
                if (tempA[i] != null) DestroyImmediate(tempA[i]);
                if (tempB[i] != null) DestroyImmediate(tempB[i]);
            }

            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log($"Done: {count}");
        }
        #endregion
    }
}
#endif
