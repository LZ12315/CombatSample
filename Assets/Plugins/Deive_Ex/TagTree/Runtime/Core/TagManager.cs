using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[assembly: InternalsVisibleTo("DeiveEx.TagTree.Editor")]
[assembly: InternalsVisibleTo("DeiveEx.TagTree.EditorTests")]
[assembly: InternalsVisibleTo("DeiveEx.TagTree.PlayModeTests")]

namespace DeiveEx.TagTree
{
    internal static class TagManager
    {
        #region Fields

        internal const string TAGS_ASSET_EXTENSION = ".tags";
        private const string SETTINGS_ASSET_NAME = "TagTree_Settings";

        internal static readonly Dictionary<int, Tag> Tags = new();
        internal static LoadTagsStrategy LoadStrategy;

        #endregion

        #region Properties

        internal static TagTreeSettingsSO Settings { get; private set; }
        internal static bool IsInitialized => Settings != null;
        internal static bool ShowLogs => IsInitialized && Settings.ShowLogs;

        #endregion

        #region Unity Events

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RuntimeInitialize()
        {
            LoadSettings();
            LoadStrategy = Settings.GetLoadStrategyFromLoadSource();
            LoadTagsFromFiles();
        }

        #endregion

        #region Internal Methods

#if UNITY_EDITOR
        internal static void EditorInitialize()
        {
            LoadSettings();
            LoadStrategy = Settings.GetLoadStrategyFromLoadSource();
        }
#endif
        
        internal static void LoadTagsFromFiles()
        {
            List<string> allTags = new();
            
            foreach (var fileName in LoadStrategy.GetFileNames())
            {
                allTags.AddRange(LoadStrategy.GetTagsFromFile(fileName));
            }

            TagTreeUtils.LoadTagsFromNames(allTags, Tags);
        }

        internal static void LoadTagsFromNames(params string[] tagNames)
        {
            TagTreeUtils.LoadTagsFromNames(tagNames, Tags);
        }

        #endregion

        #region Private Methods
        
        private static void LoadSettings()
        {
            var settingsAsset = Resources.Load<TagTreeSettingsSO>(SETTINGS_ASSET_NAME);
            var settingsAssets = settingsAsset != null
                ? new[] { settingsAsset }
                : Resources.LoadAll<TagTreeSettingsSO>("");

            if (settingsAssets.Length == 0)
            {
#if UNITY_EDITOR
                settingsAssets = new[] { CreateSettingsAsset() };
#else
                TagTreeUtils.LogMessage("Unable to load settings asset. Make sure it exists in the Resources folder.", LogType.Error);
                return;
#endif
            }
            
            if (settingsAssets.Length > 1)
            {
                TagTreeUtils.LogMessage("Multiple TagTree settings detected. Make sure only one settings asset exists in the Resources folder.", LogType.Error);
                return;
            }

            Settings = settingsAssets[0];
            
            if(ShowLogs) TagTreeUtils.LogMessage("Settings loaded");
        }

#if UNITY_EDITOR
        private static TagTreeSettingsSO CreateSettingsAsset()
        {
            var asset = ScriptableObject.CreateInstance<TagTreeSettingsSO>();
            
            string resourcesPath = "Assets/Resources";
            if (!Directory.Exists(resourcesPath))
                Directory.CreateDirectory(resourcesPath);
            
            //Save asset to Resources
            string assetPath = Path.Combine(resourcesPath, $"{SETTINGS_ASSET_NAME}.asset");
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.LogWarning($"Created {SETTINGS_ASSET_NAME} asset at: {assetPath}");
            return asset;
        }
#endif

        #endregion
    }
}
