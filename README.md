
A collection of Unity Editor tools for managing project assets efficiently.  
Access all tools via the **NamPhuThuy → Assets Pipeline** menu.

---

# Window_AssetNaming

**Menu:** `NamPhuThuy → Assets Pipeline → Window - Asset Naming`

Batch asset renaming tool with a template-based naming system.

## Features

### Global Naming Template
- Build a naming rule from **Prefixes** + **Main Name** + **Suffixes**, each with configurable connect characters (`_`, `-`, `.`, space, or custom).
- Apply the template to all target assets at once.

### Target Assets
- Add assets via **drag & drop**, **Add Selected**, or by selecting in the Project window.
- Each asset can have its own inline rule override or inherit from the global template.
- Live preview of the final name for each asset.

### Batch Operations
| Operation | Description |
|---|---|
| **Clear Whitespace** | Strips all whitespace from asset names. |
| **Remove Substring** | Removes a specific substring. Supports **count** (0 = all) and **direction** (L-to-R or R-to-L). |
| **Replace Connect Char** | Replaces one separator character with another across all names. |
| **Change Case** | Converts names to **UPPER**, **lower**, **Title Case**, **camelCase**, **PascalCase**, or **snake_case**. |
| **Rename All** | Applies each asset's naming rule to produce the final name. |

### Undo / Redo
- All rename operations are tracked in a self-managed history stack.
- Use the **↩ Undo** / **Redo ↪** buttons in the tool window to reverse or re-apply any batch rename.

---

# Window_AssetRefLooking
**Menu:** `NamPhuThuy → Assets Pipeline → Window UITK - Asset Ref Looking`

Find all references to/from project assets and scene GameObjects.

## Features
- **Project Assets:** scans the entire project to find which assets reference the target.
- **Scene GameObjects:** inspects all components (including children) to list every project asset they use.
- **Filtering:** filter results by text search and asset type (Prefab, Material, Texture, Shader, Script, etc.).
- **Reference Contexts:** toggle "Show Context Details" to see exactly which component, material, or property is using a discovered asset (Scene Objects only).
- **Move to Folder:** batch-move all filtered results into a target folder with collision handling.
- Add targets via drag & drop from either Project or Hierarchy.

---

# Window_MaterialPropsExecute

**Menu:** `NamPhuThuy → Assets Pipeline → Window - Material Properties Execute`

Copy or swap shader properties between two lists of materials.

## Features
- **Copy A → B / B → A:** copies all shader properties (including the shader itself) from one list to another.
- **Swap A ↔ B:** swaps properties between paired materials.
- Uses buffered copies internally to avoid cross-overwriting issues.
- Full Unity Undo support.
- Both lists must have the same number of materials.

---

# Window_AssetFilter

**Menu:** `NamPhuThuy → Assets Pipeline → Window - AssetFilter`

Filter and extract specific asset types from one or more target folders.

## Features
- **Target Folders:** add multiple folders to search in via drag & drop or selection.
- **Filtering:** dynamically filter all found assets by text search and asset type (Prefab, Material, Texture, Shader, Script, etc.).
- **Results Preview:** view all filtered assets in a clean list with ping/select buttons.
- **Batch Move:** batch-move all filtered results into a new target folder, with automatic name collision handling.

---

# Window_ButtonPrefabReplacer

**Menu:** `NamPhuThuy → Assets Pipeline → Window - Button Prefab Replacer`

Replace button GameObjects (scene or open prefab) with instances of a button prefab, keeping their hierarchy setup.

## Features
- **Targets:** add via **Add Selected** (Hierarchy selection) or drag into the list. Assets, the prefab root, objects inside another prefab instance and children of another target are skipped (logged).
- **Kept (toggle in Options):** parent + sibling index, name, active / layer / tag, RectTransform anchors / pivot / position / size / rotation / scale, Button `onClick` / interactable / navigation, label text (TMP or legacy Text), root components the prefab lacks (e.g. `LayoutElement`; visuals and `Selectable` always come from the prefab).
- **Remap References:** fields in the same scene / prefab that pointed to the old button (its GameObject or root components) point to the new one. Fields left pointing to deleted old children are logged.
- **Move Old Children** (off by default): keep badges / icons by moving the old children under the new button.
- **Undo:** one Ctrl+Z reverts the whole batch.
