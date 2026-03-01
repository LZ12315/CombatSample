using System;
using UnityEngine;

namespace DeiveEx.TagTree
{
    public class TagTreeSettingsSO : ScriptableObject
    {
        public enum TagsLoadSource
        {
            Resources,
            StreamingAssets,
        }

        //TODO allow users to assign multiple sources? This way users can easily define things like using the resources
        //folder for internal game tags and Streaming assets for external tags (for mods?)
        public TagsLoadSource LoadSource;
        public bool ShowLogs;

        public static event Action SettingsChanged;

        public LoadTagsStrategy GetLoadStrategyFromLoadSource()
        {
            return LoadSource switch
            {
                TagsLoadSource.Resources => new LoadTagsFromResourcesStrategy(),
                TagsLoadSource.StreamingAssets => new LoadTagsFromStreamingAssetsStrategy(),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private void OnValidate()
        {
            SettingsChanged?.Invoke();
        }
    }
}
