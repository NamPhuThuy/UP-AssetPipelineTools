# DEV_LOG — UP-AssetPipelineTools
Development log documenting key problems encountered and the solutions applied while building each tool.

# Window_AssetNaming

## Problem 1: `Object.name` returns wrong name for some asset types

**Symptom:** Shaders and certain imported assets returned an incorrect name via `record.targetAsset.name`. For example, a Shader file named `MyShader.shader` on disk would return its internal declaration name like `Custom/MyShader` instead.

**Root Cause:** Unity's `Object.name` property returns the *internal* name of the asset, which some asset types (Shaders, imported models) override independently of their file name on disk.

**Solution:** Created `GetAssetFileName()` which uses `AssetDatabase.GetAssetPath()` + `Path.GetFileNameWithoutExtension()` to always return the actual file name on disk. All name reads now use this method instead of `Object.name`.

```csharp
private string GetAssetFileName(Object asset)
{
    string assetPath = AssetDatabase.GetAssetPath(asset);
    if (string.IsNullOrEmpty(assetPath))
        return asset.name; // fallback for non-persistent assets
    return System.IO.Path.GetFileNameWithoutExtension(assetPath);
}
```

## Problem 2: Unity's Undo system cannot reverse `AssetDatabase.RenameAsset`

**Symptom:** After renaming assets, pressing Ctrl+Z did nothing - files stayed renamed on disk.

**Root Cause:** `AssetDatabase.RenameAsset()` is a **file system operation** that physically renames the file on disk. Unity's `Undo.RecordObject()` only tracks in-memory serialized state changes (component properties, ScriptableObject fields). It has no mechanism to reverse disk operations.

**Failed Attempt - Shadow History Pattern:**
Tried using `[SerializeField] _renameHistory` tracked by `Undo.RecordObject(this, ...)` with a `[NonSerialized] _shadowHistory` copy. On `Undo.undoRedoPerformed`, compared counts to detect undo/redo. This failed because:
- `Undo.RecordObject` on an `EditorWindow` doesn't reliably serialize/deserialize complex nested `List<List<T>>` structures.
- The shadow copy would desync after domain reloads or window re-opens.

**Final Solution - Self-managed undo/redo stacks:**
Removed all dependency on Unity's Undo system for file renames. Instead:
- Two `[NonSerialized]` lists: `_undoStack` and `_redoStack`, each holding `RenameHistoryBatch` objects (GUID + old name + new name).
- `PerformBatchRename()` pushes to `_undoStack` and clears `_redoStack`.
- `UndoLastRename()` pops from `_undoStack`, reverses file renames using stored GUIDs, pushes to `_redoStack`.
- `RedoLastRename()` does the reverse.
- Uses `AssetDatabase.AssetPathToGUID()` / `GUIDToAssetPath()` so undo still works even if the asset was moved to a different folder between operations.

```
[Rename] → push to _undoStack, clear _redoStack
[Undo]   → pop _undoStack, rename back, push to _redoStack
[Redo]   → pop _redoStack, rename forward, push to _undoStack
```

## Problem 3: Case conversion must handle mixed naming conventions
**Symptom:** Converting `myHTMLParser_config` to snake_case or camelCase produced incorrect results when using simple `String.Split` on separators alone.

**Solution:** Created `SplitIntoWords()` that splits by **both** separators (`_`, `-`, `.`, space) **and** casing boundaries. This handles:
- `camelCase` → `camel`, `Case`
- `HTMLParser` → `HTML`, `Parser` (uppercase run followed by lowercase)
- `my_asset-name` → `my`, `asset`, `name`

Each case mode then reassembles from the word list:
| Mode | Join logic |
|---|---|
| camelCase | first word lower, rest capitalized, no separator |
| PascalCase | all words capitalized, no separator |
| snake_case | all words lower, joined with `_` |
| Title Case | preserves original separators, capitalizes first letter of each segment |

## Problem 4: Removing N occurrences of a substring from a specific direction
**Symptom:** `String.Replace()` always removes **all** occurrences — no way to remove only the first 2, or only from the right.

**Solution:** Created `RemoveSubstringOccurrences()`:
1. Find all non-overlapping occurrence indices with `IndexOf` in a loop.
2. Select which to remove: `Take(N)` for L-to-R, `Skip(total - N)` for R-to-L, all if count=0.
3. Remove in **reverse index order** so earlier indices stay valid after each removal.

