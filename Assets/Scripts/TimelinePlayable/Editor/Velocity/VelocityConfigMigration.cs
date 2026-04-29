#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 将工程内 Timeline（.playable）中的 <see cref="ActionVelocityClip"/> 的 <see cref="VelocityConfig"/>
/// 显式迁移为 overrideGravity / control 等字段，避免仅依赖运行时反序列化推断。
/// </summary>
public static class VelocityConfigMigration
{
    private const string MenuPath = "Tools/Combat Sample/Migrate VelocityConfig In Action Timelines";

    [MenuItem(MenuPath)]
    private static void MigrateAllInActionAssetTree()
    {
        const string searchInFolder = "Assets/Create/ActionAsset";
        string[] guids = AssetDatabase.FindAssets("glob:\"*.playable\"", new[] { searchInFolder });

        int clipCount = 0;
        int fileCount = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
                continue;

            var all = AssetDatabase.LoadAllAssetsAtPath(path);
            bool touched = false;

            foreach (var obj in all)
            {
                if (obj is not ActionVelocityClip vClip)
                    continue;

                vClip.config.ApplyMigrationRulesFromEditor();
                EditorUtility.SetDirty(vClip);
                clipCount++;
                touched = true;
            }

            if (touched)
                fileCount++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[VelocityConfigMigration] Done. playable files modified={fileCount}, ActionVelocityClip migrated={clipCount}.");
    }
}
#endif
