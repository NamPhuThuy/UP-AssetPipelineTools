# UP-AssetPipeline-OptimizeTools

## Window_AssetNaming
old: EditorGUILayout.TextField(record.targetAsset.name, GUILayout.Width(80));
new: EditorGUILayout.TextField(GetAssetFileName(record.targetAsset), GUILayout.Width(80));

old: main = asset.name;
new: main = GetAssetFileName(asset);


old: if (string.IsNullOrEmpty(newName) || record.targetAsset.name == newName)
new: if (string.IsNullOrEmpty(newName) || GetAssetFileName(record.targetAsset) == newName)