# Window_AssetRefLooking

## Problem 1: Scene GameObjects vs. Project Assets require different search strategies

**Symptom:** Dragging a Hierarchy GameObject into the tool and clicking "Find References" returned nothing — the tool only knew how to scan project-to-project dependencies.

**Root Cause:** `AssetDatabase.GetDependencies()` only works on project assets (files on disk). Scene GameObjects exist only in memory at runtime.

**Solution:** Split the search into two modes:
- **Mode A (Project Assets):** Reverse-scan all project assets using `AssetDatabase.GetDependencies()` to find which assets reference the target. Uses GUID matching for efficiency.
- **Mode B (Scene GameObjects):** Use `SerializedObject` / `SerializedProperty` to walk every component on the GameObject (and its children), extracting all `ObjectReference` properties that point to project assets.

An `isSceneObject` flag on each entry determines which mode is used.

## Problem 2: `MonoScript.FromMonoBehaviour` doesn't work for built-in components

**Symptom:** Built-in components like `Renderer`, `Collider`, `Transform` would not have their scripts listed in the results.

**Solution:** Created `FindScriptForComponent()` as a fallback — searches `Resources.FindObjectsOfTypeAll<MonoScript>()` and matches by `GetClass()`. Returns `null` for built-in Unity types (which is expected and harmless).

## Problem 3: Deep-scanning nested dependencies on Scene Objects

**Symptom:** Scene object scans missed textures assigned to custom shaders (e.g., BIRP ToonWater), and missed deeper dependencies like Animation Clips inside an AnimatorController.

**Root Cause:** A generic `SerializedProperty` walk isn't enough. Native array properties like `Renderer.sharedMaterials` can sometimes be skipped by `NextVisible()`. Furthermore, even if the Material is found, Unity's `AssetDatabase.GetDependencies()` sometimes fails to register textures in custom shaders as direct dependencies.

**Solution:** Implemented a robust 3-stage discovery pipeline for Scene objects:
1. **Explicit Component API reads:** Directly grab `Renderer.sharedMaterials` and `Animator.runtimeAnimatorController` to guarantee these common native references are never missed.
2. **Explicit Material Texture Extraction:** Iterate through discovered `.mat` files and explicitly extract textures using both `Material.GetTexturePropertyNameIDs()` and a direct `SerializedObject` walk on the material. This guarantees textures are found even if the shader isn't perfectly configured for the dependency graph.
3. **Universal `AssetDatabase.GetDependencies` Pass:** Finally, run `GetDependencies(..., true)` on all collected asset paths to recursively grab everything else (Prefab → Meshes, AnimatorController → Clips, etc.).

## Problem 4: Filter Types UI clipping on small window widths

**Symptom:** The row of `AssetTypeFilter` toggle buttons (Prefab, Material, Texture, etc.) in the filter section would get clipped off-screen if the editor window was resized to be too narrow, since `EditorGUILayout.BeginHorizontal()` doesn't automatically wrap items to a new line.

**Solution:** Implemented manual horizontal wrapping using `EditorStyles.miniButton.CalcSize()`:
- Keep a running `currentWidth` variable.
- For each flag, calculate its button width (`btnSize.x + 4`).
- If `currentWidth + buttonWidth > EditorGUIUtility.currentViewWidth - 20`, manually call `EditorGUILayout.EndHorizontal()` and start a new `BeginHorizontal()`.
- Add `GUILayout.Space(42)` on wrapped lines to align the buttons neatly under the "Types:" label from the first row.

# Window_MaterialPropsExecute

## Problem 1: Cross-overwriting when copying between overlapping material lists

**Symptom:** When List A and List B share some of the same material references, copying A → B would corrupt the results. For example, if `A[0]` and `B[1]` are the same material, modifying `B[1]` during the copy would also change `A[0]` before it gets read as a source for `B[0]`.

**Solution:** Buffer all source materials into temporary `new Material(source)` copies **before** applying any changes. After the copy is complete, `DestroyImmediate` the temporaries. This same pattern is used for both Copy and Swap operations.

```
Step 1: Buffer — tempSources[i] = new Material(sourceList[i])
Step 2: Apply  — target.CopyPropertiesFromMaterial(tempSources[i])
Step 3: Cleanup — DestroyImmediate(tempSources[i])
```
